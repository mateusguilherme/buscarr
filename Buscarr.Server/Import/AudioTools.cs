using JetBrains.Annotations;

namespace Buscarr.Server.Import;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record AudioProbe(string Codec, int? BitRate, int? SampleRate, int? BitDepth, double Duration);

public sealed record SpectralLevels(double Overall, double Above16K, double Above19K, double Above20K);

public interface IAudioTools
{
    Task<AudioProbe?> ProbeAsync(string path);

    Task<SpectralLevels?> LevelsAsync(string path);

    Task<bool> TranscodeToMp3Async(string source, string target);
}
