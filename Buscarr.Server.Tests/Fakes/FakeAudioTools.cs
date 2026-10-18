using Buscarr.Server.Import;

namespace Buscarr.Server.Tests.Fakes;

public sealed class FakeAudioTools : IAudioTools
{
    private readonly Dictionary<string, AudioProbe?> _probes = [];
    private readonly Dictionary<string, SpectralLevels?> _levels = [];

    public List<(string Source, string Target)> Transcoded { get; } = [];

    public bool TranscodeFails { get; set; }

    public void Probe(string path, AudioProbe? probe) => _probes[path] = probe;

    public void Levels(string path, SpectralLevels? levels) => _levels[path] = levels;

    public void GoodMp3(string path, double duration)
    {
        Probe(path, new AudioProbe("mp3", 320_000, 44_100, null, duration));
        Levels(path, new SpectralLevels(-10, -50, -65, -78));
    }

    public void GoodFlac(string path, double duration)
    {
        Probe(path, new AudioProbe("flac", 900_000, 44_100, 16, duration));
        Levels(path, new SpectralLevels(-10, -50, -65, -78));
    }

    public Task<AudioProbe?> ProbeAsync(string path) =>
        Task.FromResult(_probes.GetValueOrDefault(path));

    public Task<SpectralLevels?> LevelsAsync(string path) =>
        Task.FromResult(_levels.GetValueOrDefault(path));

    public Task<bool> TranscodeToMp3Async(string source, string target)
    {
        Transcoded.Add((source, target));

        if (TranscodeFails)
        {
            return Task.FromResult(false);
        }

        File.WriteAllBytes(target, [0xFF, 0xFB]);

        return Task.FromResult(true);
    }
}
