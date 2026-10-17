namespace Buscarr.Server.MusicBrainz;

public record Artist(string Id, string Name, string? Country = null, Area? Area = null);
