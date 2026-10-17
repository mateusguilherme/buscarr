namespace Buscarr.Server.Import;

public sealed record AudioProbe(string Codec, int? BitRate, int? SampleRate, int? BitDepth, double Duration);

public sealed record SpectralLevels(double Overall, double Above16k, double Above19k, double Above20k);

public interface AudioTools
{
    Task<AudioProbe?> ProbeAsync(string path);

    Task<SpectralLevels?> LevelsAsync(string path);

    Task<bool> TranscodeToMp3Async(string source, string target);
}
