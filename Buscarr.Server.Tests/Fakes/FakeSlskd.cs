using System.Net;
using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public record FakeTransfer(string State, long BytesTransferred = 0, int? PlaceInQueue = null);

public sealed class FakeSlskd
{
    private readonly Dictionary<Guid, string> _searches = [];
    private readonly Dictionary<Guid, int> _polls = [];
    private readonly Dictionary<string, string> _responses = [];
    private readonly Dictionary<string, Func<int, string, FakeTransfer>> _peers = [];
    private readonly Dictionary<string, List<(Guid Id, string Filename, long Size)>> _known = [];
    private readonly Dictionary<string, int> _transferPolls = [];
    private readonly HashSet<string> _refusing = [];
    private int _busyStarts;

    public FakeSlskd()
    {
        Handler = new FakeHttpMessageHandler(Respond);
    }

    public FakeHttpMessageHandler Handler { get; }

    public List<string> SearchTexts { get; } = [];

    public string[] States { get; set; } = ["InProgress", "Completed, TimedOut"];

    public List<(string Username, string Filename, long Size)> Enqueued { get; } = [];

    public List<(string Username, string Filename)> Cancelled { get; } = [];

    public void BusyFor(int starts) => _busyStarts = starts;

    public void Answer(string searchText, params object[] responses) =>
        _responses[searchText] = SlskdJson.Responses(responses);

    public void Peer(string username, Func<int, string, FakeTransfer> transfer) => _peers[username] = transfer;

    public void RefuseDownloadsFrom(string username) => _refusing.Add(username);

    public void AlreadyKnows(string username, string filename, long size) => Know(username, filename, size);

    public bool Connected { get; set; } = true;

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath.Split('/');

        if (path[3] == "application")
        {
            var state = Connected ? "true" : "false";

            return FakeHttpMessageHandler.Json(
                "{\"server\":{\"isConnected\":" + state + ",\"isLoggedIn\":" + state + "}}");
        }

        if (path[3] == "transfers")
        {
            return Transfers(request, Uri.UnescapeDataString(path[5]), path.Length > 6 ? Guid.Parse(path[6]) : null);
        }

        if (request.Method == HttpMethod.Post)
        {
            return Start(request);
        }

        var id = Guid.Parse(path[4]);

        if (path.Length == 6)
        {
            return FakeHttpMessageHandler.Json(_responses.GetValueOrDefault(_searches[id], "[]"));
        }

        var poll = _polls[id] = _polls.GetValueOrDefault(id) + 1;

        return FakeHttpMessageHandler.Json(SlskdJson.Search(id, States[Math.Min(poll, States.Length) - 1]));
    }

    private HttpResponseMessage Start(HttpRequestMessage request)
    {
        if (_busyStarts > 0)
        {
            _busyStarts--;

            return FakeHttpMessageHandler.Status(HttpStatusCode.TooManyRequests);
        }

        using var body = JsonDocument.Parse(Body(request));
        var id = body.RootElement.GetProperty("id").GetGuid();
        var text = body.RootElement.GetProperty("searchText").GetString()!;
        _searches[id] = text;
        SearchTexts.Add(text);

        return FakeHttpMessageHandler.Json(SlskdJson.Search(id, "InProgress"));
    }

    private HttpResponseMessage Transfers(HttpRequestMessage request, string username, Guid? id)
    {
        if (request.Method == HttpMethod.Post)
        {
            return Enqueue(request, username);
        }

        if (request.Method == HttpMethod.Delete)
        {
            Cancelled.Add((username, _known[username].Single(file => file.Id == id).Filename));

            return FakeHttpMessageHandler.Status(HttpStatusCode.NoContent);
        }

        if (!_known.TryGetValue(username, out var files))
        {
            return FakeHttpMessageHandler.Status(HttpStatusCode.NotFound);
        }

        var poll = _transferPolls[username] = _transferPolls.GetValueOrDefault(username) + 1;
        var transfer = _peers.GetValueOrDefault(username, (_, _) => new FakeTransfer("Completed, Succeeded"));

        return FakeHttpMessageHandler.Json(SlskdJson.UserTransfers(username, [.. files.Select(file =>
        {
            var state = transfer(poll, file.Filename);

            return SlskdJson.Transfer(
                file.Id,
                file.Filename,
                state.State,
                file.Size,
                state.State == "Completed, Succeeded" ? file.Size : state.BytesTransferred,
                state.PlaceInQueue);
        })]));
    }

    private HttpResponseMessage Enqueue(HttpRequestMessage request, string username)
    {
        if (_refusing.Contains(username))
        {
            return FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError);
        }

        using var body = JsonDocument.Parse(Body(request));

        foreach (var file in body.RootElement.EnumerateArray())
        {
            var filename = file.GetProperty("filename").GetString()!;
            var size = file.GetProperty("size").GetInt64();
            Enqueued.Add((username, filename, size));
            Know(username, filename, size);
        }

        return FakeHttpMessageHandler.Status(HttpStatusCode.Created);
    }

    private void Know(string username, string filename, long size)
    {
        if (!_known.TryGetValue(username, out var files))
        {
            files = _known[username] = [];
        }

        files.Add((Guid.NewGuid(), filename, size));
    }

    private static string Body(HttpRequestMessage request) =>
        request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
}
