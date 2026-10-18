using Buscarr.Server.Import;

namespace Buscarr.Server.Tests.Import;

public class FfmpegAudioToolsTests
{
    [Theory]
    [InlineData(16_000, "afftfilt=real='re*gte(b/nb*sr/2,16000)':imag='im*gte(b/nb*sr/2,16000)'")]
    [InlineData(19_000, "afftfilt=real='re*gte(b/nb*sr/2,19000)':imag='im*gte(b/nb*sr/2,19000)'")]
    [InlineData(20_000, "afftfilt=real='re*gte(b/nb*sr/2,20000)':imag='im*gte(b/nb*sr/2,20000)'")]
    public void BandFilter_ZeroesTheBinsBelowTheCutoff(int cutoff, string expected)
    {
        Assert.Equal(expected, FfmpegAudioTools.BandFilter(cutoff));
    }
}
