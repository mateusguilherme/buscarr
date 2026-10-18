namespace Buscarr.Server.Slskd;

public record SoulseekFolder(
    string Username,
    string Directory,
    List<SlskdFile> Files,
    bool NeedsTranscode,
    string? Quality = null);

public record FolderCandidate(
    SoulseekFolder Folder,
    string? Rejection,
    bool HasFreeUploadSlot,
    int QueueLength,
    int UploadSpeed);

public static class Folders
{
    private static readonly string[] LosslessExtensions = [".flac", ".wav", ".aiff", ".aif", ".ape", ".wv"];

    private static readonly string[] AudioExtensions =
        [.. LosslessExtensions, ".mp3", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".alac"];

    public static List<SoulseekFolder> Choose(
        IEnumerable<SlskdResponse> responses,
        int trackCount,
        string quality,
        string? fallbackQuality = null) =>
        [.. Candidates(responses, trackCount, quality, fallbackQuality)
            .Where(candidate => candidate.Rejection is null)
            .Select(candidate => candidate.Folder)];

    public static List<FolderCandidate> Candidates(
        IEnumerable<SlskdResponse> responses,
        int trackCount,
        string quality,
        string? fallbackQuality = null) =>
        [.. responses
            .OrderByDescending(response => response.HasFreeUploadSlot)
            .ThenBy(response => response.QueueLength)
            .ThenByDescending(response => response.UploadSpeed)
            .SelectMany(response => response.Files
                .Where(file => AudioExtensions.Contains(Extension(file.Filename)))
                .GroupBy(file => DirectoryOf(file.Filename))
                .Select(folder => Candidate(response, folder.Key, [.. folder], trackCount, quality, fallbackQuality)))
            .OrderBy(candidate => Rank(candidate, quality))];

    public static SoulseekFolder Pin(SoulseekFolder folder, string quality)
    {
        if (folder.Files.All(file => IsIn(file, quality)))
        {
            return folder with { NeedsTranscode = false, Quality = quality };
        }

        if (folder.Files.All(IsLossless))
        {
            return folder with { NeedsTranscode = true, Quality = quality };
        }

        var other = Qualities.Preferences
            .Where(preference => preference != quality)
            .FirstOrDefault(preference => folder.Files.All(file => IsIn(file, preference)));

        return folder with { NeedsTranscode = false, Quality = other };
    }

    public static string Format(IEnumerable<SlskdFile> files) =>
        string.Join("+", files
            .Select(file => Extension(file.Filename).TrimStart('.'))
            .Distinct()
            .Order());

    public static bool IsAudioFile(string path) => AudioExtensions.Contains(Extension(path));

    public static bool IsLosslessFile(string path) => LosslessExtensions.Contains(Extension(path));

    public static bool MatchesQuality(string path, string quality) =>
        quality switch
        {
            Qualities.Mp3320 => Extension(path) == ".mp3",
            Qualities.Flac => Extension(path) == ".flac",
            _ => false,
        };

    private static FolderCandidate Candidate(
        SlskdResponse response,
        string directory,
        List<SlskdFile> files,
        int trackCount,
        string quality,
        string? fallbackQuality)
    {
        var folder = new SoulseekFolder(response.Username, directory, files, false);
        string? rejection = null;

        if (files.Count != trackCount)
        {
            rejection = "track-count";
        }
        else if (files.All(file => IsIn(file, quality)))
        {
            folder = folder with { Quality = quality };
        }
        else if (files.All(IsLossless))
        {
            folder = folder with { NeedsTranscode = true, Quality = quality };
        }
        else if (fallbackQuality is not null && files.All(file => IsIn(file, fallbackQuality)))
        {
            folder = folder with { Quality = fallbackQuality };
        }
        else
        {
            rejection = "quality";
        }

        return new FolderCandidate(folder, rejection, response.HasFreeUploadSlot, response.QueueLength, response.UploadSpeed);
    }

    private static int Rank(FolderCandidate candidate, string quality) =>
        candidate.Rejection is not null ? 3
            : candidate.Folder.NeedsTranscode ? 1
                : candidate.Folder.Quality == quality ? 0
                    : 2;

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
