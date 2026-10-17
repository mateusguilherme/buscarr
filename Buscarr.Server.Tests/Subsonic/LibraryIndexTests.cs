using Buscarr.Server.Subsonic;

namespace Buscarr.Server.Tests.Subsonic;

public class LibraryIndexTests
{
    [Fact]
    public void TrackCount_FindsAnAlbumByAnyOfItsMusicBrainzIds()
    {
        var index = new LibraryIndex([Album("Nevermind", "Nirvana", 12, "r1")]);

        Assert.Equal(12, index.TrackCount(["g1", "r1"], "Nevermind", "Nirvana"));
        Assert.Null(index.TrackCount(["g2", "r9"], "In Utero", "Nirvana"));
    }

    [Fact]
    public void TrackCount_IgnoresTheCaseOfTheIds()
    {
        var index = new LibraryIndex([Album("Nevermind", "Nirvana", 12, "A1B2")]);

        Assert.Equal(12, index.TrackCount(["a1b2"], "Nevermind", "Nirvana"));
    }

    [Fact]
    public void TrackCount_TakesTheFullestCopyWhenAnAlbumRepeats()
    {
        var index = new LibraryIndex(
        [
            Album("Nevermind", "Nirvana", 12, "r1"),
            Album("Nevermind (Deluxe)", "Nirvana", 40, "r1"),
        ]);

        Assert.Equal(40, index.TrackCount(["r1"], "Nevermind", "Nirvana"));
    }

    [Fact]
    public void TrackCount_FallsBackToNameAndArtistOnlyForAlbumsWithoutATag()
    {
        var index = new LibraryIndex(
        [
            Album("Respiro", "ConeCrewDiretoria", 13),
            Album("Nevermind", "Nirvana", 12, "r1"),
        ]);

        Assert.Equal(13, index.TrackCount(["g9"], "Respiro", "ConeCrewDiretoria"));
        Assert.Null(index.TrackCount(["g9"], "Nevermind", "Nirvana"));
    }

    [Fact]
    public void TrackCount_NeverMatchesALooseTitle()
    {
        var index = new LibraryIndex(
        [
            Album("Sessão Respiro", "ConeCrewDiretoria", 5),
            Album("Mammoth", "Mammoth WVH", 14),
        ]);

        Assert.Null(index.TrackCount(["g1"], "Respiro", "ConeCrewDiretoria"));
        Assert.Null(index.TrackCount(["g2"], "Mammoth", "Mammoth Mammoth"));
    }

    [Fact]
    public void TrackCount_NormalizesTheNameAndTheArtist()
    {
        var index = new LibraryIndex([Album("The Sand’s Song", "Mother’s Cake", 10)]);

        Assert.Equal(10, index.TrackCount([], "the sand's song", "mother's cake"));
    }

    private static SubsonicAlbum Album(string name, string artist, int songCount, string? musicBrainzId = null) =>
        new("al-1", name, artist, songCount, musicBrainzId);
}
