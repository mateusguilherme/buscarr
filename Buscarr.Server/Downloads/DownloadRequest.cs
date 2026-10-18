namespace Buscarr.Server.Downloads;

public enum DownloadState
{
    Waiting,
    Searching,
    NotFound,
    Downloading,
    Downloaded,
    Failed,
    Cancelled,
    Importing,
    Imported,
}

public class DownloadRequest
{
    public int Id { get; init; }
    public required string ArtistId { get; init; }
    public required string ArtistName { get; init; }
    public required string ReleaseGroupId { get; init; }
    public required string Title { get; init; }
    public required string Type { get; init; }
    public string? FirstReleaseDate { get; init; }
    public string? EditionReleaseId { get; set; }
    public int TrackCount { get; init; }
    public required string Quality { get; init; }
    public string? CoverUrl { get; set; }
    public DownloadState State { get; set; }
    public string? Failure { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? Username { get; set; }
    public string? Directory { get; set; }
    public int TracksDone { get; set; }
    public long BytesTransferred { get; set; }
    public long BytesTotal { get; set; }
    public int? PlaceInQueue { get; set; }
}
