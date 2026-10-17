using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public static class SlskdJson
{
    public static string Application(bool isConnected, bool isLoggedIn) =>
        JsonSerializer.Serialize(new
        {
            server = new
            {
                address = "server.slsknet.org:2242",
                isConnected,
                isLoggedIn,
            },
        });

    public static string Search(Guid id, string state, int responseCount = 0) =>
        JsonSerializer.Serialize(new
        {
            id,
            searchText = "",
            state,
            isComplete = state.StartsWith("Completed", StringComparison.Ordinal),
            responseCount,
            fileCount = 0,
        });

    public static string Responses(params object[] responses) =>
        JsonSerializer.Serialize(responses);

    public static object Response(
        string username,
        object[] files,
        bool hasFreeUploadSlot = true,
        int queueLength = 0,
        int uploadSpeed = 1_000_000) =>
        new
        {
            username,
            fileCount = files.Length,
            files,
            hasFreeUploadSlot,
            queueLength,
            uploadSpeed,
            lockedFileCount = 0,
            lockedFiles = Array.Empty<object>(),
        };

    public static object AudioFile(string filename, int? bitRate = 320, long size = 8_000_000, int? length = 200) =>
        new
        {
            filename,
            size,
            bitRate,
            length,
            extension = "",
            isLocked = false,
            code = 1,
        };

    public static object[] AlbumFiles(string directory, int tracks, int? bitRate = 320, string extension = "mp3") =>
        [.. Enumerable.Range(1, tracks).Select(track =>
            AudioFile($"{directory}\\{track:00} - Faixa {track}.{extension}", bitRate))];
}
