using Buscarr.Server.Downloads;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;

namespace Buscarr.Server.Import;

public sealed class ImportPipeline(MusicBrainzClient musicBrainz, AudioTools tools, Func<ImportOptions> currentOptions)
{
    private readonly AlbumVerifier _verifier = new(tools);
    private readonly Transcoder _transcoder = new(tools);
    private readonly Tagger _tagger = new();
    private readonly LibraryImporter _importer = new();

    public ImportPipeline(MusicBrainzClient musicBrainz, AudioTools tools, ImportOptions options)
        : this(musicBrainz, tools, () => options)
    {
    }

    public bool Enabled => Options.Enabled;

    private ImportOptions Options => currentOptions();

    public async Task<EditionTracks?> TracksAsync(DownloadRequest request)
    {
        if (!Enabled || request.EditionReleaseId is null)
        {
            return null;
        }

        var tracks = await musicBrainz.ReleaseTracksAsync("", request.EditionReleaseId);

        return tracks.Tracks.Count > 0 ? tracks : null;
    }

    public async Task<string?> VerifyAsync(DownloadRequest request, EditionTracks tracks, SoulseekFolder folder)
    {
        var local = Resolve(folder);

        if (local is null)
        {
            return "missing-files";
        }

        var reason = await _verifier.VerifyAsync(local.Value.Files, tracks.Tracks, request.Quality, folder.NeedsTranscode);

        if (reason is not null)
        {
            Quarantine(local.Value.Directory, request, reason);
        }

        return reason;
    }

    public async Task<string?> ImportAsync(DownloadRequest request, EditionTracks tracks, SoulseekFolder folder)
    {
        var local = Resolve(folder);

        if (local is null)
        {
            return "missing-files";
        }

        return await ImportFilesAsync(request, tracks, local.Value.Directory, local.Value.Files, folder.NeedsTranscode);
    }

    public async Task<string?> ReimportAsync(DownloadRequest request, EditionTracks tracks)
    {
        var directory = Path.Combine(Options.DownloadsPath!, Name(request.Directory ?? ""));

        if (request.Directory is null || !Directory.Exists(directory))
        {
            return "missing-files";
        }

        var files = Directory.EnumerateFiles(directory).Where(Folders.IsAudioFile).ToList();
        var needsTranscode =
            !files.All(file => Folders.MatchesQuality(file, request.Quality))
            && files.All(Folders.IsLosslessFile);
        var reason = await _verifier.VerifyAsync(files, tracks.Tracks, request.Quality, needsTranscode);

        if (reason is not null)
        {
            Quarantine(directory, request, reason);

            return reason;
        }

        return await ImportFilesAsync(request, tracks, directory, files, needsTranscode);
    }

    private async Task<string?> ImportFilesAsync(
        DownloadRequest request,
        EditionTracks tracks,
        string directory,
        List<string> sources,
        bool needsTranscode)
    {
        var files = sources.OrderBy(Path.GetFileName, NaturalOrder.Instance).ToList();

        if (needsTranscode)
        {
            files = await _transcoder.ToMp3Async(files) ?? [];

            if (files.Count == 0)
            {
                return "transcode-failed";
            }
        }

        try
        {
            var album = new AlbumTags(
                request.ArtistName,
                request.Title,
                request.FirstReleaseDate,
                request.Type,
                tracks.ArtistId ?? request.ArtistId,
                tracks.ReleaseGroupId ?? request.ReleaseGroupId,
                request.EditionReleaseId);

            for (var i = 0; i < files.Count; i++)
            {
                _tagger.Tag(files[i], album, tracks.Tracks[i], tracks.Tracks.Count);
            }

            _importer.Import(
                Options.LibraryPath!,
                request.ArtistName,
                request.Title,
                request.Type,
                [.. files.Select((file, i) => new ImportFile(file, tracks.Tracks[i].Position, tracks.Tracks[i].Title))]);
            Directory.Delete(directory, true);

            return null;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"erro ao importar {request.ArtistName} {request.Title}: {e.Message}");

            return "import-failed";
        }
    }

    private (string Directory, List<string> Files)? Resolve(SoulseekFolder folder)
    {
        var names = folder.Files.Select(file => Name(file.Filename)).ToList();
        var candidate = Path.Combine(Options.DownloadsPath!, Name(folder.Directory));

        foreach (var directory in Candidates(candidate))
        {
            var files = names.Select(name => Path.Combine(directory, name)).ToList();

            if (files.All(File.Exists))
            {
                return (directory, files);
            }
        }

        return null;
    }

    private IEnumerable<string> Candidates(string first)
    {
        if (Directory.Exists(first))
        {
            yield return first;
        }

        foreach (var directory in Directory.EnumerateDirectories(Options.DownloadsPath!, "*", SearchOption.AllDirectories))
        {
            if (directory != first)
            {
                yield return directory;
            }
        }
    }

    private void Quarantine(string directory, DownloadRequest request, string reason)
    {
        try
        {
            Directory.CreateDirectory(Options.Rejected);
            var target = Path.Combine(Options.Rejected, $"{Path.GetFileName(directory)}-{Guid.NewGuid():N}");
            Directory.Move(directory, target);
            File.WriteAllText(
                $"{target}.reason.txt",
                $"{request.ArtistName} - {request.Title}: {reason}{Environment.NewLine}");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"erro ao mover para a quarentena {directory}: {e.Message}");
        }
    }

    private static string Name(string path) =>
        path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
}
