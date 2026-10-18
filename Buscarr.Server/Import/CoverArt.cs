namespace Buscarr.Server.Import;

public sealed record CoverImage(byte[] Bytes, string MimeType, string Extension);

public sealed class CoverArt(HttpClient http)
{
    public async Task<CoverImage?> DownloadAsync(string? url)
    {
        if (url is null)
        {
            return null;
        }

        try
        {
            var full = url.EndsWith("-250", StringComparison.Ordinal) ? url[..^4] : url;
            var bytes = await http.GetByteArrayAsync(full);

            return Identify(bytes);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao baixar a capa {url}: {e.Message}");

            return null;
        }
    }

    private static CoverImage? Identify(byte[] bytes) =>
        bytes switch
        {
            [0xFF, 0xD8, ..] => new CoverImage(bytes, "image/jpeg", ".jpg"),
            [0x89, 0x50, 0x4E, 0x47, ..] => new CoverImage(bytes, "image/png", ".png"),
            _ => null,
        };
}
