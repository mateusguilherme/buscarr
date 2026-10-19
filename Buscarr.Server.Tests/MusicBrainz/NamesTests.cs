using Buscarr.Server.MusicBrainz;

namespace Buscarr.Server.Tests.MusicBrainz;

public class NamesTests
{
    [Fact]
    public void Normalize_TrimsAndLowercases() =>
        Assert.Equal("nirvana", Names.Normalize("  Nirvana "));

    [Fact]
    public void Normalize_StripsDiacritics()
    {
        Assert.Equal("motorhead", Names.Normalize("Motörhead"));
        Assert.Equal("sigur ros", Names.Normalize("  Sigur Rós "));
        Assert.Equal("beyonce", Names.Normalize("Beyoncé"));
    }

    [Fact]
    public void Normalize_ReplacesTypographicHyphenAndApostrophe()
    {
        var name = $"blink{(char)0x2010}182 Guns N{(char)0x2019} Roses";

        Assert.Equal("blink-182 guns n' roses", Names.Normalize(name));
    }
}
