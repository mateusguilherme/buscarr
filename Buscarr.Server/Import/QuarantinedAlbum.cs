namespace Buscarr.Server.Import;

public class QuarantinedAlbum
{
    public int Id { get; init; }
    public int? RequestId { get; set; }
    public required string ArtistName { get; init; }
    public required string Title { get; init; }
    public required string Reason { get; init; }
    public required string Path { get; init; }
    public string? Username { get; init; }
    public DateTimeOffset At { get; init; }
}
