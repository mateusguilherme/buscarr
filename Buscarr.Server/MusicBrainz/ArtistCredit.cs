using JetBrains.Annotations;

namespace Buscarr.Server.MusicBrainz;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class ArtistCredit
{
    public Artist? Artist { get; init; }
}
