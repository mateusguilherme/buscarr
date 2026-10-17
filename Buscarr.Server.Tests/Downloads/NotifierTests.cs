using System.Net;
using Buscarr.Server.Downloads;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Downloads;

public sealed class NotifierTests
{
    [Fact]
    public async Task NotifyAsync_PostsTheMessageToTheTopic()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(HttpStatusCode.OK));
        var notifier = new Notifier(new HttpClient(handler), "https://ntfy.exemplo.com/buscarr");

        await notifier.NotifyAsync("Nevermind (Nirvana) falhou: nenhuma pasta passou na verificação");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://ntfy.exemplo.com/buscarr", request.Uri.ToString());
        Assert.Equal("Nevermind (Nirvana) falhou: nenhuma pasta passou na verificação", request.Body);
    }

    [Fact]
    public async Task NotifyAsync_DoesNothingWithoutATopic()
    {
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Status(HttpStatusCode.OK));
        var notifier = new Notifier(new HttpClient(handler), null);

        await notifier.NotifyAsync("qualquer coisa");

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task NotifyAsync_SwallowsASendFailure()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("fora do ar"));
        var notifier = new Notifier(new HttpClient(handler), "https://ntfy.exemplo.com/buscarr");

        await notifier.NotifyAsync("qualquer coisa");
    }

    [Fact]
    public void Describe_TurnsEveryReasonIntoAReadablePhrase()
    {
        Assert.Equal("nenhuma pasta passou na verificação", FailureText.Describe("rejected"));
        Assert.Equal("as faixas não batem com a edição", FailureText.Describe("wrong-tracks"));
        Assert.Equal("a qualidade anunciada é falsa", FailureText.Describe("fake-audio"));
        Assert.Equal("o peer recusou", FailureText.Describe("refused"));
        Assert.Equal("motivo-desconhecido", FailureText.Describe("motivo-desconhecido"));
    }
}
