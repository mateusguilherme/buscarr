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

        Assert.Equal(["52654632"], artist.DeezerIds);
        Assert.Equal(["0eHkGhhUyp3LZdtZjaA8gd"], artist.SpotifyIds);
    }

    [Fact]
    public void StreamingIds_KeepEveryLinkInOrder()
    {
        var artist = new Artist("a1", "Seether")
        {
            Relations =
            [
                Relation("https://open.spotify.com/artist/5ZAm84INxpjY5dmaZjxo32"),
                Relation("https://open.spotify.com/artist/6B5c4sch27tWHAGdarpPaW"),
                Relation("https://www.deezer.com/artist/1189"),
            ],
        };

        Assert.Equal(["5ZAm84INxpjY5dmaZjxo32", "6B5c4sch27tWHAGdarpPaW"], artist.SpotifyIds);
        Assert.Equal(["1189"], artist.DeezerIds);
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

        Assert.Equal(["52654632"], artist.DeezerIds);
        Assert.Equal(["0eHkGhhUyp3LZdtZjaA8gd"], artist.SpotifyIds);
    }

    [Fact]
    public void StreamingIds_AreEmptyWithoutTheRelations()
    {
        var artist = new Artist("a1", "Project46");

        Assert.Empty(artist.DeezerIds);
        Assert.Empty(artist.SpotifyIds);
    }

    private static UrlRelation Relation(string resource) =>
        new() { Url = new UrlTarget { Resource = resource } };
}
