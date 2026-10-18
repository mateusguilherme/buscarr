using Buscarr.Server.Slskd;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Buscarr.Server.Tests.Slskd;

public class AlbumDownloadTests
{
    private const string Succeeded = "Completed, Succeeded";

    private readonly FakeSlskd _slskd = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Reports _progress = new();
    private readonly AlbumDownload _download;

    public AlbumDownloadTests()
    {
        var client = new SlskdClient(new HttpClient(_slskd.Handler), "http://slskd.local:5030", "chave-de-teste");
        _download = new AlbumDownload(client, _time);
    }

    [Fact]
    public async Task DownloadAsync_AsksForEveryFileOfTheFolderAndWaitsUntilAllArrive()
    {
        var folder = Folder("fulano");
        _slskd.Peer("fulano", (poll, _) => poll < 3 ? new("InProgress", 500) : new(Succeeded));

        var result = await DownloadAsync(folder);

        Assert.Equal(AlbumDownloadOutcome.Downloaded, result.Outcome);
        Assert.Same(folder, result.Folder);
        Assert.Empty(result.Failures);
        Assert.Equal(
            folder.Files.Select(file => ("fulano", file.Filename, file.Size)),
            _slskd.Enqueued);
        Assert.Empty(_slskd.Cancelled);
    }

    [Fact]
    public async Task DownloadAsync_RemovesTheTransfersFromSlskdAfterTheFolderArrives()
    {
        var folder = Folder("fulano");

        var result = await DownloadAsync(folder);

        Assert.Equal(AlbumDownloadOutcome.Downloaded, result.Outcome);
        Assert.Equal(folder.Files.Select(file => ("fulano", file.Filename)), _slskd.Removed);
        Assert.Empty(_slskd.Cancelled);
    }

    [Fact]
    public async Task DownloadAsync_RemovesTheFailedTransfersWhenGivingUpOnAFolder()
    {
        var rejected = Folder("fulano");
        var next = Folder("beltrano");
        _slskd.Peer("fulano", (_, file) => file.EndsWith("02.mp3") ? new("Completed, Rejected") : new("InProgress", 100));

        await DownloadAsync(rejected, next);

        Assert.Equal(
            [("fulano", rejected.Files[1].Filename), .. next.Files.Select(file => ("beltrano", file.Filename))],
            _slskd.Removed);
        Assert.Equal(
            [("fulano", rejected.Files[0].Filename), ("fulano", rejected.Files[2].Filename)],
            _slskd.Cancelled);
    }

    [Fact]
    public async Task DownloadAsync_ReportsTheTracksDoneTheBytesAndThePlaceInThePeersQueue()
    {
        var folder = Folder("fulano");
        _slskd.Peer("fulano", (poll, file) => (poll, file.EndsWith("01.mp3")) switch
        {
            (1, _) => new("Queued, Remotely", 0, 4),
            (2, true) => new(Succeeded),
            (2, false) => new("InProgress", 250),
            _ => new(Succeeded),
        });

        await DownloadAsync(folder);

        Assert.Equal(
            [(0, 0L, 3_000L, (int?)4), (1, 1_500L, 3_000L, null)],
            _progress.All.Select(report =>
                (report.TracksDone, report.BytesTransferred, report.BytesTotal, report.PlaceInQueue)));
    }

    [Fact]
    public async Task DownloadAsync_MovesToTheNextFolderWhenAFileFailsAndCancelsTheRest()
    {
        var rejected = Folder("fulano");
        var next = Folder("beltrano");
        _slskd.Peer("fulano", (_, file) => file.EndsWith("02.mp3") ? new("Completed, Rejected") : new("InProgress", 100));

        var result = await DownloadAsync(rejected, next);

        Assert.Equal(AlbumDownloadOutcome.Downloaded, result.Outcome);
        Assert.Same(next, result.Folder);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(
            (rejected, FolderFailureReason.TransferFailed, "Completed, Rejected"),
            (failure.Folder, failure.Reason, failure.Detail));
        Assert.Equal(
            [("fulano", rejected.Files[0].Filename), ("fulano", rejected.Files[2].Filename)],
            _slskd.Cancelled);
    }

    [Fact]
    public async Task DownloadAsync_MovesToTheNextFolderWhenThePeerCannotBeAsked()
    {
        var refused = Folder("fulano");
        _slskd.RefuseDownloadsFrom("fulano");

        var result = await DownloadAsync(refused, Folder("beltrano"));

        Assert.Equal(AlbumDownloadOutcome.Downloaded, result.Outcome);
        Assert.Equal("beltrano", result.Folder!.Username);
        Assert.Equal(FolderFailureReason.Refused, Assert.Single(result.Failures).Reason);
    }

    [Fact]
    public async Task DownloadAsync_GivesUpOnAFolderWhenNothingArrivesForTenMinutes()
    {
        var stuck = Folder("fulano");
        _slskd.Peer("fulano", (_, _) => new("Queued, Remotely", 0, 30));
        var started = _time.GetUtcNow();

        var result = await DownloadAsync(stuck, Folder("beltrano"));

        Assert.Equal("beltrano", result.Folder!.Username);
        Assert.Equal(FolderFailureReason.Stalled, Assert.Single(result.Failures).Reason);
        Assert.True(_time.GetUtcNow() - started >= TimeSpan.FromMinutes(10));
        Assert.Equal(stuck.Files.Select(file => ("fulano", file.Filename)), _slskd.Cancelled);
    }

