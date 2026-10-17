using System.Net;
using System.Text;

namespace Buscarr.Server.Tests.Fakes;

public record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    string UserAgent,
    IReadOnlyDictionary<string, string> Headers,
    string? Body);

public sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var headers = request.Headers.ToDictionary(header => header.Key, header => string.Join(" ", header.Value));
        var body = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            headers.GetValueOrDefault("User-Agent", ""),
            headers,
            body));

        return Task.FromResult(respond(request));
    }
}
