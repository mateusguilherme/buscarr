using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace Buscarr.Server.MusicBrainz;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class ArtistCredit
{
    public string? Name { get; init; }

    [JsonPropertyName("joinphrase")]
    public string? JoinPhrase { get; init; }

    public Artist? Artist { get; init; }
}
