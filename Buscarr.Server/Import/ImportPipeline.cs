using Buscarr.Server.Deezer;
using Buscarr.Server.Downloads;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;

namespace Buscarr.Server.Import;

public sealed class ImportPipeline(
    MusicBrainzClient musicBrainz,
    IAudioTools tools,
    CoverArt covers,
    DeezerClient deezer,
    Func<ImportOptions> currentOptions)
{
    private readonly AlbumVerifier _verifier = new(tools);
    private readonly Transcoder _transcoder = new(tools);
    private readonly Tagger _tagger = new();
    private readonly LibraryImporter _importer = new();

    public ImportPipeline(
        MusicBrainzClient musicBrainz,
        IAudioTools tools,
        CoverArt covers,
        DeezerClient deezer,
        ImportOptions options)
        : this(musicBrainz, tools, covers, deezer, () => options)
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

    public async Task<ImportFailure?> VerifyAsync(DownloadRequest request, EditionTracks tracks, SoulseekFolder folder)
    {
        var local = Resolve(folder);

        if (local is null)
        {
            return new ImportFailure("missing-files");
        }

        var reason = await _verifier.VerifyAsync(
            local.Value.Files,
            tracks.Tracks,
            request.Quality,
            folder.NeedsTranscode,
            () => deezer.AlbumDurationsAsync(request.ArtistName, request.Title));

        if (reason is not null)
        {
            return new ImportFailure(reason, true, Quarantine(local.Value.Directory, request, reason));
        }

        return null;
    }

    public async Task<ImportFailure?> ImportAsync(DownloadRequest request, EditionTracks tracks, SoulseekFolder folder)
    {
        var local = Resolve(folder);

        if (local is null)
        {
            return new ImportFailure("missing-files");
        }

        return await ImportFilesAsync(request, tracks, local.Value.Directory, local.Value.Files, folder.NeedsTranscode);
    }

    public async Task<ImportFailure?> ReimportAsync(DownloadRequest request, EditionTracks tracks)
    {
        var directory = Path.Combine(Options.DownloadsPath!, Name(request.Directory ?? ""));

        if (request.Directory is null || !Directory.Exists(directory))
        {
            return new ImportFailure("missing-files");
        }

        var files = Directory.EnumerateFiles(directory).Where(Folders.IsAudioFile).ToList();
        var needsTranscode = NeedsTranscode(files, request.Quality);
        var reason = await _verifier.VerifyAsync(
            files,
            tracks.Tracks,
            request.Quality,
            needsTranscode,
            () => deezer.AlbumDurationsAsync(request.ArtistName, request.Title));

        if (reason is not null)
        {
            return new ImportFailure(reason, true, Quarantine(directory, request, reason));
        }

        return await ImportFilesAsync(request, tracks, directory, files, needsTranscode);
    }

    public async Task<ImportFailure?> ImportQuarantinedAsync(DownloadRequest request, EditionTracks tracks, string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new ImportFailure("missing-files");
        }

        var files = Directory.EnumerateFiles(directory).Where(Folders.IsAudioFile).ToList();

        if (files.Count != tracks.Tracks.Count)
        {
            return new ImportFailure("wrong-tracks");
        }

        return await ImportFilesAsync(request, tracks, directory, files, NeedsTranscode(files, request.Quality));
    }

    private async Task<ImportFailure?> ImportFilesAsync(
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
                return new ImportFailure("transcode-failed");
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
            var cover = await covers.DownloadAsync(request.CoverUrl);

            for (var i = 0; i < files.Count; i++)
            {
                _tagger.Tag(files[i], album, tracks.Tracks[i], tracks.Tracks.Count, cover);
            }

            _importer.Import(
                Options.LibraryPath!,
                request.ArtistName,
                request.Title,
                request.Type,
                [.. files.Select((file, i) => new ImportFile(file, tracks.Tracks[i].Position, tracks.Tracks[i].Title))],
                cover);
            Directory.Delete(directory, true);

            return null;
        }
        catch (Exception e)
        {
            await Console.Error.WriteLineAsync($"erro ao importar {request.ArtistName} {request.Title}: {e.Message}");

            return new ImportFailure("import-failed");
        }
    }

    private static bool NeedsTranscode(List<string> files, string quality) =>
        !files.All(file => Folders.MatchesQuality(file, quality)) && files.All(Folders.IsLosslessFile);

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

    private string? Quarantine(string directory, DownloadRequest request, string reason)
    {
        try
        {
            Directory.CreateDirectory(Options.Rejected);
            var target = Path.Combine(Options.Rejected, $"{Path.GetFileName(directory)}-{Guid.NewGuid():N}");
            Directory.Move(directory, target);
            File.WriteAllText(
                $"{target}.reason.txt",
                $"{request.ArtistName} - {request.Title}: {reason}{Environment.NewLine}");

            return target;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"erro ao mover para a quarentena {directory}: {e.Message}");

            return null;
        }
    }

    private static string Name(string path) =>
        path[(path.LastIndexOfAny(['\\', '/']) + 1)..];
}
