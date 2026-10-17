using System.Security.Claims;
using System.Text.Json;
using Buscarr.Server.FanartTv;
using Buscarr.Server.Lists;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Users;

namespace Buscarr.Server.Endpoints;

public record ArtistCard(string Id, string Name, string? Disambiguation, string? Area, string? Image, bool Listed);

public static class ArtistEndpoints
{
    public static void MapArtistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artists").RequireAuthorization();

        group.MapGet("/", SearchArtists);
    }

    private static async Task<IResult> SearchArtists(
        MusicBrainzClient musicBrainz,
        FanartTvClient fanartTv,
        ArtistListStore list,
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
        var listed = await list.ListedAsync(user.UserId(), artists.Select(artist => artist.Id));

        return Results.Json(artists
            .Select((artist, index) => Card(artist, images[index], listed.Contains(artist.Id)))
            .ToList());
    }

    private static ArtistCard Card(Artist artist, string? image, bool listed) =>
        new(artist.Id, artist.Name, artist.Disambiguation, artist.Area?.Name, image, listed);

    internal static async Task<string?> ArtistImageAsync(FanartTvClient fanartTv, string id)
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

    internal static bool IsMusicBrainzId(string id) =>
        id.All(c => char.IsAsciiHexDigit(c) || c == '-');

    internal static string Contact(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email) ?? "";

    internal static bool IsUpstreamError(Exception e) =>
        e is HttpRequestException or JsonException or TaskCanceledException;

    internal static IResult BadGateway(Exception e)
    {
        Console.Error.WriteLine($"erro no MusicBrainz: {e.Message}");

        return Results.StatusCode(StatusCodes.Status502BadGateway);
    }
}
