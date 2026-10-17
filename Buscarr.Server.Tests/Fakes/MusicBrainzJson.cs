using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public static class MusicBrainzJson
{
    public static string Artists(params (string Id, string Name)[] artists) =>
        SearchResults([.. artists.Select(a => SearchResult(a.Id, a.Name))]);

    public static string SearchResults(params object[] artists) =>
        JsonSerializer.Serialize(new { artists });

    public static object SearchResult(string id, string name, string? area = null, string? disambiguation = null) =>
        new
        {
            id,
            name,
            area = area is null ? null : new { name = area },
            disambiguation,
        };

    public static string Artist(string id, string name) =>
        JsonSerializer.Serialize(new { id, name });

    public static string Releases(int releaseCount, params Dictionary<string, object>[] releases) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["release-count"] = releaseCount,
            ["releases"] = releases,
        });

    public static Dictionary<string, object> WithCoverArt(
        Dictionary<string, object> release,
        string id,
        string releaseGroupId,
        bool front)
    {
        release["id"] = id;
        release["cover-art-archive"] = new { front };
        ((Dictionary<string, object>)release["release-group"])["id"] = releaseGroupId;

        return release;
    }

    public static Dictionary<string, object> WithMedia(
        Dictionary<string, object> release,
        params (string Format, int Tracks)[] media)
    {
        release["media"] = media.Select(medium => new Dictionary<string, object>
        {
            ["format"] = medium.Format,
            ["track-count"] = medium.Tracks,
        });

        return release;
    }

    public static Dictionary<string, object> WithDisambiguation(Dictionary<string, object> release, string disambiguation)
    {
        release["disambiguation"] = disambiguation;

        return release;
    }

    public static Dictionary<string, object> WithPrimaryType(Dictionary<string, object> release, string primaryType)
    {
        ((Dictionary<string, object>)release["release-group"])["primary-type"] = primaryType;

        return release;
    }

    public static Dictionary<string, object> Release(
        string title,
        string date,
        string firstReleaseDate,
        params string[] secondaryTypes) =>
        new()
        {
            ["id"] = $"{title}/{date}",
            ["title"] = title,
            ["date"] = date,
            ["release-group"] = new Dictionary<string, object>
            {
                ["id"] = $"group/{title}/{date}",
                ["primary-type"] = "Album",
                ["first-release-date"] = firstReleaseDate,
                ["secondary-types"] = secondaryTypes,
            },
        };

    public static string ReleaseWithTracks(
        string id,
        string artistId,
        string releaseGroupId,
        params object[] media) =>
        JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = id,
            ["title"] = "Nevermind",
            ["artist-credit"] = new object[] { new { artist = new { id = artistId, name = "Nirvana" } } },
            ["release-group"] = new Dictionary<string, object> { ["id"] = releaseGroupId },
            ["media"] = media,
        });

    public static object LookupMedium(int position, params object[] tracks) =>
        new { position, tracks };

    public static object LookupTrack(
        int position,
        string title,
        int? length = null,
        string? recordingId = null,
        int? recordingLength = null) =>
        new Dictionary<string, object?>
        {
            ["position"] = position,
            ["title"] = title,
            ["length"] = length,
            ["recording"] = recordingId is null && recordingLength is null
                ? null
                : new Dictionary<string, object?> { ["id"] = recordingId, ["length"] = recordingLength },
        };
}
