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

    public static Dictionary<string, object> Release(
        string title,
        string date,
        string firstReleaseDate,
        params string[] secondaryTypes) =>
        new()
        {
            ["title"] = title,
            ["date"] = date,
            ["release-group"] = new Dictionary<string, object>
            {
                ["first-release-date"] = firstReleaseDate,
                ["secondary-types"] = secondaryTypes,
            },
        };
}
