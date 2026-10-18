using Buscarr.Server.MusicBrainz;

namespace Buscarr.Server.Tests.MusicBrainz;

public sealed class ArtistTests
{
    [Fact]
    public void StreamingIds_ComeFromTheUrlRelations()
    {
        var artist = new Artist("a1", "AJULLIACOSTA")
        {
            Relations =
            [
                Relation("https://ajulliacosta.com.br"),
                Relation("https://open.spotify.com/artist/0eHkGhhUyp3LZdtZjaA8gd"),
                Relation("https://www.deezer.com/artist/52654632"),
            ],
        };

        Assert.Equal("52654632", artist.DeezerId);
        Assert.Equal("0eHkGhhUyp3LZdtZjaA8gd", artist.SpotifyId);
    }

    [Fact]
    public void StreamingIds_IgnoreTrailingPathAndQuery()
    {
        var artist = new Artist("a1", "AJULLIACOSTA")
        {
            Relations =
            [
                Relation("https://open.spotify.com/artist/0eHkGhhUyp3LZdtZjaA8gd?si=abc"),
                Relation("https://www.deezer.com/artist/52654632/"),
            ],
        };

        Assert.Equal("52654632", artist.DeezerId);
        Assert.Equal("0eHkGhhUyp3LZdtZjaA8gd", artist.SpotifyId);
    }

    [Fact]
    public void StreamingIds_AreNullWithoutTheRelations()
    {
        var artist = new Artist("a1", "Project46");

        Assert.Null(artist.DeezerId);
        Assert.Null(artist.SpotifyId);
    }

    private static UrlRelation Relation(string resource) =>
        new() { Url = new UrlTarget { Resource = resource } };
}
