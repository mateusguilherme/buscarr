using Buscarr.Server.MusicBrainz;

namespace Buscarr.Server.Tests.MusicBrainz;

public class EditionsTests
{
    [Fact]
    public void Choose_Original_TakesTheMostCommonTrackCountAmongCdAndDigitalEditions()
    {
        Release[] editions =
        [
            Edition("vinyl-1", "1991-09-24", 10, ["12\" Vinyl"]),
            Edition("vinyl-2", "1992-01-01", 10, ["12\" Vinyl"]),
            Edition("cassette", "1993-01-01", 10, ["Cassette"]),
            Edition("cd-1", "1991-09-24", 12, ["CD"]),
            Edition("cd-2", "1996-01-01", 12, ["CD"]),
            Edition("digital", "2011-09-26", 13, ["Digital Media"]),
        ];

        var chosen = Editions.Choose(editions, Editions.Original);

        Assert.Equal("cd-1", chosen.Id);
    }

    [Fact]
    public void Choose_Original_LeavesOutRemastersAndDeluxeEditions()
    {
        Release[] editions =
        [
            Edition("cd", "1991-09-24", 12, ["CD"]),
            Edition("remaster-1", "2011-09-26", 13, ["Digital Media"], "Remastered"),
            Edition("remaster-2", "2011-09-26", 13, ["Digital Media"], "Remastered"),
            Edition("deluxe", "2011-09-26", 40, ["Digital Media"], "deluxe edition"),
        ];

        var chosen = Editions.Choose(editions, Editions.Original);

        Assert.Equal("cd", chosen.Id);
    }

    [Fact]
    public void Choose_Original_BreaksATieWithTheEarliestEdition()
    {
        Release[] editions =
        [
            Edition("later", "1996-01-01", 13, ["CD"]),
            Edition("first", "1991-09-24", 12, ["CD"]),
        ];

        var chosen = Editions.Choose(editions, Editions.Original);

        Assert.Equal("first", chosen.Id);
    }

    [Fact]
    public void Choose_UsesEveryEditionWhenNoneIsCdOrDigital()
    {
        Release[] editions =
        [
            Edition("vinyl", "1969-09-26", 17, ["12\" Vinyl"]),
            Edition("cassette", "1970-01-01", 17, ["Cassette"]),
        ];

        var chosen = Editions.Choose(editions, Editions.Original);

        Assert.Equal("vinyl", chosen.Id);
    }

    [Fact]
    public void Choose_Remastered_TakesTheMostRecentRemaster()
    {
        Release[] editions =
        [
            Edition("cd", "1991-09-24", 12, ["CD"]),
            Edition("remaster-2004", "2004-01-01", 12, ["CD"], "remastered"),
            Edition("remaster-2011", "2011-09-26", 12, ["CD"], "2011 digital remaster"),
        ];

        var chosen = Editions.Choose(editions, Editions.Remastered);

        Assert.Equal("remaster-2011", chosen.Id);
    }

    [Fact]
    public void Choose_Deluxe_TakesTheMostCommonDeluxeEdition()
    {
        Release[] editions =
        [
            Edition("cd", "1991-09-24", 12, ["CD"]),
            Edition("deluxe-cd", "2011-09-26", 40, ["CD", "CD"], "deluxe edition"),
            Edition("deluxe-digital", "2011-09-27", 40, ["Digital Media"], "deluxe edition"),
            Edition("super-deluxe", "2011-09-26", 70, ["Digital Media"], "super deluxe"),
        ];

        var chosen = Editions.Choose(editions, Editions.Deluxe);

        Assert.Equal("deluxe-cd", chosen.Id);
    }

    [Theory]
    [InlineData(Editions.Remastered)]
    [InlineData(Editions.Deluxe)]
    public void Choose_FallsBackToTheOriginalWhenTheAlbumHasNoSuchEdition(string preference)
    {
        Release[] editions = [Edition("cd", "1991-09-24", 12, ["CD"]), Edition("cd-2", "1996-01-01", 12, ["CD"])];

        var chosen = Editions.Choose(editions, preference);

        Assert.Equal("cd", chosen.Id);
    }

    private static Release Edition(
        string id,
        string date,
        int tracks,
        string[] formats,
        string? disambiguation = null) =>
        new()
        {
            Id = id,
            Title = "Nevermind",
            Date = date,
            Disambiguation = disambiguation,
            Media = [.. formats.Select((format, index) => new Medium { Format = format, TrackCount = index == 0 ? tracks : 0 })],
        };
}
