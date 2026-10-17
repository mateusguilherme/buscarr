using System.Collections.Concurrent;
using Buscarr.Server.Data;
using Buscarr.Server.Slskd;

namespace Buscarr.Server.Downloads;

public sealed class DownloadRunner(IServiceScopeFactory scopes, AlbumDownload download, TimeProvider time)
{
    private const int MaxConcurrent = 3;

    private readonly SemaphoreSlim _slots = new(MaxConcurrent, MaxConcurrent);
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _running = new();
    private readonly object _writing = new();

    public void Start(int requestId, List<SoulseekFolder> folders)
    {
        var cancellation = new CancellationTokenSource();

        if (!_running.TryAdd(requestId, cancellation))
        {
            cancellation.Dispose();

            return;
        }

        _ = RunAsync(requestId, folders, cancellation.Token);
    }

    public void Cancel(int requestId)
    {
        if (_running.TryGetValue(requestId, out var cancellation))
        {
            cancellation.Cancel();
        }
    }

    private async Task RunAsync(int requestId, List<SoulseekFolder> folders, CancellationToken cancellation)
    {
        try
        {
            await _slots.WaitAsync(cancellation);

            try
            {
                var result = await download.DownloadAsync(folders, new RowProgress(this, requestId), cancellation);
                Persist(requestId, request => Finish(request, result));
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (OperationCanceledException)
        {
            Persist(requestId, request => request.State = DownloadState.Cancelled);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"erro ao baixar o pedido {requestId}: {e.Message}");
            Persist(requestId, request =>
            {
                request.State = DownloadState.Waiting;
                request.NextAttemptAt = RetrySchedule.AfterBusy(time.GetUtcNow());
            });
        }
        finally
        {
            if (_running.TryRemove(requestId, out var finished))
            {
                finished.Dispose();
            }
        }
    }

    private void Finish(DownloadRequest request, AlbumDownloadResult result)
    {
        if (result.Outcome == AlbumDownloadOutcome.Downloaded)
        {
            request.State = DownloadState.Downloaded;
            request.Failure = null;
            request.Username = result.Folder!.Username;
            request.Directory = result.Folder.Directory;
            request.TracksDone = result.Folder.Files.Count;
            request.BytesTotal = result.Folder.Files.Sum(file => file.Size);
            request.BytesTransferred = request.BytesTotal;
            request.PlaceInQueue = null;
        }
        else
        {
            request.State = DownloadState.Failed;
            request.Failure = result.Failures.Count > 0 ? Reason(result.Failures[^1].Reason) : "transfer-failed";
            request.Attempts++;
            request.NextAttemptAt = RetrySchedule.NextRetry(time.GetUtcNow(), request.Attempts);
        }
    }

    private void Report(int requestId, DownloadProgress value) =>
        Persist(requestId, request =>
        {
            request.Username = value.Folder.Username;
            request.Directory = value.Folder.Directory;
            request.TracksDone = value.TracksDone;
            request.BytesTransferred = value.BytesTransferred;
            request.BytesTotal = value.BytesTotal;
            request.PlaceInQueue = value.PlaceInQueue;
        });

    private void Persist(int requestId, Action<DownloadRequest> change)
    {
        lock (_writing)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
            var request = db.DownloadRequests.SingleOrDefault(r => r.Id == requestId);

            if (request is null)
            {
                return;
            }

            change(request);
            db.SaveChanges();
        }
    }

    private static string Reason(FolderFailureReason reason) =>
        reason switch
        {
            FolderFailureReason.Refused => "refused",
            FolderFailureReason.Stalled => "stalled",
            _ => "transfer-failed",
        };

    private sealed class RowProgress(DownloadRunner runner, int requestId) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => runner.Report(requestId, value);
    }
}
