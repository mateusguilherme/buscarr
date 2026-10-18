using System.Collections.Concurrent;
using Buscarr.Server.Data;
using Buscarr.Server.Import;
using Buscarr.Server.Slskd;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Downloads;

public sealed class DownloadRunner(
    IServiceScopeFactory scopes,
    AlbumDownload download,
    TimeProvider time,
    ImportPipeline pipeline)
{
    private const int MaxConcurrent = 3;

    private readonly SemaphoreSlim _slots = new(MaxConcurrent, MaxConcurrent);
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _running = new();
    private readonly Lock _writing = new();

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

    public void StartImport(int requestId)
    {
        var cancellation = new CancellationTokenSource();

        if (!_running.TryAdd(requestId, cancellation))
        {
            cancellation.Dispose();

            return;
        }

        _ = ReimportAsync(requestId, cancellation.Token);
    }

    public void StartQuarantineImport(int requestId, int quarantineId)
    {
        var cancellation = new CancellationTokenSource();

        if (!_running.TryAdd(requestId, cancellation))
        {
            cancellation.Dispose();

            return;
        }

        _ = QuarantineImportAsync(requestId, quarantineId, cancellation.Token);
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
                var snapshot = await SnapshotAsync(requestId);
                var tracks = snapshot is null ? null : await pipeline.TracksAsync(snapshot);
                var verify = tracks is null
                    ? null
                    : (Func<SoulseekFolder, Task<string?>>)(async folder =>
                    {
                        var rejection = await pipeline.VerifyAsync(snapshot!, tracks, folder);

                        if (rejection is { Rejected: true })
                        {
                            RecordRejection(requestId, snapshot!, folder.Username, folder.Directory, rejection);
                        }

                        return rejection?.Reason;
                    });
                var result = await download.DownloadAsync(
                    folders,
                    new RowProgress(this, requestId),
                    verify,
                    cancellation);
                var importing = result.Outcome == AlbumDownloadOutcome.Downloaded && tracks is not null;
                Persist(requestId, request => Finish(request, result, importing));

                if (importing)
                {
                    var failure = await pipeline.ImportAsync(snapshot!, tracks!, result.Folder!);
                    Persist(requestId, request => FinishImport(request, failure?.Reason, result.Folder!.Quality));
                }
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (OperationCanceledException)
        {
            Persist(requestId, Cancelled);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao baixar o pedido {requestId}: {e.Message}");
            Persist(requestId, request =>
            {
                request.State = request.AcceptedQuality is null ? DownloadState.Waiting : DownloadState.Imported;
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

    private async Task ReimportAsync(int requestId, CancellationToken cancellation)
    {
        try
        {
            await _slots.WaitAsync(cancellation);

            try
            {
                var snapshot = await SnapshotAsync(requestId);

                if (snapshot is null)
                {
                    return;
                }

                var tracks = await pipeline.TracksAsync(snapshot);
                var failure = tracks is null
                    ? new ImportFailure("import-failed")
                    : await pipeline.ReimportAsync(snapshot, tracks);

                if (failure is { Rejected: true })
                {
                    RecordRejection(requestId, snapshot, snapshot.Username, snapshot.Directory, failure);
                }

                Persist(requestId, request => FinishImport(request, failure?.Reason));
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (OperationCanceledException)
        {
            Persist(requestId, Cancelled);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao importar o pedido {requestId}: {e.Message}");
            Persist(requestId, request => request.State = DownloadState.Downloaded);
        }
        finally
        {
            if (_running.TryRemove(requestId, out var finished))
            {
                finished.Dispose();
            }
        }
    }

    private async Task QuarantineImportAsync(int requestId, int quarantineId, CancellationToken cancellation)
    {
        try
        {
            await _slots.WaitAsync(cancellation);

            try
            {
                var snapshot = await SnapshotAsync(requestId);

                if (snapshot is null)
                {
                    return;
                }

                var quarantined = await QuarantinedAsync(quarantineId);
                var tracks = quarantined is null ? null : await pipeline.TracksAsync(snapshot);
                var failure = quarantined is null
                    ? new ImportFailure("missing-files")
                    : tracks is null
                        ? new ImportFailure("import-failed")
                        : await pipeline.ImportQuarantinedAsync(snapshot, tracks, quarantined.Path);

                if (failure is null)
                {
                    RemoveQuarantined(quarantineId);
                }

                Persist(requestId, request => FinishImport(request, failure?.Reason));
            }
            finally
            {
                _slots.Release();
            }
        }
        catch (OperationCanceledException)
        {
            Persist(requestId, Cancelled);
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao importar a quarentena {quarantineId}: {e.Message}");
            Persist(requestId, request => FinishImport(request, "import-failed"));
        }
        finally
        {
            if (_running.TryRemove(requestId, out var finished))
            {
                finished.Dispose();
            }
        }
    }

    private void Finish(DownloadRequest request, AlbumDownloadResult result, bool importing)
    {
        if (result.Outcome == AlbumDownloadOutcome.Downloaded)
        {
            request.State = importing ? DownloadState.Importing : DownloadState.Downloaded;
            request.Failure = null;
            request.Username = result.Folder!.Username;
            request.Directory = result.Folder.Directory;
            request.TracksDone = result.Folder.Files.Count;
            request.BytesTotal = result.Folder.Files.Sum(file => file.Size);
            request.BytesTransferred = request.BytesTotal;
            request.PlaceInQueue = null;
        }
        else if (request.AcceptedQuality is not null)
        {
            request.State = DownloadState.Imported;
            request.NextAttemptAt = RetrySchedule.UpgradeRetry(time.GetUtcNow());
        }
        else
        {
            request.State = DownloadState.Failed;
            request.Failure = FailureOf(result);
            request.Attempts++;
            request.NextAttemptAt = RetrySchedule.NextRetry(time.GetUtcNow(), request.Attempts);
        }
    }

    private static void Cancelled(DownloadRequest request)
    {
        if (request.AcceptedQuality is not null)
        {
            request.State = DownloadState.Imported;
            request.AcceptedQuality = null;

            return;
        }

        request.State = DownloadState.Cancelled;
    }

    private static string FailureOf(AlbumDownloadResult result) =>
        result.Failures.Count > 0 ? Reason(result.Failures[^1]) : "transfer-failed";

    private void FinishImport(DownloadRequest request, string? failure, string? obtainedQuality = null)
    {
        if (failure is null)
        {
            request.State = DownloadState.Imported;
            request.Failure = null;

            if (obtainedQuality is not null && obtainedQuality != request.Quality)
            {
                request.AcceptedQuality = obtainedQuality;
                request.NextAttemptAt = RetrySchedule.UpgradeRetry(time.GetUtcNow());
            }
            else
            {
                request.AcceptedQuality = null;
            }

            return;
        }

        if (request.AcceptedQuality is not null)
        {
            request.State = DownloadState.Imported;
            request.NextAttemptAt = RetrySchedule.UpgradeRetry(time.GetUtcNow());

            return;
        }

        request.State = DownloadState.Failed;
        request.Failure = failure;
        request.Attempts++;
        request.NextAttemptAt = RetrySchedule.NextRetry(time.GetUtcNow(), request.Attempts);
    }

    private async Task<DownloadRequest?> SnapshotAsync(int requestId)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();

        return await db.DownloadRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == requestId);
    }

    private async Task<QuarantinedAlbum?> QuarantinedAsync(int quarantineId)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();

        return await db.QuarantinedAlbums.AsNoTracking().SingleOrDefaultAsync(q => q.Id == quarantineId);
    }

    private void RecordRejection(
        int requestId,
        DownloadRequest snapshot,
        string? username,
        string? directory,
        ImportFailure rejection)
    {
        lock (_writing)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();

            if (!db.DownloadRequests.Any(r => r.Id == requestId))
            {
                return;
            }

            if (username is not null
                && directory is not null
                && !db.FailedSources.Any(s => s.RequestId == requestId && s.Username == username && s.Directory == directory))
            {
                db.FailedSources.Add(new FailedSource { RequestId = requestId, Username = username, Directory = directory });
            }

            if (rejection.QuarantinePath is not null)
            {
                db.QuarantinedAlbums.Add(new QuarantinedAlbum
                {
                    RequestId = requestId,
                    ArtistName = snapshot.ArtistName,
                    Title = snapshot.Title,
                    Reason = rejection.Reason,
                    Path = rejection.QuarantinePath,
                    Username = username,
                    At = time.GetUtcNow(),
                });
            }

            db.SaveChanges();
        }
    }

    private void RemoveQuarantined(int quarantineId)
    {
        lock (_writing)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
            var quarantined = db.QuarantinedAlbums.SingleOrDefault(q => q.Id == quarantineId);

            if (quarantined is null)
            {
                return;
            }

            db.QuarantinedAlbums.Remove(quarantined);
            db.SaveChanges();

            try
            {
                File.Delete($"{quarantined.Path}.reason.txt");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"erro ao apagar o motivo da quarentena {quarantined.Path}: {e.Message}");
            }
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

    private static string Reason(FolderFailure failure) =>
        failure.Reason switch
        {
            FolderFailureReason.Refused => "refused",
            FolderFailureReason.Stalled => "stalled",
            FolderFailureReason.Rejected => failure.Detail ?? "rejected",
            _ => "transfer-failed",
        };

    private sealed class RowProgress(DownloadRunner runner, int requestId) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => runner.Report(requestId, value);
    }
}
