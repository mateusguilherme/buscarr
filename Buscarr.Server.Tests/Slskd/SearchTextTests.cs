using Buscarr.Server.Slskd;

namespace Buscarr.Server.Tests.Slskd;

public class SearchTextTests
{
    [Theory]
    [InlineData("Nirvana", "Nevermind", "Nirvana Nevermind")]
    [InlineData("AC/DC", "Back in Black", "AC DC Back in Black")]
    [InlineData("Guns N' Roses", "Appetite for Destruction", "Guns N Roses Appetite for Destruction")]
    [InlineData("Charlie Brown Jr.", "Transpiração Contínua Prolongada", "Charlie Brown Jr Transpiração Contínua Prolongada")]
    [InlineData("Nirvana", "MTV Unplugged in New York", "Nirvana MTV Unplugged in New York")]
    public void For_JoinsArtistAndAlbumWithoutPunctuation(string artist, string album, string expected)
    {
        Assert.Equal(expected, SearchText.For(artist, album));
    }

    [Theory]
    [InlineData("Lady Gaga The Fame Monster", "*ady *aga *he *ame *onster")]
    [InlineData("AC DC Back in Black", "AC DC *ack in *lack")]
    [InlineData("Beyoncé Lemonade", "*eyoncé *emonade")]
    public void Wildcard_SwapsTheFirstLetterOfEachWordOfThreeOrMoreLetters(string text, string expected)
    {
        Assert.Equal(expected, SearchText.Wildcard(text));
    }
}
