using Buscarr.Server.MusicBrainz;

namespace Buscarr.Server.Tests.MusicBrainz;

public class ReleaseTests
{
    [Theory]
    [InlineData("1991-09-24", "1991-09-24", true)]
    [InlineData("1989-06-15", "1989", true)]
    [InlineData("1991", "1991", false)]
    [InlineData("2011-09-26", "1991-09-24", false)]
    [InlineData("1991-09-24", "", false)]
    public void IsOriginalRelease_RequiresCompleteDateMatchingFirstRelease(
        string date,
        string firstReleaseDate,
        bool expected)
    {
        var release = CreateRelease(date, firstReleaseDate);

        Assert.Equal(expected, release.IsOriginalRelease);
    }

    [Theory]
    [InlineData((object)new string[0])]
    [InlineData((object)new[] { "Live" })]
    [InlineData((object)new[] { "Compilation" })]
    [InlineData((object)new[] { "Compilation", "Live" })]
    public void IsOriginalRelease_DoesNotDependOnSecondaryTypes(string[] secondaryTypes)
    {
        var release = CreateRelease("1994-11-01", "1994-11-01", secondaryTypes);

        Assert.True(release.IsOriginalRelease);
    }

    [Theory]
    [InlineData("Album", new string[0], "album")]
    [InlineData("EP", new string[0], "ep")]
    [InlineData("Single", new string[0], "single")]
    [InlineData("Broadcast", new string[0], null)]
    [InlineData(null, new string[0], null)]
    [InlineData("Album", new[] { "Live" }, "live")]
    [InlineData("Album", new[] { "Compilation", "Live" }, "compilation")]
    [InlineData("Album", new[] { "Soundtrack", "Remix" }, "soundtrack")]
    [InlineData("Album", new[] { "Remix" }, "remix")]
    [InlineData("Single", new[] { "Demo" }, "demo")]
    [InlineData("Album", new[] { "DJ-mix" }, "album")]
    [InlineData("Broadcast", new[] { "Live" }, "live")]
    public void Category_ComesFromTheSecondaryTypesAndThenThePrimaryType(
        string? primaryType,
        string[] secondaryTypes,
        string? expected)
    {
        var release = new Release
        {
            Title = "Nevermind",
            ReleaseGroup = new ReleaseGroup { PrimaryType = primaryType, SecondaryTypes = [.. secondaryTypes] },
        };

        Assert.Equal(expected, release.Category);
    }

    [Fact]
    public void Category_IsNullWithoutReleaseGroup()
    {
        var release = new Release { Title = "Nevermind" };

        Assert.Null(release.Category);
    }

    [Fact]
    public void IsOriginalRelease_IsFalseWithoutReleaseGroup()
    {
        var release = new Release { Title = "Nevermind", Date = "1991-09-24" };

        Assert.False(release.IsOriginalRelease);
    }

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { "Live" }, true)]
    [InlineData(new[] { "Compilation", "Live" }, true)]
    public void IsLive_DependsOnLiveSecondaryType(string[] secondaryTypes, bool expected)
    {
        var release = CreateRelease("1994-11-01", "1994-11-01", secondaryTypes);

        Assert.Equal(expected, release.IsLive);
    }

    [Theory]
    [InlineData("Nevermind", null, EditionKind.Original)]
    [InlineData("Nevermind", "BIEM/MCPS, Made in France by Cinram Optical Discs", EditionKind.Original)]
    [InlineData("Nevermind", "Remastered", EditionKind.Remastered)]
    [InlineData("Nevermind", "200 gram, remastered", EditionKind.Remastered)]
    [InlineData("Nevermind", "2011 digital remaster", EditionKind.Remastered)]
    [InlineData("Nevermind", "remasterizado", EditionKind.Remastered)]
    [InlineData("Nevermind (deluxe edition)", null, EditionKind.Deluxe)]
    [InlineData("Nevermind", "20th anniversary super deluxe edition", EditionKind.Deluxe)]
    [InlineData("Nevermind", "deluxe edition, remastered", EditionKind.Deluxe)]
    public void Kind_ComesFromTheTitleAndDescription(string title, string? disambiguation, EditionKind expected)
    {
        var release = new Release { Title = title, Disambiguation = disambiguation };

        Assert.Equal(expected, release.Kind);
    }

    [Theory]
    [InlineData(new[] { "CD" }, true)]
    [InlineData(new[] { "CD", "Enhanced CD" }, true)]
    [InlineData(new[] { "Digital Media" }, true)]
    [InlineData(new[] { "CD", "DVD" }, false)]
    [InlineData(new[] { "12\" Vinyl" }, false)]
    [InlineData(new string[0], false)]
    public void IsCdOrDigital_RequiresEveryMediumToBeACdOrDigital(string[] formats, bool expected)
    {
        var release = new Release { Title = "Nevermind", Media = [.. formats.Select(format => new Medium { Format = format })] };

        Assert.Equal(expected, release.IsCdOrDigital);
    }

    [Fact]
    public void TrackCount_AddsTheTracksOfEveryMedium()
    {
        var release = new Release
        {
            Title = "Nevermind",
            Media = [new Medium { Format = "CD", TrackCount = 12 }, new Medium { Format = "CD", TrackCount = 28 }],
        };

        Assert.Equal(40, release.TrackCount);
    }

    [Fact]
    public void CoverUrl_IsTheFrontOfTheReleaseWhenItHasOne()
    {
        var release = new Release
        {
            Id = "r1",
            Title = "Nevermind",
            CoverArtArchive = new CoverArtArchive { Front = true },
            ReleaseGroup = new ReleaseGroup { Id = "g1" },
        };

        Assert.Equal("https://coverartarchive.org/release/r1/front-250", release.CoverUrl);
    }

    [Fact]
    public void CoverUrl_IsTheFrontOfTheReleaseGroupWhenTheReleaseHasNone()
    {
        var release = new Release
        {
            Id = "r1",
            Title = "Nevermind",
            CoverArtArchive = new CoverArtArchive { Front = false },
            ReleaseGroup = new ReleaseGroup { Id = "g1" },
        };

        Assert.Equal("https://coverartarchive.org/release-group/g1/front-250", release.CoverUrl);
    }

    [Fact]
    public void CoverUrl_IsNullWithoutTheReleaseGroup()
    {
        var release = new Release { Id = "r1", Title = "Nevermind" };

        Assert.Null(release.CoverUrl);
    }

    [Theory]
    [InlineData("Album", "album")]
    [InlineData("EP", "ep")]
    [InlineData(null, null)]
    public void Type_IsThePrimaryTypeOfTheReleaseGroupInLowercase(string? primaryType, string? expected)
    {
        var release = new Release { Title = "Nevermind", ReleaseGroup = new ReleaseGroup { PrimaryType = primaryType } };

        Assert.Equal(expected, release.Type);
    }

    private static Release CreateRelease(string date, string firstReleaseDate, string[]? secondaryTypes = null) =>
        new()
        {
            Title = "Nevermind",
            Date = date,
            ReleaseGroup = new ReleaseGroup
            {
                FirstReleaseDate = firstReleaseDate,
                SecondaryTypes = [.. secondaryTypes ?? []],
            },
        };
}
