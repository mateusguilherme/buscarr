using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public static class MusicBrainzJson
{
    public static string Artists(params (string Id, string Name)[] artists) =>
        JsonSerializer.Serialize(new { artists = artists.Select(a => new { id = a.Id, name = a.Name }) });

    public static string ArtistsWithArea(params (string Id, string Name, string? Country, string? Area)[] artists) =>
        JsonSerializer.Serialize(new
        {
            artists = artists.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                country = a.Country,
                area = a.Area is null ? null : new { name = a.Area },
            }),
        });

    public static string Artist(string id, string name, string? country = null) =>
        JsonSerializer.Serialize(new { id, name, country });

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
