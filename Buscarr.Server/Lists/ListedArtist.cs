namespace Buscarr.Server.Lists;

public class ListedArtist
{
    public int UserId { get; set; }
    public required string ArtistId { get; set; }
    public required string Name { get; set; }
    public string? Disambiguation { get; set; }
    public string? Area { get; set; }
    public string? Image { get; set; }
    public List<string> ReleaseTypes { get; set; } = [];
}
