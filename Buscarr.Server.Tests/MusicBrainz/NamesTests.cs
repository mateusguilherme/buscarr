using Buscarr.Server.MusicBrainz;

namespace Buscarr.Server.Tests.MusicBrainz;

public class NamesTests
{
    [Fact]
    public void Normalize_TrimsAndLowercases() =>
        Assert.Equal("sigur rós", Names.Normalize("  Sigur Rós "));

    [Fact]
    public void Normalize_ReplacesTypographicHyphenAndApostrophe()
    {
        var name = $"blink{(char)0x2010}182 Guns N{(char)0x2019} Roses";

        Assert.Equal("blink-182 guns n' roses", Names.Normalize(name));
    }
}
