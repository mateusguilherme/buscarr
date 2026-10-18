using JetBrains.Annotations;

namespace Buscarr.Server.MusicBrainz;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class ArtistCredit
{
    public string? Name { get; init; }

    public Artist? Artist { get; init; }
}
