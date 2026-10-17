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
    [InlineData(new string[0], true)]
    [InlineData(new[] { "Live" }, true)]
    [InlineData(new[] { "Compilation" }, false)]
    [InlineData(new[] { "Compilation", "Live" }, false)]
    public void IsOriginalRelease_AcceptsOnlyStudioAndLiveAlbums(string[] secondaryTypes, bool expected)
    {
        var release = CreateRelease("1994-11-01", "1994-11-01", secondaryTypes);

        Assert.Equal(expected, release.IsOriginalRelease);
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
