using JetBrains.Annotations;

namespace Buscarr.Server.MusicBrainz;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public record Artist(string Id, string Name, Area? Area = null, string? Disambiguation = null);
