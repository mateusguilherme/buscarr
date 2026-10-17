using Buscarr.Server.Downloads;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;

namespace Buscarr.Server.Import;

public sealed class ImportPipeline(MusicBrainzClient musicBrainz, AudioTools tools, ImportOptions options)
{
    private readonly AlbumVerifier _verifier = new(tools);
    private readonly Transcoder _transcoder = new(tools);
    private readonly Tagger _tagger = new();
    private readonly LibraryImporter _importer = new();

    public bool Enabled => options.Enabled;

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

        var files = local.Value.Files.OrderBy(Path.GetFileName, NaturalOrder.Instance).ToList();

        if (folder.NeedsTranscode)
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
                options.LibraryPath!,
                request.ArtistName,
                request.Title,
                [.. files.Select((file, i) => new ImportFile(file, tracks.Tracks[i].Position, tracks.Tracks[i].Title))]);
            Directory.Delete(local.Value.Directory, true);

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
        var candidate = Path.Combine(options.DownloadsPath!, Name(folder.Directory));

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

        foreach (var directory in Directory.EnumerateDirectories(options.DownloadsPath!, "*", SearchOption.AllDirectories))
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
            Directory.CreateDirectory(options.Rejected);
            var target = Path.Combine(options.Rejected, $"{Path.GetFileName(directory)}-{Guid.NewGuid():N}");
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
