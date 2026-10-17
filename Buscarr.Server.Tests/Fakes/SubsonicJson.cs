namespace Buscarr.Server.Tests.Fakes;

public static class SubsonicJson
{
    public static string AlbumList(params string[] albums) =>
        @"{""subsonic-response"":{""status"":""ok"",""version"":""1.16.1"",""type"":""navidrome"",""openSubsonic"":true,"
        + @"""albumList2"":{""album"":[" + string.Join(",", albums) + "]}}}";

    public static string EmptyAlbumList() =>
        @"{""subsonic-response"":{""status"":""ok"",""version"":""1.16.1"",""albumList2"":{}}}";

    public static string Album(string id, string name, string artist, int songCount, string? musicBrainzId = null)
    {
        var mbid = musicBrainzId is null ? "" : $@",""musicBrainzId"":""{musicBrainzId}""";

        return $@"{{""id"":""{id}"",""name"":""{name}"",""artist"":""{artist}"",""songCount"":{songCount}{mbid}}}";
    }

    public static string Failed(string message) =>
        @"{""subsonic-response"":{""status"":""failed"",""version"":""1.16.1"","
        + $@"""error"":{{""code"":40,""message"":""{message}""}}}}}}";
}
