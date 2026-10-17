using System.Security.Claims;
using System.Text.Json;
using Buscarr.Server.FanartTv;
using Buscarr.Server.MusicBrainz;

namespace Buscarr.Server.Endpoints;

public record ArtistCard(string Id, string Name, string? Disambiguation, string? Area, string? Image);

public record ReleaseItem(string Title, string Date, bool Live, string? Type);

public static class ArtistEndpoints
{
    public static void MapArtistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artists").RequireAuthorization();

        group.MapGet("/", SearchArtists);
        group.MapGet("/{id}", GetArtist);
        group.MapGet("/{id}/releases", GetArtistReleases);
    }

    private static async Task<IResult> SearchArtists(
        MusicBrainzClient musicBrainz,
        FanartTvClient fanartTv,
        ClaimsPrincipal user,
        string q)
    {
        List<Artist> artists;

        try
        {
            artists = await musicBrainz.FindArtistsAsync(Contact(user), q);
        }
        catch (Exception e) when (IsUpstreamError(e))
        {
            return BadGateway(e);
        }

        var images = await Task.WhenAll(artists.Select(artist => ArtistImageAsync(fanartTv, artist.Id)));

        return Results.Json(artists.Select((artist, index) => Card(artist, images[index])).ToList());
    }

    private static async Task<IResult> GetArtist(
        MusicBrainzClient musicBrainz,
        FanartTvClient fanartTv,
        ClaimsPrincipal user,
        string id)
    {
        if (!IsMusicBrainzId(id))
        {
            return Results.NotFound();
        }

        var image = ArtistImageAsync(fanartTv, id);
        Artist artist;

        try
        {
            artist = await musicBrainz.ArtistAsync(Contact(user), id);
        }
        catch (Exception e) when (IsUpstreamError(e))
        {
            return BadGateway(e);
        }

        return Results.Json(Card(artist, await image));
    }

    private static async Task<IResult> GetArtistReleases(
        MusicBrainzClient musicBrainz,
        ClaimsPrincipal user,
        string id)
    {
        if (!IsMusicBrainzId(id))
        {
            return Results.NotFound();
        }

        List<Release> releases;

        try
        {
            releases = await musicBrainz.ReleasesAsync(Contact(user), id);
        }
        catch (Exception e) when (IsUpstreamError(e))
        {
            return BadGateway(e);
        }

        return Results.Json(releases
            .Select(release => new ReleaseItem(release.Title, release.Date ?? "", release.IsLive, release.Type))
            .ToList());
    }

    private static ArtistCard Card(Artist artist, string? image) =>
        new(artist.Id, artist.Name, artist.Disambiguation, artist.Area?.Name, image);

    private static async Task<string?> ArtistImageAsync(FanartTvClient fanartTv, string id)
    {
        try
        {
            return await fanartTv.ArtistImageAsync(id);
        }
        catch (Exception e) when (IsUpstreamError(e))
        {
            Console.Error.WriteLine($"erro no fanart.tv: {e.Message}");

            return null;
        }
    }

    private static bool IsMusicBrainzId(string id) =>
        id.All(c => char.IsAsciiHexDigit(c) || c == '-');

    private static string Contact(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) ?? "";

    private static bool IsUpstreamError(Exception e) =>
        e is HttpRequestException or JsonException or TaskCanceledException;

    private static IResult BadGateway(Exception e)
    {
        Console.Error.WriteLine($"erro no MusicBrainz: {e.Message}");

        return Results.StatusCode(StatusCodes.Status502BadGateway);
    }
}
