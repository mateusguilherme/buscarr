namespace Buscarr.Server.Lists;

public class ListedArtist
{
    public int UserId { get; init; }
    public required string ArtistId { get; init; }
    public required string Name { get; init; }
    public string? Disambiguation { get; init; }
    public string? Area { get; init; }
    public string? Image { get; set; }
    public List<string> ReleaseTypes { get; init; } = [];
}
