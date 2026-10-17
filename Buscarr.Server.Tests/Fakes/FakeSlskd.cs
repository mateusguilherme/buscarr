using System.Net;
using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public sealed class FakeSlskd
{
    private readonly Dictionary<Guid, string> _searches = [];
    private readonly Dictionary<Guid, int> _polls = [];
    private readonly Dictionary<string, string> _responses = [];
    private int _busyStarts;

    public FakeSlskd()
    {
        Handler = new FakeHttpMessageHandler(Respond);
    }

    public FakeHttpMessageHandler Handler { get; }

    public List<string> SearchTexts { get; } = [];

    public string[] States { get; set; } = ["InProgress", "Completed, TimedOut"];

    public void BusyFor(int starts) => _busyStarts = starts;

    public void Answer(string searchText, params object[] responses) =>
        _responses[searchText] = SlskdJson.Responses(responses);

    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath.Split('/');

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

        using var body = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        var id = body.RootElement.GetProperty("id").GetGuid();
        var text = body.RootElement.GetProperty("searchText").GetString()!;
        _searches[id] = text;
        SearchTexts.Add(text);

        return FakeHttpMessageHandler.Json(SlskdJson.Search(id, "InProgress"));
    }
}
