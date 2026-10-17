namespace Buscarr.Server.MusicBrainz;

public record Album(Release First, List<Release> Editions);
