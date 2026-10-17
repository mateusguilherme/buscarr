using System.Text;

namespace Buscarr.Server.Import;

public sealed record ImportFile(string Source, int Position, string Title);

public sealed class LibraryImporter
{
    private const int MaxSegmentBytes = 255;

    private static readonly char[] InvalidChars =
        [.. Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    private static readonly Dictionary<string, string> TypeSuffixes = new()
    {
        ["ep"] = " (EP)",
        ["single"] = " (Single)",
        ["live"] = " (Live)",
        ["compilation"] = " (Compilation)",
        ["soundtrack"] = " (Soundtrack)",
        ["remix"] = " (Remix)",
        ["demo"] = " (Demo)",
    };

    public string Import(string libraryPath, string artist, string album, string type, IReadOnlyList<ImportFile> files)
    {
        var temp = Path.Combine(libraryPath, $".buscarr-{Guid.NewGuid()}");
        var artistSegment = Segment(artist, "");
        var typeSuffix = TypeSuffixes.GetValueOrDefault(type, "");
        var albumSegment = Segment(album, typeSuffix) + typeSuffix;

        try
        {
            var staged = Path.Combine(temp, albumSegment);
            Directory.CreateDirectory(staged);

            foreach (var file in files)
            {
                var extension = Path.GetExtension(file.Source).ToLowerInvariant();
                var prefix = $"{file.Position:00} ";
                var name = prefix + Segment(file.Title, extension, MaxSegmentBytes - prefix.Length) + extension;
                File.Move(file.Source, Path.Combine(staged, name));
            }

            var target = Path.Combine(libraryPath, artistSegment, albumSegment);
            Directory.CreateDirectory(Path.Combine(libraryPath, artistSegment));

            if (Directory.Exists(target))
            {
                var old = $"{target}.old-{Guid.NewGuid()}";
                Directory.Move(target, old);
                Directory.Move(staged, target);
                Directory.Delete(old, true);
            }
            else
            {
                Directory.Move(staged, target);
            }

            return target;
        }
        finally
        {
            if (Directory.Exists(temp))
            {
                Directory.Delete(temp, true);
            }
        }
    }

    private static string Segment(string name, string suffix, int maxBytes = MaxSegmentBytes)
    {
        var clean = string.Join(
                " ",
                new string([.. name.Select(c => InvalidChars.Contains(c) || char.IsControl(c) ? ' ' : c)])
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .TrimEnd('.');

        if (clean.Length == 0)
        {
            clean = "_";
        }

        var budget = maxBytes - Encoding.UTF8.GetByteCount(suffix);

        while (Encoding.UTF8.GetByteCount(clean) > budget)
        {
            clean = clean[..^1];
        }

        return clean;
    }
}
