using System.Security.Claims;
using Buscarr.Server.Downloads;
using Buscarr.Server.Lists;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Users;

namespace Buscarr.Server.Endpoints;

public record ListSettings(List<string> ReleaseTypes);

public static class ListEndpoints
{
    public static void MapListEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/list").RequireAuthorization();

        group.MapGet("/", GetList);
        group.MapGet("/{id}", GetEntry);
        group.MapPut("/{id}", AddArtist);
        group.MapDelete("/{id}", RemoveArtist);
    }

    private static async Task<List<ArtistCard>> GetList(ArtistListStore list, ClaimsPrincipal user) =>
        [.. (await list.ArtistsAsync(user.UserId()))
            .Select(artist => new ArtistCard(
                artist.ArtistId,
                artist.Name,
                Disambiguations.Clean(artist.Disambiguation, artist.Area),
                artist.Area,
                ImageEndpoints.Proxied(artist.Image),
                true))];

    private static async Task<IResult> GetEntry(ArtistListStore list, ClaimsPrincipal user, string id) =>
        await list.EntryAsync(user.UserId(), id) is { } artist
            ? Results.Json(new ListSettings(artist.ReleaseTypes))
            : Results.NotFound();

    private static async Task<IResult> AddArtist(
        MusicBrainzClient musicBrainz,
        ArtistImages images,
        ArtistListStore list,
        GeneratorTrigger generator,
        ClaimsPrincipal user,
        string id,
        ListSettings settings)
    {
        if (!ArtistEndpoints.IsMusicBrainzId(id))
        {
            return Results.NotFound();
        }

        if (settings.ReleaseTypes.Count == 0 || !settings.ReleaseTypes.All(ListReleaseTypes.All.Contains))
        {
            return Results.BadRequest();
        }

        Artist artist;

        try
        {
            artist = await musicBrainz.ArtistAsync(ArtistEndpoints.Contact(user), id);
        }
        catch (Exception e) when (ArtistEndpoints.IsUpstreamError(e))
        {
            return ArtistEndpoints.BadGateway(e);
        }

        await list.AddAsync(new ListedArtist
        {
            UserId = user.UserId(),
            ArtistId = id,
            Name = artist.Name,
            Disambiguation = artist.Disambiguation,
            Area = artist.Area?.Name,
            Image = await images.FindAsync(artist),
            ReleaseTypes = [.. ListReleaseTypes.All.Where(settings.ReleaseTypes.Contains)],
        });
        generator.Fire();

        return Results.NoContent();
    }

    private static async Task<IResult> RemoveArtist(
        ArtistListStore list,
        GeneratorTrigger generator,
        ClaimsPrincipal user,
        string id)
    {
        if (!ArtistEndpoints.IsMusicBrainzId(id))
        {
            return Results.NotFound();
        }

        await list.RemoveAsync(user.UserId(), id);
        generator.Fire();

        return Results.NoContent();
    }
}
