using Buscarr.Server.MusicBrainz;

namespace Buscarr.Server.Tests.MusicBrainz;

public class DisambiguationsTests
{
    [Theory]
    [InlineData("Brazilian metalcore band", "Brazil", "Metalcore band")]
    [InlineData("Italian rapper Tarek Iurcich", "Italy", "Rapper Tarek Iurcich")]
    [InlineData("French electronic duo", "France", "Electronic duo")]
    [InlineData("90s US grunge band", "United States", "90s grunge band")]
    [InlineData("metalcore band from Brazil", "Brazil", "metalcore band")]
    [InlineData("Japanese pop singer", "Japan", "Pop singer")]
    [InlineData("German thrash metal band", "Germany", "Thrash metal band")]
    public void Clean_RemovesWordsRedundantWithArea(string disambiguation, string area, string expected) =>
        Assert.Equal(expected, Disambiguations.Clean(disambiguation, area));

    [Theory]
    [InlineData("Brazilian rock band", "São Paulo")]
    [InlineData("indie rock band", "India")]
    [InlineData("industrial metal band", "India")]
    [InlineData("Brazilian metalcore band", null)]
    public void Clean_KeepsTextWithoutRedundancy(string disambiguation, string? area) =>
        Assert.Equal(disambiguation, Disambiguations.Clean(disambiguation, area));

    [Fact]
    public void Clean_WhenOnlyTheDemonymRemains_ReturnsNull() =>
        Assert.Null(Disambiguations.Clean("Brazilian", "Brazil"));

    [Fact]
    public void Clean_WithoutDisambiguation_ReturnsNull() =>
        Assert.Null(Disambiguations.Clean(null, "Brazil"));
}
