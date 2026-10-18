using Buscarr.Server.Slskd;

namespace Buscarr.Server.Tests.Slskd;

public class FoldersTests
{
    private const string Nevermind = @"@@musica\Nirvana\1991 - Nevermind";

    [Fact]
    public void Choose_KeepsAFolderWithTheEditionsTrackCountInMp3320()
    {
        var folders = Folders.Choose([Peer("fulano", Tracks(Nevermind, 12))], 12, Qualities.Mp3320);

        var folder = Assert.Single(folders);
        Assert.Equal(("fulano", Nevermind, 12), (folder.Username, folder.Directory, folder.Files.Count));
        Assert.False(folder.NeedsTranscode);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(13)]
    public void Choose_LeavesOutFoldersWithOtherTrackCounts(int tracks)
    {
        Assert.Empty(Folders.Choose([Peer("fulano", Tracks(Nevermind, tracks))], 12, Qualities.Mp3320));
    }

    [Theory]
    [InlineData("mp3", 192)]
    [InlineData("mp3", null)]
    [InlineData("m4a", 320)]
    [InlineData("ogg", null)]
    public void Choose_LeavesOutFoldersInALowerFormatThanMp3320(string extension, int? bitRate)
    {
        Assert.Empty(Folders.Choose([Peer("fulano", Tracks(Nevermind, 12, extension, bitRate))], 12, Qualities.Mp3320));
    }

    [Fact]
    public void Choose_LeavesOutAFolderWithOneTrackInAnotherBitRate()
    {
        var files = Tracks(Nevermind, 11).Append(Track($@"{Nevermind}\12 - Something in the Way.mp3", 256));

        Assert.Empty(Folders.Choose([Peer("fulano", files)], 12, Qualities.Mp3320));
    }

    [Fact]
    public void Choose_TakesFlacFoldersWhenTheQualityIsFlac()
    {
        var folders = Folders.Choose(
            [Peer("fulano", Tracks(Nevermind, 12)), Peer("beltrano", Tracks(Nevermind, 12, "flac", null))],
            12,
            Qualities.Flac);

        Assert.Equal([("beltrano", false)], folders.Select(folder => (folder.Username, folder.NeedsTranscode)));
    }

    [Theory]
    [InlineData("flac")]
    [InlineData("wav")]
    [InlineData("ape")]
    public void Choose_TakesALosslessFolderToTranscodeWhenNoneIsInMp3320(string extension)
    {
        var folders = Folders.Choose([Peer("fulano", Tracks(Nevermind, 12, extension, null))], 12, Qualities.Mp3320);

        var folder = Assert.Single(folders);
        Assert.Equal((Nevermind, true), (folder.Directory, folder.NeedsTranscode));
    }

    [Fact]
    public void Choose_PutsFoldersInTheChosenQualityBeforeTheOnesToTranscode()
    {
        var folders = Folders.Choose(
            [
                Peer("flac-rapido", Tracks(Nevermind, 12, "flac", null), speed: 9_000_000),
                Peer("mp3-lento", Tracks(Nevermind, 12), freeSlot: false, queue: 20, speed: 10_000),
            ],
            12,
            Qualities.Mp3320);

        Assert.Equal(
            [("mp3-lento", false), ("flac-rapido", true)],
            folders.Select(folder => (folder.Username, folder.NeedsTranscode)));
    }

    [Theory]
    [InlineData("wav")]
    [InlineData("aiff")]
    [InlineData("wv")]
    public void Choose_TakesAnotherLosslessFolderToConvertWhenTheQualityIsFlac(string extension)
    {
        var folders = Folders.Choose([Peer("fulano", Tracks(Nevermind, 12, extension, null))], 12, Qualities.Flac);

        Assert.True(Assert.Single(folders).NeedsTranscode);
    }

    [Fact]
    public void Choose_LeavesOutFoldersMixingLosslessAndLossyFiles()
    {
        var files = Tracks(Nevermind, 11, "flac", null).Append(Track($@"{Nevermind}
 - Something in the Way.mp3", 320)).ToList();

        Assert.Empty(Folders.Choose([Peer("fulano", files)], 12, Qualities.Mp3320));
        Assert.Empty(Folders.Choose([Peer("fulano", files)], 12, Qualities.Flac));
    }

    [Fact]
    public void Choose_CountsAndKeepsOnlyTheAudioFiles()
    {
        var files = Tracks(Nevermind, 12).Concat(
        [
            Track($@"{Nevermind}\cover.jpg", null),
            Track($@"{Nevermind}\Nevermind.cue", null),
            Track($@"{Nevermind}\Nevermind.log", null),
        ]);

        var folder = Assert.Single(Folders.Choose([Peer("fulano", files)], 12, Qualities.Mp3320));

        Assert.Equal(12, folder.Files.Count);
        Assert.All(folder.Files, file => Assert.EndsWith(".mp3", file.Filename));
    }

    [Fact]
    public void Choose_SeparatesEachFolderOfAPeer()
    {
        const string bleach = @"@@musica\Nirvana\1989 - Bleach";

        var folders = Folders.Choose(
            [Peer("fulano", Tracks(bleach, 13).Concat(Tracks(Nevermind, 12)))],
            12,
            Qualities.Mp3320);

        Assert.Equal([Nevermind], folders.Select(folder => folder.Directory));
    }

    [Fact]
    public void Choose_ReadsFoldersSeparatedBySlashes()
    {
        var files = Enumerable.Range(1, 12).Select(track => Track($"musica/Nirvana/Nevermind/{track:00}.mp3", 320));

        var folder = Assert.Single(Folders.Choose([Peer("fulano", files)], 12, Qualities.Mp3320));

        Assert.Equal("musica/Nirvana/Nevermind", folder.Directory);
    }

    [Fact]
    public void Choose_PutsPeersWithAFreeSlotFirstThenTheShortestQueueThenTheFastest()
    {
        var folders = Folders.Choose(
            [
                Peer("lento", Tracks(Nevermind, 12), speed: 100_000),
                Peer("ocupado", Tracks(Nevermind, 12), freeSlot: false),
                Peer("fila", Tracks(Nevermind, 12), queue: 3),
                Peer("rapido", Tracks(Nevermind, 12), speed: 5_000_000),
            ],
            12,
            Qualities.Mp3320);

        Assert.Equal(["rapido", "lento", "fila", "ocupado"], folders.Select(folder => folder.Username));
    }

    private static SlskdResponse Peer(
        string username,
        IEnumerable<SlskdFile> files,
        bool freeSlot = true,
        int queue = 0,
        int speed = 1_000_000) =>
        new(username, freeSlot, queue, speed, [.. files]);

    private static IEnumerable<SlskdFile> Tracks(string directory, int count, string extension = "mp3", int? bitRate = 320) =>
        Enumerable.Range(1, count).Select(track => Track($@"{directory}\{track:00} - Faixa {track}.{extension}", bitRate));

    private static SlskdFile Track(string filename, int? bitRate) =>
        new(filename, 8_000_000, bitRate, null, 200);
}
