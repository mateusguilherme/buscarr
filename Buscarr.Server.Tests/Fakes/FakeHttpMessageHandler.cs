using System.Net;
using System.Text;

namespace Buscarr.Server.Tests.Fakes;

public record RecordedRequest(Uri Uri, string UserAgent);

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
        var userAgent = request.Headers.TryGetValues("User-Agent", out var values)
            ? string.Join(" ", values)
            : "";
        Requests.Add(new RecordedRequest(request.RequestUri!, userAgent));

        return Task.FromResult(respond(request));
    }
}
