using Buscarr.Server.Import;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Import;

public class AlbumVerifierTests
{
    private readonly FakeAudioTools _tools = new();
    private readonly AlbumVerifier _verifier;

    public AlbumVerifierTests()
    {
        _verifier = new AlbumVerifier(_tools);
    }

    [Fact]
    public async Task Verify_ApprovesAFolderMatchingTheTracklist()
    {
        _tools.GoodMp3("01 Smells Like Teen Spirit.mp3", 301);
        _tools.GoodMp3("02 In Bloom.mp3", 255);

        var reason = await _verifier.VerifyAsync(
            ["01 Smells Like Teen Spirit.mp3", "02 In Bloom.mp3"],
            [Track(1, 301_000), Track(2, 255_000)],
            Qualities.Mp3320,
            false);

        Assert.Null(reason);
    }

    [Fact]
    public async Task Verify_RejectsAFolderWithTheWrongFileCount()
    {
        _tools.GoodMp3("01.mp3", 301);

        var reason = await _verifier.VerifyAsync(
            ["01.mp3"],
            [Track(1, 301_000), Track(2, 255_000)],
            Qualities.Mp3320,
            false);

        Assert.Equal("wrong-tracks", reason);
    }

    [Fact]
    public async Task Verify_RejectsAFileWhoseDurationDoesNotMatchTheTrack()
    {
        _tools.GoodMp3("01.mp3", 301);
        _tools.GoodMp3("02.mp3", 180);

        var reason = await _verifier.VerifyAsync(
            ["01.mp3", "02.mp3"],
            [Track(1, 301_000), Track(2, 255_000)],
            Qualities.Mp3320,
            false);

        Assert.Equal("wrong-tracks", reason);
    }

    [Fact]
    public async Task Verify_AcceptsADurationTheFallbackConfirms()
    {
        _tools.GoodMp3("01.mp3", 301);
        _tools.GoodMp3("02.mp3", 280);

        var reason = await _verifier.VerifyAsync(
            ["01.mp3", "02.mp3"],
            [Track(1, 301_000), Track(2, 319_000)],
            Qualities.Mp3320,
            false,
            () => Task.FromResult<IReadOnlyList<int>?>([301, 280]));

        Assert.Null(reason);
    }

    [Fact]
    public async Task Verify_RejectsADurationTheFallbackDoesNotConfirm()
    {
        _tools.GoodMp3("01.mp3", 301);
        _tools.GoodMp3("02.mp3", 280);

        var reason = await _verifier.VerifyAsync(
            ["01.mp3", "02.mp3"],
            [Track(1, 301_000), Track(2, 319_000)],
            Qualities.Mp3320,
            false,
            () => Task.FromResult<IReadOnlyList<int>?>([301, 200]));

        Assert.Equal("wrong-tracks", reason);
    }

    [Fact]
    public async Task Verify_RejectsADurationMismatchWhenThereIsNoFallback()
    {
        _tools.GoodMp3("01.mp3", 301);
        _tools.GoodMp3("02.mp3", 280);

        var reason = await _verifier.VerifyAsync(
            ["01.mp3", "02.mp3"],
            [Track(1, 301_000), Track(2, 319_000)],
            Qualities.Mp3320,
            false,
            () => Task.FromResult<IReadOnlyList<int>?>(null));

        Assert.Equal("wrong-tracks", reason);
    }

    [Fact]
    public async Task Verify_RejectsADurationMismatchWhenTheFallbackHasAnotherTrackCount()
    {
        _tools.GoodMp3("01.mp3", 301);
        _tools.GoodMp3("02.mp3", 280);

        var reason = await _verifier.VerifyAsync(
            ["01.mp3", "02.mp3"],
            [Track(1, 301_000), Track(2, 319_000)],
            Qualities.Mp3320,
            false,
            () => Task.FromResult<IReadOnlyList<int>?>([280]));

        Assert.Equal("wrong-tracks", reason);
    }

    [Fact]
    public async Task Verify_FetchesTheFallbackOnce()
    {
        _tools.GoodMp3("01.mp3", 295);
        _tools.GoodMp3("02.mp3", 280);
        var fetches = 0;

        var reason = await _verifier.VerifyAsync(
            ["01.mp3", "02.mp3"],
            [Track(1, 301_000), Track(2, 319_000)],
            Qualities.Mp3320,
            false,
            () =>
            {
                fetches++;

                return Task.FromResult<IReadOnlyList<int>?>([295, 280]);
            });

        Assert.Null(reason);
        Assert.Equal(1, fetches);
    }

    [Fact]
    public async Task Verify_MatchesFilesToTracksInNaturalNameOrder()
    {
        _tools.GoodMp3("10 Something in the Way.mp3", 222);
        _tools.GoodMp3("2 In Bloom.mp3", 255);

        var reason = await _verifier.VerifyAsync(
            ["10 Something in the Way.mp3", "2 In Bloom.mp3"],
            [Track(1, 255_000), Track(2, 222_000)],
            Qualities.Mp3320,
            false);

        Assert.Null(reason);
    }

    [Fact]
    public async Task Verify_SkipsTheDurationCheckWhenTheTrackHasNoLength()
    {
        _tools.GoodMp3("01.mp3", 301);

        var reason = await _verifier.VerifyAsync(
            ["01.mp3"],
            [Track(1, null)],
            Qualities.Mp3320,
            false);

        Assert.Null(reason);
    }

