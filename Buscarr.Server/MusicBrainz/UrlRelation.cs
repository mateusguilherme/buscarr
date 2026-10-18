using JetBrains.Annotations;

namespace Buscarr.Server.MusicBrainz;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class UrlRelation
{
    public UrlTarget? Url { get; init; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class UrlTarget
{
    public string? Resource { get; init; }
}
