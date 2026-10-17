using Buscarr.Server.Import;
using Buscarr.Server.MusicBrainz;

namespace Buscarr.Server.Tests.Import;

public sealed class TaggerTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"buscarr-tagger-{Guid.NewGuid()}");

    private readonly Tagger _tagger = new();

    public TaggerTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, true);

    [Theory]
    [InlineData("silence.mp3")]
    [InlineData("silence.flac")]
    public void Tag_WritesTheAlbumAndTrackTags(string fixture)
    {
        var path = CopyFixture(fixture);

        _tagger.Tag(path, Album(), new ReleaseTrack(2, "In Bloom", 255_000, "rec2"), 12);

        using var file = TagLib.File.Create(path);
        Assert.Equal("In Bloom", file.Tag.Title);
        Assert.Equal(["Nirvana"], file.Tag.Performers);
        Assert.Equal(["Nirvana"], file.Tag.AlbumArtists);
        Assert.Equal("Nevermind", file.Tag.Album);
        Assert.Equal((2u, 12u, 1991u), (file.Tag.Track, file.Tag.TrackCount, file.Tag.Year));
        Assert.Equal("a1", file.Tag.MusicBrainzArtistId);
        Assert.Equal("g1", file.Tag.MusicBrainzReleaseGroupId);
        Assert.Equal("r1", file.Tag.MusicBrainzReleaseId);
        Assert.Equal("rec2", file.Tag.MusicBrainzTrackId);
        Assert.Equal("album", file.Tag.MusicBrainzReleaseType);
    }

    [Fact]
    public void Tag_DropsTheUploadersTags()
    {
        var path = CopyFixture("silence.mp3");

        using (var file = TagLib.File.Create(path))
        {
            file.Tag.Comment = "ripped by fulano";
            file.Tag.MusicBrainzReleaseId = "outra-edicao";
            file.Tag.Genres = ["137"];
            file.Save();
        }

        _tagger.Tag(path, Album(), new ReleaseTrack(1, "Smells Like Teen Spirit", 301_000, "rec1"), 12);

        using var tagged = TagLib.File.Create(path);
        Assert.True(string.IsNullOrEmpty(tagged.Tag.Comment));
        Assert.Empty(tagged.Tag.Genres);
        Assert.Equal("r1", tagged.Tag.MusicBrainzReleaseId);
    }

    [Fact]
    public void Tag_LeavesTheYearEmptyWithoutAReleaseDate()
    {
        var path = CopyFixture("silence.mp3");

        _tagger.Tag(path, Album() with { Date = null }, new ReleaseTrack(1, "Faixa", null, null), 1);

        using var file = TagLib.File.Create(path);
        Assert.Equal(0u, file.Tag.Year);
    }

    private string CopyFixture(string name)
    {
        var path = Path.Combine(_directory, name);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Import", "fixtures", name), path);

        return path;
    }

    private static AlbumTags Album() =>
        new("Nirvana", "Nevermind", "1991-09-24", "album", "a1", "g1", "r1");
}
