using System.Security.Claims;
using System.Text.Json;
using Buscarr.Server.MusicBrainz;
using Microsoft.AspNetCore.Mvc;

namespace Buscarr.Server.Endpoints;

public record ArtistCard(string Id, string Name);

public record ReleaseItem(string Title, string Date, bool Live);

public record ArtistReleases(string Name, List<ReleaseItem> Releases);

public static class ArtistEndpoints
{
    private static readonly string[] ReleaseTypes = ["album", "ep", "single", "all"];

    public static void MapArtistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artists").RequireAuthorization();

        group.MapGet("/", SearchArtist);
        group.MapGet("/{id}/releases", GetArtistReleases);
    }

    private static async Task<IResult> SearchArtist(
        MusicBrainzClient musicBrainz,
        ClaimsPrincipal user,
        string q,
        string? country)
    {
        if (!string.IsNullOrEmpty(country) && !IsCountryCode(country))
        {
            return Results.BadRequest();
        }

        Artist? artist;

        try
        {
            artist = await musicBrainz.FindArtistAsync(
                Contact(user),
                q,
                string.IsNullOrEmpty(country) ? null : country.ToUpperInvariant());
        }
        catch (Exception e) when (IsUpstreamError(e))
        {
            return BadGateway(e);
        }

        return artist is null
            ? Results.NotFound()
            : Results.Json(new ArtistCard(artist.Id, artist.Name));
    }

    private static async Task<IResult> GetArtistReleases(
        MusicBrainzClient musicBrainz,
        ClaimsPrincipal user,
        string id,
        [FromQuery(Name = "type")] string? releaseType)
    {
        if (!id.All(c => char.IsAsciiHexDigit(c) || c == '-'))
        {
            return Results.NotFound();
        }

        var selectedType = releaseType ?? "album";

        if (!ReleaseTypes.Contains(selectedType))
        {
            return Results.BadRequest();
        }

        Artist artist;
        List<Release> releases;

        try
        {
            artist = await musicBrainz.ArtistAsync(Contact(user), id);
            releases = await musicBrainz.ReleasesAsync(
                Contact(user),
                id,
                selectedType == "all" ? null : selectedType);
        }
        catch (Exception e) when (IsUpstreamError(e))
        {
            return BadGateway(e);
        }

        var items = releases
            .Select(release => new ReleaseItem(release.Title, release.Date ?? "", release.IsLive))
            .ToList();

        return Results.Json(new ArtistReleases(artist.Name, items));
    }

    private static bool IsCountryCode(string country) =>
        country.Length == 2 && country.All(char.IsAsciiLetter);

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
