namespace Buscarr.Server.Downloads;

public class SearchCandidate
{
    public int Id { get; init; }
    public int RequestId { get; init; }
    public required string Username { get; init; }
    public required string Directory { get; init; }
    public int FileCount { get; init; }
    public required string Format { get; init; }
    public int? BitRate { get; init; }
    public bool HasFreeUploadSlot { get; init; }
    public int QueueLength { get; init; }
    public string? Rejection { get; init; }
}
