using System.Text;
using Buscarr.Server.Import;

namespace Buscarr.Server.Tests.Import;

public sealed class LibraryImporterTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"buscarr-importer-{Guid.NewGuid()}");

    private readonly string _library;
    private readonly string _downloads;
    private readonly LibraryImporter _importer = new();

    public LibraryImporterTests()
    {
        _library = Path.Combine(_root, "musicas");
        _downloads = Path.Combine(_root, "complete");
        Directory.CreateDirectory(_library);
        Directory.CreateDirectory(_downloads);
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void Import_KeepsAnEpApartFromTheAlbumWithTheSameTitle()
    {
        _importer.Import(_library, "Metallica", "St. Anger", "album", [new(Source("a.mp3"), 1, "St. Anger")]);
        var ep = _importer.Import(_library, "Metallica", "St. Anger", "ep", [new(Source("b.mp3"), 1, "St. Anger")]);

        Assert.Equal(Path.Combine(_library, "Metallica", "St. Anger (EP)"), ep);
        Assert.True(File.Exists(Path.Combine(_library, "Metallica", "St. Anger", "01 St. Anger.mp3")));
    }

    [Fact]
    public void Import_PlacesTheAlbumInTheLibraryTree()
    {
        var album = _importer.Import(
            _library,
            "Nirvana",
            "Nevermind",
            "album",
            [
                new(Source("a.mp3"), 1, "Smells Like Teen Spirit"),
                new(Source("b.mp3"), 2, "In Bloom"),
            ]);

        Assert.Equal(Path.Combine(_library, "Nirvana", "Nevermind"), album);
        Assert.Equal(
            ["01 Smells Like Teen Spirit.mp3", "02 In Bloom.mp3"],
            Directory.GetFiles(album).Select(Path.GetFileName).Order());
        Assert.Empty(Directory.GetFiles(_downloads));
        Assert.DoesNotContain(Directory.GetDirectories(_library), dir => Path.GetFileName(dir).StartsWith('.'));
    }

    [Fact]
    public void Import_SanitizesAndTruncatesEverySegment()
    {
        var longTitle = new string('ã', 200);

        var album = _importer.Import(
            _library,
            "AC/DC",
            "Live: 1992",
            "album",
            [new(Source("a.mp3"), 1, longTitle)]);

        var file = Assert.Single(Directory.GetFiles(album));
        var segments = Path.GetRelativePath(_library, file).Split(Path.DirectorySeparatorChar);

        Assert.Equal(["AC DC", "Live 1992"], segments[..2]);
        Assert.All(segments, segment => Assert.True(Encoding.UTF8.GetByteCount(segment) <= 255));
        Assert.StartsWith("01 ãã", Path.GetFileName(file));
        Assert.EndsWith(".mp3", file);
    }

    [Fact]
    public void Import_ReplacesAnExistingAlbumOnlyAfterTheNewOneIsInPlace()
    {
        var existing = Path.Combine(_library, "Nirvana", "Nevermind");
        Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "velho.mp3"), "velho");

        var album = _importer.Import(
            _library,
            "Nirvana",
            "Nevermind",
            "album",
            [new(Source("a.mp3"), 1, "Smells Like Teen Spirit")]);

        Assert.Equal(
            ["01 Smells Like Teen Spirit.mp3"],
            Directory.GetFiles(album).Select(Path.GetFileName));
        Assert.DoesNotContain(
            Directory.GetDirectories(Path.Combine(_library, "Nirvana")),
            dir => Path.GetFileName(dir) != "Nevermind");
    }

    [Fact]
    public void Import_WritesTheCoverInTheAlbumFolder()
    {
        var cover = new CoverImage([0xFF, 0xD8, 0xFF], "image/jpeg", ".jpg");

        var album = _importer.Import(
            _library,
            "Nirvana",
            "Nevermind",
            "album",
            [new(Source("a.mp3"), 1, "Smells Like Teen Spirit")],
            cover);

        Assert.Equal(cover.Bytes, File.ReadAllBytes(Path.Combine(album, "cover.jpg")));
    }

    private string Source(string name)
    {
        var path = Path.Combine(_downloads, name);
        File.WriteAllText(path, name);

        return path;
    }
}
