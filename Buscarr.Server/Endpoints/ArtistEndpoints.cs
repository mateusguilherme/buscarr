using System.Security.Claims;
using System.Text.Json;
using Buscarr.Server.MusicBrainz;

namespace Buscarr.Server.Endpoints;

public record ArtistCard(string Id, string Name, string? Disambiguation, string? Area);

public record ReleaseItem(string Title, string Date, bool Live, string? Type);

public record ArtistReleases(string Name, List<ReleaseItem> Releases);

public static class ArtistEndpoints
{
    public static void MapArtistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artists").RequireAuthorization();

        group.MapGet("/", SearchArtists);
        group.MapGet("/{id}/releases", GetArtistReleases);
    }

    private static async Task<IResult> SearchArtists(
        MusicBrainzClient musicBrainz,
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

        return Results.Json(artists
            .Select(artist => new ArtistCard(artist.Id, artist.Name, artist.Disambiguation, artist.Area?.Name))
            .ToList());
    }

    private static async Task<IResult> GetArtistReleases(
        MusicBrainzClient musicBrainz,
        ClaimsPrincipal user,
        string id)
    {
        if (!id.All(c => char.IsAsciiHexDigit(c) || c == '-'))
        {
            return Results.NotFound();
        }

        Artist artist;
        List<Release> releases;

        try
        {
            artist = await musicBrainz.ArtistAsync(Contact(user), id);
            releases = await musicBrainz.ReleasesAsync(Contact(user), id);
        }
        catch (Exception e) when (IsUpstreamError(e))
        {
            return BadGateway(e);
        }

        var items = releases
            .Select(release => new ReleaseItem(release.Title, release.Date ?? "", release.IsLive, release.Type))
            .ToList();

        return Results.Json(new ArtistReleases(artist.Name, items));
    }

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