    [Fact]
    public async Task Verify_RejectsAnUnreadableFile()
    {
        _tools.GoodMp3("01.mp3", 301);
        _tools.Probe("02.mp3", null);

        var reason = await _verifier.VerifyAsync(
            ["01.mp3", "02.mp3"],
            [Track(1, 301_000), Track(2, 255_000)],
            Qualities.Mp3320,
            false);

        Assert.Equal("bad-audio", reason);
    }

    [Fact]
    public async Task Verify_RejectsAnMp3WhoseRealBitRateIsLow()
    {
        _tools.Probe("01.mp3", new AudioProbe("mp3", 128_000, 44_100, null, 301));
        _tools.Levels("01.mp3", new SpectralLevels(-20, -45, -60, -70));

        var reason = await _verifier.VerifyAsync(
            ["01.mp3"],
            [Track(1, 301_000)],
            Qualities.Mp3320,
            false);

        Assert.Equal("bad-audio", reason);
    }

    [Fact]
    public async Task Verify_RejectsAFlacClaimWithAnotherCodec()
    {
        _tools.Probe("01.flac", new AudioProbe("mp3", 320_000, 44_100, null, 301));
        _tools.Levels("01.flac", new SpectralLevels(-20, -45, -60, -70));

        var reason = await _verifier.VerifyAsync(
            ["01.flac"],
            [Track(1, 301_000)],
            Qualities.Flac,
            false);

        Assert.Equal("bad-audio", reason);
    }

    [Fact]
    public async Task Verify_AcceptsAnyLosslessCodecWhenTheFolderWillBeTranscoded()
    {
        _tools.Probe("01.wav", new AudioProbe("pcm_s16le", null, 44_100, 16, 301));
        _tools.Levels("01.wav", new SpectralLevels(-20, -45, -60, -70));

        var reason = await _verifier.VerifyAsync(
            ["01.wav"],
            [Track(1, 301_000)],
            Qualities.Mp3320,
            true);

        Assert.Null(reason);
    }

    [Fact]
    public async Task Verify_RejectsALossyCodecWhenTheFolderWillBeTranscoded()
    {
        _tools.Probe("01.flac", new AudioProbe("mp3", 320_000, 44_100, null, 301));
        _tools.Levels("01.flac", new SpectralLevels(-20, -45, -60, -70));

        var reason = await _verifier.VerifyAsync(
            ["01.flac"],
            [Track(1, 301_000)],
            Qualities.Mp3320,
            true);

        Assert.Equal("bad-audio", reason);
    }

    [Fact]
    public async Task Verify_RejectsAudioCutBelow19kHz()
    {
        _tools.Probe("01.mp3", new AudioProbe("mp3", 320_000, 44_100, null, 301));
        _tools.Levels("01.mp3", new SpectralLevels(-10, -100, -112, -118));

        var reason = await _verifier.VerifyAsync(
            ["01.mp3"],
            [Track(1, 301_000)],
            Qualities.Mp3320,
            false);

        Assert.Equal("fake-audio", reason);
    }

    [Fact]
    public async Task Verify_RejectsASteepCutBetween19And20kHz()
    {
        _tools.Probe("01.flac", new AudioProbe("flac", 900_000, 44_100, 16, 301));
        _tools.Levels("01.flac", new SpectralLevels(-10, -40, -62, -108));

        var reason = await _verifier.VerifyAsync(
            ["01.flac"],
            [Track(1, 301_000)],
            Qualities.Flac,
            false);

        Assert.Equal("fake-audio", reason);
    }

    [Fact]
    public async Task Verify_AcceptsAGentleRolloffBetween19And20kHz()
    {
        _tools.Probe("01.flac", new AudioProbe("flac", 900_000, 44_100, 16, 301));
        _tools.Levels("01.flac", new SpectralLevels(-10, -55, -70, -108));

        var reason = await _verifier.VerifyAsync(
            ["01.flac"],
            [Track(1, 301_000)],
            Qualities.Flac,
            false);

        Assert.Null(reason);
    }

    [Fact]
    public async Task Verify_AcceptsAFaint19kHzBand()
    {
        _tools.Probe("01.mp3", new AudioProbe("mp3", 320_000, 44_100, null, 301));
        _tools.Levels("01.mp3", new SpectralLevels(-10, -60, -78, -100));

        var reason = await _verifier.VerifyAsync(
            ["01.mp3"],
            [Track(1, 301_000)],
            Qualities.Mp3320,
            false);

        Assert.Null(reason);
    }

    [Fact]
    public async Task Verify_AcceptsAFileWhoseSpectrumCannotBeMeasured()
    {
        _tools.Probe("01.mp3", new AudioProbe("mp3", 320_000, 44_100, null, 301));
        _tools.Levels("01.mp3", null);

        var reason = await _verifier.VerifyAsync(
            ["01.mp3"],
            [Track(1, 301_000)],
            Qualities.Mp3320,
            false);

        Assert.Null(reason);
    }

    private static ReleaseTrack Track(int position, int? lengthMs) =>
        new(position, $"Faixa {position}", lengthMs, $"rec{position}");
}
