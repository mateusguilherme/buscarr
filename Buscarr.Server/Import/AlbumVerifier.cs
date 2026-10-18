using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;

namespace Buscarr.Server.Import;

public sealed class AlbumVerifier(IAudioTools tools)
{
    private const int Mp3MinBitRate = 315_000;
    private const double DurationTolerance = 5;
    private const double PresenceDb = -85;
    private const double SteepDropDb = 20;

    private static readonly string[] LosslessCodecs = ["flac", "alac", "ape", "wavpack"];

    public async Task<string?> VerifyAsync(
        IReadOnlyList<string> files,
        IReadOnlyList<ReleaseTrack> tracks,
        string quality,
        bool needsTranscode,
        Func<Task<IReadOnlyList<int>?>>? fallbackDurations = null)
    {
        if (files.Count != tracks.Count)
        {
            return "wrong-tracks";
        }

        var sorted = files.OrderBy(Path.GetFileName, NaturalOrder.Instance).ToList();
        IReadOnlyList<int>? fallback = null;
        var fetched = false;

        for (var i = 0; i < sorted.Count; i++)
        {
            var probe = await tools.ProbeAsync(sorted[i]);

            if (probe is null)
            {
                return "bad-audio";
            }

            if (tracks[i].LengthMs is { } length
                && Math.Abs(probe.Duration - length / 1000.0) > DurationTolerance)
            {
                if (!fetched)
                {
                    fallback = fallbackDurations is null ? null : await fallbackDurations();
                    fetched = true;
                }

                if (fallback is null
                    || fallback.Count != tracks.Count
                    || Math.Abs(probe.Duration - fallback[i]) > DurationTolerance)
                {
                    return "wrong-tracks";
                }
            }

            if (!FitsFormat(probe, quality, needsTranscode))
            {
                return "bad-audio";
            }

            if (await IsFakeAsync(sorted[i]))
            {
                return "fake-audio";
            }
        }

        return null;
    }

    private async Task<bool> IsFakeAsync(string path)
    {
        var levels = await tools.LevelsAsync(path);

        if (levels is null)
        {
            return false;
        }

        if (IsPresent(levels, levels.Above20K))
        {
            return false;
        }

        if (IsPresent(levels, levels.Above19K))
        {
            return levels.Above16K - levels.Above19K >= SteepDropDb;
        }

        return true;
    }

    private static bool IsPresent(SpectralLevels levels, double band) =>
        band - levels.Overall >= PresenceDb;

    private static bool FitsFormat(AudioProbe probe, string quality, bool needsTranscode)
    {
        if (needsTranscode)
        {
            return IsLossless(probe.Codec);
        }

        return quality switch
        {
            Qualities.Mp3320 => probe is { Codec: "mp3", BitRate: >= Mp3MinBitRate },
            Qualities.Flac => probe.Codec == "flac",
            _ => false,
        };
    }

    private static bool IsLossless(string codec) =>
        LosslessCodecs.Contains(codec) || codec.StartsWith("pcm_", StringComparison.Ordinal);
}
