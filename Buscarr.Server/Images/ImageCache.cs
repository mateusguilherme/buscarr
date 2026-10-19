using Buscarr.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Images;

public sealed class ImageCache(HttpClient http)
{
    public async Task<CachedImage?> GetOrFetchAsync(BuscarrDbContext db, string url)
    {
        if (await db.CachedImages.FirstOrDefaultAsync(image => image.Url == url) is { } cached)
        {
            return cached;
        }

        if (await FetchAsync(url) is not { } fetched)
        {
            return null;
        }

        db.CachedImages.Add(fetched);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            db.Entry(fetched).State = EntityState.Detached;

            return await db.CachedImages.FirstAsync(image => image.Url == url);
        }

        return fetched;
    }

    private async Task<CachedImage?> FetchAsync(string url)
    {
        try
        {
            using var response = await http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return new CachedImage
            {
                Url = url,
                ContentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg",
                Bytes = await response.Content.ReadAsByteArrayAsync(),
            };
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao baixar a imagem {url}: {e.Message}");

            return null;
        }
    }
}
