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
    public int Id { get; set; }
    public required string ArtistId { get; set; }
    public required string ArtistName { get; set; }
    public required string ReleaseGroupId { get; set; }
    public required string Title { get; set; }
    public required string Type { get; set; }
    public string? FirstReleaseDate { get; set; }
    public string? EditionReleaseId { get; set; }
    public int TrackCount { get; set; }
    public required string Quality { get; set; }
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
