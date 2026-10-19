using Buscarr.Server.Data;
using Buscarr.Server.Images;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Endpoints;

public static class ImageEndpoints
{
    public static void MapImageEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/images", GetImage).RequireAuthorization();

    public static string? Proxied(string? url) =>
        url is null ? null : $"/api/images?url={Uri.EscapeDataString(url)}";

    private static async Task<IResult> GetImage(HttpContext context, BuscarrDbContext db, ImageCache cache, string url)
    {
        var stored = await db.ListedArtists.AnyAsync(artist => artist.Image == url)
            || await db.DownloadRequests.AnyAsync(request => request.CoverUrl == url);

        if (!stored)
        {
            return Results.NotFound();
        }

        if (await cache.GetOrFetchAsync(db, url) is not { } image)
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";

        return Results.Bytes(image.Bytes, image.ContentType);
    }
}