    [Fact]
    public async Task DownloadAsync_KeepsWaitingForASlowFolderWhileItMoves()
    {
        _slskd.Peer("fulano", (poll, _) => poll < 100 ? new("InProgress", poll) : new(Succeeded));
        var started = _time.GetUtcNow();

        var result = await DownloadAsync(Folder("fulano"), Folder("beltrano"));

        Assert.Equal("fulano", result.Folder!.Username);
        Assert.True(_time.GetUtcNow() - started > TimeSpan.FromMinutes(10));
        Assert.Empty(_slskd.Cancelled);
    }

    [Fact]
    public async Task DownloadAsync_FailsWhenNoFolderArrives()
    {
        _slskd.RefuseDownloadsFrom("fulano");
        _slskd.Peer("beltrano", (_, _) => new("Completed, Errored"));

        var result = await DownloadAsync(Folder("fulano"), Folder("beltrano"));

        Assert.Equal(AlbumDownloadOutcome.Failed, result.Outcome);
        Assert.Null(result.Folder);
        Assert.Equal(
            [("fulano", FolderFailureReason.Refused), ("beltrano", FolderFailureReason.TransferFailed)],
            result.Failures.Select(failure => (failure.Folder.Username, failure.Reason)));
    }

    [Fact]
    public async Task DownloadAsync_DoesNotAskAgainForFilesAlreadyComing()
    {
        var folder = Folder("fulano");
        _slskd.AlreadyKnows("fulano", folder.Files[0].Filename, folder.Files[0].Size);
        _slskd.AlreadyKnows("fulano", folder.Files[1].Filename, folder.Files[1].Size);

        var result = await DownloadAsync(folder);

        Assert.Equal(AlbumDownloadOutcome.Downloaded, result.Outcome);
        Assert.Equal([folder.Files[2].Filename], _slskd.Enqueued.Select(file => file.Filename));
    }

    [Fact]
    public async Task DownloadAsync_CancelsTheTransfersWhenTheDownloadIsCancelled()
    {
        var folder = Folder("fulano");
        _slskd.Peer("fulano", (_, file) => file.EndsWith("01.mp3") ? new(Succeeded) : new("InProgress", 10));
        using var cancellation = new CancellationTokenSource();
        var download = _download.DownloadAsync([folder], _progress, cancellation: cancellation.Token);

        while (_progress.All.Count == 0)
        {
            await Task.Delay(1, cancellation.Token);
        }

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => download);
        Assert.Equal([("fulano", folder.Files[1].Filename), ("fulano", folder.Files[2].Filename)], _slskd.Cancelled);
    }

    [Fact]
    public async Task DownloadAsync_MovesToTheNextFolderWhenVerificationRejectsIt()
    {
        var rejected = Folder("fulano");
        var next = Folder("beltrano");

        var result = await DownloadAsync(
            folder => Task.FromResult(folder.Username == "fulano" ? "fake-audio" : null),
            rejected,
            next);

        Assert.Equal(AlbumDownloadOutcome.Downloaded, result.Outcome);
        Assert.Same(next, result.Folder);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(
            (rejected, FolderFailureReason.Rejected, "fake-audio"),
            (failure.Folder, failure.Reason, failure.Detail));
    }

    [Fact]
    public async Task DownloadAsync_FailsWhenEveryFolderIsRejected()
    {
        var result = await DownloadAsync(
            _ => Task.FromResult<string?>("wrong-tracks"),
            Folder("fulano"),
            Folder("beltrano"));

        Assert.Equal(AlbumDownloadOutcome.Failed, result.Outcome);
        Assert.Equal(2, result.Failures.Count);
        Assert.All(result.Failures, failure =>
            Assert.Equal((FolderFailureReason.Rejected, "wrong-tracks"), (failure.Reason, failure.Detail)));
    }

    private Task<AlbumDownloadResult> DownloadAsync(params SoulseekFolder[] folders) =>
        DownloadAsync(null, folders);

    private async Task<AlbumDownloadResult> DownloadAsync(
        Func<SoulseekFolder, Task<string?>>? verify,
        params SoulseekFolder[] folders)
    {
        var download = _download.DownloadAsync(folders, _progress, verify);

        while (!download.IsCompleted)
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(1);
        }

        return await download;
    }

    private static SoulseekFolder Folder(string username) =>
        new(
            username,
            $@"@@{username}\Nirvana\Nevermind",
            [.. Enumerable.Range(1, 3).Select(track =>
                new SlskdFile($@"@@{username}\Nirvana\Nevermind\{track:00}.mp3", 1_000, 320, null, 200))],
            false);

    private sealed class Reports : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> All { get; } = [];

        public void Report(DownloadProgress value) => All.Add(value);
    }
}
