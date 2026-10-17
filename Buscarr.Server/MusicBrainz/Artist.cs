namespace Buscarr.Server.MusicBrainz;

public record Artist(string Id, string Name, Area? Area = null, string? Disambiguation = null);
