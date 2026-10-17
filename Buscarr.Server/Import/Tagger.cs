using Buscarr.Server.MusicBrainz;
using TagLib;

namespace Buscarr.Server.Import;

public sealed record AlbumTags(
    string Artist,
    string Album,
    string? Date,
    string Type,
    string? ArtistId,
    string? ReleaseGroupId,
    string? ReleaseId);

public sealed class Tagger
{
    public void Tag(string path, AlbumTags album, ReleaseTrack track, int trackCount)
    {
        using var file = TagLib.File.Create(path);
        file.RemoveTags(TagTypes.AllTags);
        var tag = file.GetTag(
            Path.GetExtension(path).ToLowerInvariant() == ".mp3" ? TagTypes.Id3v2 : TagTypes.Xiph,
            true);
        tag.Title = track.Title;
        tag.Performers = [album.Artist];
        tag.AlbumArtists = [album.Artist];
        tag.Album = album.Album;
        tag.Track = (uint)track.Position;
        tag.TrackCount = (uint)trackCount;
        tag.MusicBrainzArtistId = album.ArtistId;
        tag.MusicBrainzReleaseGroupId = album.ReleaseGroupId;
        tag.MusicBrainzReleaseId = album.ReleaseId;
        tag.MusicBrainzTrackId = track.RecordingId;
        tag.MusicBrainzReleaseType = album.Type;

        if (album.Date is { Length: >= 4 } date && uint.TryParse(date[..4], out var year))
        {
            tag.Year = year;
        }

        file.Save();
    }
}
