using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Buscarr.Server.Import;

public sealed partial class FfmpegAudioTools : AudioTools
{
    private static readonly TimeSpan ProbeLimit = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan AnalysisLimit = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TranscodeLimit = TimeSpan.FromMinutes(5);

    public async Task<AudioProbe?> ProbeAsync(string path)
    {
        var (exitCode, output, _, _) = await ProcessRunner.RunAsync(
            "ffprobe",
            ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", path],
            ProbeLimit);

        if (exitCode != 0)
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(output);
            var stream = json.RootElement.GetProperty("streams").EnumerateArray()
                .FirstOrDefault(s => s.TryGetProperty("codec_type", out var type) && type.GetString() == "audio");

            if (stream.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var format = json.RootElement.GetProperty("format");

            return new AudioProbe(
                stream.GetProperty("codec_name").GetString() ?? "",
                Number(stream, "bit_rate") ?? Number(format, "bit_rate"),
                Number(stream, "sample_rate"),
                Number(stream, "bits_per_raw_sample") ?? Number(stream, "bits_per_sample"),
                double.Parse(format.GetProperty("duration").GetString()!, CultureInfo.InvariantCulture));
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or FormatException)
        {
            return null;
        }
    }

    public async Task<SpectralLevels?> LevelsAsync(string path)
    {
        var overall = await RmsAsync(path, null);
        var above16k = await RmsAsync(path, 16_000);
        var above19k = await RmsAsync(path, 19_000);
        var above20k = await RmsAsync(path, 20_000);

        if (overall is null || above16k is null || above19k is null || above20k is null)
        {
            return null;
        }

        return new SpectralLevels(overall.Value, above16k.Value, above19k.Value, above20k.Value);
    }

    public async Task<bool> TranscodeToMp3Async(string source, string target)
    {
        var (exitCode, _, _, _) = await ProcessRunner.RunAsync(
            "ffmpeg",
            ["-v", "error", "-y", "-i", source, "-codec:a", "libmp3lame", "-b:a", "320k", target],
            TranscodeLimit);

        return exitCode == 0;
    }

    private static async Task<double?> RmsAsync(string path, int? highpass)
    {
        var filter = highpass is { } cutoff
            ? $"highpass=f={cutoff},astats=measure_perchannel=none:measure_overall=RMS_level"
            : "astats=measure_perchannel=none:measure_overall=RMS_level";
        var (exitCode, _, log, _) = await ProcessRunner.RunAsync(
            "ffmpeg",
            ["-nostats", "-i", path, "-af", filter, "-f", "null", "-"],
            AnalysisLimit);

        if (exitCode != 0)
        {
            return null;
        }

        var match = RmsLine().Matches(log).LastOrDefault();

        if (match is null)
        {
            return null;
        }

        return match.Groups[1].Value == "-inf"
            ? double.NegativeInfinity
            : double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static int? Number(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : int.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    [GeneratedRegex(@"RMS level dB:\s*(-?[\d.]+|-inf)")]
    private static partial Regex RmsLine();
}
