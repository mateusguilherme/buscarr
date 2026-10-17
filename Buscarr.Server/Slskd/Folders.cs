namespace Buscarr.Server.Slskd;

public record SoulseekFolder(string Username, string Directory, List<SlskdFile> Files, bool NeedsTranscode);

public static class Folders
{
    private static readonly string[] LosslessExtensions = [".flac", ".wav", ".aiff", ".aif", ".ape", ".wv"];

    private static readonly string[] AudioExtensions =
        [.. LosslessExtensions, ".mp3", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".alac"];

    public static List<SoulseekFolder> Choose(IEnumerable<SlskdResponse> responses, int trackCount, string quality)
    {
        var folders = responses
            .OrderByDescending(response => response.HasFreeUploadSlot)
            .ThenBy(response => response.QueueLength)
            .ThenByDescending(response => response.UploadSpeed)
            .SelectMany(response => response.Files
                .Where(file => AudioExtensions.Contains(Extension(file.Filename)))
                .GroupBy(file => DirectoryOf(file.Filename))
                .Select(folder => new SoulseekFolder(response.Username, folder.Key, [.. folder], false)))
            .Where(folder => folder.Files.Count == trackCount)
            .ToList();
        var inQuality = folders.Where(folder => folder.Files.All(file => IsIn(file, quality)));
        var toTranscode = folders
            .Where(folder => !folder.Files.All(file => IsIn(file, quality)) && folder.Files.All(IsLossless))
            .Select(folder => folder with { NeedsTranscode = true });

        return [.. inQuality, .. toTranscode];
    }

    public static bool IsAudioFile(string path) => AudioExtensions.Contains(Extension(path));

    public static bool IsLosslessFile(string path) => LosslessExtensions.Contains(Extension(path));

    public static bool MatchesQuality(string path, string quality) =>
        quality switch
        {
            Qualities.Mp3320 => Extension(path) == ".mp3",
            Qualities.Flac => Extension(path) == ".flac",
            _ => false,
        };

    private static bool IsIn(SlskdFile file, string quality) =>
        quality switch
        {
            Qualities.Mp3320 => Extension(file.Filename) == ".mp3" && file.BitRate == 320,
            Qualities.Flac => Extension(file.Filename) == ".flac",
            _ => false,
        };

    private static bool IsLossless(SlskdFile file) =>
        LosslessExtensions.Contains(Extension(file.Filename));

    private static string DirectoryOf(string filename) =>
        filename[..Math.Max(filename.LastIndexOfAny(['\\', '/']), 0)];

    private static string Extension(string filename) =>
        Path.GetExtension(filename[(filename.LastIndexOfAny(['\\', '/']) + 1)..]).ToLowerInvariant();
}
