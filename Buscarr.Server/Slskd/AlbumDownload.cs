namespace Buscarr.Server.Slskd;

public enum AlbumDownloadOutcome
{
    Downloaded,
    Failed,
}

public enum FolderFailureReason
{
    Refused,
    TransferFailed,
    Stalled,
}

public record FolderFailure(SoulseekFolder Folder, FolderFailureReason Reason, string? Detail);

public record AlbumDownloadResult(AlbumDownloadOutcome Outcome, SoulseekFolder? Folder, List<FolderFailure> Failures);

public record DownloadProgress(
    SoulseekFolder Folder,
    int TracksDone,
    long BytesTransferred,
    long BytesTotal,
    int? PlaceInQueue);

public sealed class AlbumDownload(SlskdClient slskd, TimeProvider time)
{
    private const string Succeeded = "Completed, Succeeded";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StallLimit = TimeSpan.FromMinutes(10);

    public async Task<AlbumDownloadResult> DownloadAsync(
        IEnumerable<SoulseekFolder> folders,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellation = default)
    {
        var failures = new List<FolderFailure>();

        foreach (var folder in folders)
        {
            var failure = await DownloadFolderAsync(folder, progress, cancellation);

            if (failure is null)
            {
                return new(AlbumDownloadOutcome.Downloaded, folder, failures);
            }

            failures.Add(failure);
        }

        return new(AlbumDownloadOutcome.Failed, null, failures);
    }

    private async Task<FolderFailure?> DownloadFolderAsync(
        SoulseekFolder folder,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellation)
    {
        var known = await slskd.DownloadsAsync(folder.Username);
        var missing = folder.Files.Where(file => Pick(known, file) is null or { IsFailed: true }).ToList();

        try
        {
            if (missing.Count > 0)
            {
                await slskd.EnqueueDownloadsAsync(folder.Username, missing);
            }
        }
        catch (HttpRequestException e)
        {
            return new(folder, FolderFailureReason.Refused, e.StatusCode?.ToString());
        }

        var lastBytes = -1L;
        var lastProgress = time.GetTimestamp();

        while (true)
        {
            var transfers = await slskd.DownloadsAsync(folder.Username);
            var states = folder.Files.Select(file => Pick(transfers, file)).ToList();

            if (states.All(state => state?.Transfer.State == Succeeded))
            {
                return null;
            }

            if (states.FirstOrDefault(state => state is { IsFailed: true }) is { } failed)
            {
                await CancelAsync(folder, states);

                return new(folder, FolderFailureReason.TransferFailed, failed.Transfer.State);
            }

            var bytes = states.Sum(state => state?.Transfer.BytesTransferred ?? 0);

            if (bytes > lastBytes)
            {
                lastBytes = bytes;
                lastProgress = time.GetTimestamp();
            }
            else if (time.GetElapsedTime(lastProgress) >= StallLimit)
            {
                await CancelAsync(folder, states);

                return new(folder, FolderFailureReason.Stalled, null);
            }

            progress?.Report(new(
                folder,
                states.Count(state => state?.Transfer.State == Succeeded),
                bytes,
                folder.Files.Sum(file => file.Size),
                states.Min(state => state?.Transfer.PlaceInQueue)));

            try
            {
                await Task.Delay(PollInterval, time, cancellation);
            }
            catch (OperationCanceledException)
            {
                await CancelAsync(folder, states);

                throw;
            }
        }
    }

    private async Task CancelAsync(SoulseekFolder folder, IEnumerable<FileState?> states)
    {
        foreach (var state in states.OfType<FileState>().Where(state => !state.IsCompleted))
        {
            await slskd.CancelDownloadAsync(folder.Username, state.Transfer.Id);
        }
    }

    private static FileState? Pick(IEnumerable<SlskdTransfer> transfers, SlskdFile file) =>
        transfers
            .Where(transfer => transfer.Filename == file.Filename)
            .Select(transfer => new FileState(transfer))
            .OrderBy(state => state.Transfer.State == Succeeded ? 0 : state.IsFailed ? 2 : 1)
            .FirstOrDefault();

    private sealed record FileState(SlskdTransfer Transfer)
    {
        public bool IsCompleted => Transfer.State.StartsWith("Completed", StringComparison.Ordinal);

        public bool IsFailed => IsCompleted && Transfer.State != Succeeded;
    }
}
