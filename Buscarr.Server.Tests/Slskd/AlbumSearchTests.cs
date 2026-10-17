using Buscarr.Server.Slskd;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;
using static Buscarr.Server.Tests.Fakes.SlskdJson;

namespace Buscarr.Server.Tests.Slskd;

public class AlbumSearchTests
{
    private const string Nevermind = @"@@musica\Nirvana\1991 - Nevermind";

    private readonly FakeSlskd _slskd = new();
    private readonly FakeTimeProvider _time = new();
    private readonly AlbumSearch _search;

    public AlbumSearchTests()
    {
        var client = new SlskdClient(new HttpClient(_slskd.Handler), "http://slskd.local:5030", "chave-de-teste");
        _search = new AlbumSearch(client, _time);
    }

    [Fact]
    public async Task SearchAsync_FindsTheFoldersThatFitOnceTheSearchEnds()
    {
        _slskd.States = ["InProgress", "InProgress", "Completed, TimedOut"];
        _slskd.Answer("Nirvana Nevermind", Response("fulano", AlbumFiles(Nevermind, 12)));

        var result = await SearchNevermindAsync();

        Assert.Equal(AlbumSearchOutcome.Found, result.Outcome);
        Assert.Equal("Nirvana Nevermind", result.SearchText);
        var folder = Assert.Single(result.Folders);
        Assert.Equal(("fulano", Nevermind, 12), (folder.Username, folder.Directory, folder.Files.Count));
        Assert.Equal(3, Polls());
        Assert.EndsWith("/responses", _slskd.Handler.Requests[^1].Uri.AbsolutePath);
    }

    [Fact]
    public async Task SearchAsync_RetriesOnceWithWildcardsWhenNobodyAnswers()
    {
        _slskd.Answer("*irvana *evermind", Response("fulano", AlbumFiles(Nevermind, 12)));

        var result = await SearchNevermindAsync();

        Assert.Equal(AlbumSearchOutcome.Found, result.Outcome);
        Assert.Equal("*irvana *evermind", result.SearchText);
        Assert.Equal(["Nirvana Nevermind", "*irvana *evermind"], _slskd.SearchTexts);
    }

    [Fact]
    public async Task SearchAsync_SaysNothingWasFoundWhenTheWildcardSearchFindsNothingEither()
    {
        var result = await SearchNevermindAsync();

        Assert.Equal(AlbumSearchOutcome.NothingFound, result.Outcome);
        Assert.Empty(result.Folders);
        Assert.Equal(2, _slskd.SearchTexts.Count);
    }

    [Fact]
    public async Task SearchAsync_SaysNoFolderFitsWithoutRetryingWhenPeersAnswer()
    {
        _slskd.Answer("Nirvana Nevermind", Response("fulano", AlbumFiles(Nevermind, 12, bitRate: 192)));

        var result = await SearchNevermindAsync();

        Assert.Equal(AlbumSearchOutcome.NoMatchingFolder, result.Outcome);
        Assert.Empty(result.Folders);
        Assert.Equal(["Nirvana Nevermind"], _slskd.SearchTexts);
    }

    [Fact]
    public async Task SearchAsync_FindsALosslessFolderToTranscodeWithoutRetryingWhenNoneIsInTheChosenQuality()
    {
        _slskd.Answer("Nirvana Nevermind", Response("fulano", AlbumFiles(Nevermind, 12, bitRate: null, extension: "flac")));

        var result = await SearchNevermindAsync();

        Assert.Equal(AlbumSearchOutcome.Found, result.Outcome);
        Assert.True(Assert.Single(result.Folders).NeedsTranscode);
        Assert.Equal(["Nirvana Nevermind"], _slskd.SearchTexts);
    }

    [Fact]
    public async Task SearchAsync_StartsAgainWhenSlskdIsBusyWithAnotherSearch()
    {
        _slskd.BusyFor(2);
        _slskd.Answer("Nirvana Nevermind", Response("fulano", AlbumFiles(Nevermind, 12)));

        var result = await SearchNevermindAsync();

        Assert.Equal(AlbumSearchOutcome.Found, result.Outcome);
        Assert.Equal(3, Starts());
    }

    [Fact]
    public async Task SearchAsync_SaysSlskdIsBusyWhenItStaysBusy()
    {
        _slskd.BusyFor(3);

        var result = await SearchNevermindAsync();

        Assert.Equal(AlbumSearchOutcome.Busy, result.Outcome);
        Assert.Equal(3, Starts());
        Assert.Empty(_slskd.SearchTexts);
    }

    [Fact]
    public async Task SearchAsync_SaysTheSearchIsUnfinishedWhenItDoesNotEndInTwoMinutes()
    {
        _slskd.States = ["InProgress"];
        var started = _time.GetUtcNow();

        var result = await SearchNevermindAsync();

        Assert.Equal(AlbumSearchOutcome.Unfinished, result.Outcome);
        Assert.True(_time.GetUtcNow() - started >= TimeSpan.FromMinutes(2));
        Assert.DoesNotContain(_slskd.Handler.Requests, request => request.Uri.AbsolutePath.EndsWith("/responses"));
        Assert.Single(_slskd.SearchTexts);
    }

    [Fact]
    public async Task SearchAsync_SaysTheSearchIsUnfinishedWhenSlskdFailsIt()
    {
        _slskd.States = ["Completed, Errored"];

        var result = await SearchNevermindAsync();

        Assert.Equal(AlbumSearchOutcome.Unfinished, result.Outcome);
        Assert.DoesNotContain(_slskd.Handler.Requests, request => request.Uri.AbsolutePath.EndsWith("/responses"));
    }

    private async Task<AlbumSearchResult> SearchNevermindAsync()
    {
        var search = _search.SearchAsync("Nirvana", "Nevermind", 12, Qualities.Mp3320);

        while (!search.IsCompleted)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(1);
        }

        return await search;
    }

    private int Starts() =>
        _slskd.Handler.Requests.Count(request => request.Method == HttpMethod.Post);

    private int Polls() =>
        _slskd.Handler.Requests.Count(request =>
            request.Method == HttpMethod.Get && !request.Uri.AbsolutePath.EndsWith("/responses"));
}
