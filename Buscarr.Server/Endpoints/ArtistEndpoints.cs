using System.Security.Claims;
using System.Text.Json;
using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.FanartTv;
using Buscarr.Server.Lists;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Subsonic;
using Buscarr.Server.Users;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Endpoints;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record ArtistCard(string Id, string Name, string? Disambiguation, string? Area, string? Image, bool Listed);

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record ArtistRelease(
    string ReleaseGroupId,
    string Title,
    string Type,
    string? FirstReleaseDate,
    string? CoverUrl,
    int TrackCount,
    bool Requested,
    bool InLibrary);

public static class ArtistEndpoints
{
    public static void MapArtistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artists").RequireAuthorization();

        group.MapGet("/", SearchArtists);
        group.MapGet("/{id}/releases", GetReleases);
    }

    private static async Task<IResult> GetReleases(
        MusicBrainzClient musicBrainz,
        BuscarrDbContext db,
        UserStore users,
        AlbumCovers covers,
        SubsonicClient subsonic,
        ClaimsPrincipal user,
        string id)
    {
        if (!IsMusicBrainzId(id))
        {
            return Results.NotFound();
        }

        List<Album> albums;

        try
        {
            albums = await musicBrainz.ReleasesAsync(Contact(user), id);
        }
        catch (Exception e) when (IsUpstreamError(e))
        {
            return BadGateway(e);
        }

        var edition = await users.EditionPreferenceAsync(user.UserId());
        var groups = albums.Select(album => album.First.ReleaseGroup?.Id).OfType<string>().ToList();
        var requested = (await db.DownloadRequests
                .Where(request => groups.Contains(request.ReleaseGroupId))
                .Select(request => request.ReleaseGroupId)
                .ToListAsync())
            .ToHashSet();

        var pairs = albums
            .Where(album => album.First.ReleaseGroup?.Id is not null)
            .Select(album => (Album: album, Chosen: Editions.Choose(album.Editions, edition)))
            .Where(pair => pair.Chosen.TrackCount > 0)
            .ToList();
        var library = await LibraryAsync(subsonic);
        var coverUrls = await Task.WhenAll(pairs.Select(pair =>
            covers.FindAsync(pair.Album, pair.Chosen, pair.Album.First.CreditedArtists)));

        return Results.Json(pairs
            .Select((pair, index) => new ArtistRelease(
                pair.Album.First.ReleaseGroup!.Id!,
                pair.Album.First.Title,
                pair.Album.First.Category ?? "album",
                pair.Album.First.ReleaseGroup!.FirstReleaseDate ?? pair.Album.First.Date,
                coverUrls[index] ?? pair.Chosen.CoverUrl ?? pair.Album.First.CoverUrl,
                pair.Chosen.TrackCount,
                requested.Contains(pair.Album.First.ReleaseGroup!.Id!),
                InLibrary(library, pair.Album, pair.Chosen)))
            .OrderByDescending(release => release.FirstReleaseDate ?? "", StringComparer.Ordinal)
            .ToList());
    }

    private static async Task<LibraryIndex?> LibraryAsync(SubsonicClient subsonic)
    {
        if (!subsonic.IsConfigured)
        {
            return null;
        }

        try
        {
            return new LibraryIndex(await subsonic.AlbumsAsync());
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao listar a biblioteca: {e.Message}");

            return null;
        }
    }

    private static bool InLibrary(LibraryIndex? library, Album album, Release chosen)
    {
        var ids = album.Editions
            .Select(release => release.Id)
            .OfType<string>()
            .Append(album.First.ReleaseGroup!.Id!);

        return library?.TrackCount(ids, album.First.Title, album.First.CreditedAs ?? "") >= chosen.TrackCount;
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
        new(
            artist.Id,
            artist.Name,
            Disambiguations.Clean(artist.Disambiguation, artist.Area?.Name),
            artist.Area?.Name,
            image,
            listed);

    internal static async Task<string?> ArtistImageAsync(FanartTvClient fanartTv, string id)
    {
        try
        {
            return await fanartTv.ArtistImageAsync(id);
        }
        catch (Exception e) when (IsUpstreamError(e))
        {
            await Console.Error.WriteLineAsync($"erro no fanart.tv: {e.Message}");

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
