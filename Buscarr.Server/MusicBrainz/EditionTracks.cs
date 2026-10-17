namespace Buscarr.Server.MusicBrainz;

public sealed record ReleaseTrack(int Position, string Title, int? LengthMs, string? RecordingId);

public sealed record EditionTracks(string? ArtistId, string? ReleaseGroupId, List<ReleaseTrack> Tracks);
