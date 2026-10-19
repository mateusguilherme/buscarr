namespace Buscarr.Server.Images;

public class CachedImage
{
    public int Id { get; set; }

    public required string Url { get; set; }

    public required string ContentType { get; set; }

    public required byte[] Bytes { get; set; }
}
