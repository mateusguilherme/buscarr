using Buscarr.Server.Import;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Import;

public sealed class TranscoderTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"buscarr-transcoder-{Guid.NewGuid()}");

    private readonly FakeAudioTools _tools = new();
    private readonly Transcoder _transcoder;

    public TranscoderTests()
    {
        Directory.CreateDirectory(_directory);
        _transcoder = new Transcoder(_tools);
    }

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public async Task ToMp3Async_TranscodesEachFileAndRemovesTheSource()
    {
        var first = Source("01 Faixa.flac");
        var second = Source("02 Faixa.flac");

        var files = await _transcoder.ToMp3Async([first, second]);

        Assert.Equal(
            [Path.Combine(_directory, "01 Faixa.mp3"), Path.Combine(_directory, "02 Faixa.mp3")],
            files!);
        Assert.All(files!, file => Assert.True(File.Exists(file)));
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
    }

    [Fact]
    public async Task ToMp3Async_KeepsTheSourcesWhenATranscodeFails()
    {
        var source = Source("01 Faixa.flac");
        _tools.TranscodeFails = true;

        var files = await _transcoder.ToMp3Async([source]);

        Assert.Null(files);
        Assert.True(File.Exists(source));
    }

    private string Source(string name)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, [1, 2, 3]);

        return path;
    }
}
