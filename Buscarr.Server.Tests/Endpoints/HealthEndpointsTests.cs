using System.Net;
using System.Text.Json;
using Buscarr.Server.Downloads;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Buscarr.Server.Tests.Endpoints;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task GetHealth_AnswersWithoutLogin()
    {
        using var factory = new BuscarrApiFactory();
        var client = factory.CreateClient();
        Beat(factory);

        var response = await client.GetAsync("/health");
        var report = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", report.GetProperty("status").GetString());
        Assert.True(report.GetProperty("database").GetBoolean());
        Assert.True(report.GetProperty("slskd").GetProperty("isConnected").GetBoolean());
    }

    [Fact]
    public async Task GetHealth_StaysOkWhenSlskdIsDown()
    {
        using var factory = new BuscarrApiFactory { SlskdDown = true };
        var client = factory.CreateClient();
        Beat(factory);

        var response = await client.GetAsync("/health");
        var report = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, report.GetProperty("slskd").ValueKind);
    }

    [Fact]
    public async Task GetHealth_DoesNotDegradeBeforeTheFirstBeat()
    {
        using var factory = new BuscarrApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var report = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            JsonValueKind.Null,
            report.GetProperty("workers").GetProperty("search").GetProperty("ageSeconds").ValueKind);
    }

    [Fact]
    public async Task GetHealth_DegradesWhenTheSearchWorkerStalls()
    {
        using var factory = new BuscarrApiFactory();
        var client = factory.CreateClient();
        Beat(factory);
        factory.Time.Advance(TimeSpan.FromMinutes(11));

        var response = await client.GetAsync("/health");
        var report = await ReadAsync(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("degraded", report.GetProperty("status").GetString());
        Assert.True(report.GetProperty("workers").GetProperty("search").GetProperty("stalled").GetBoolean());
        Assert.False(report.GetProperty("workers").GetProperty("generator").GetProperty("stalled").GetBoolean());
    }

    [Fact]
    public async Task GetHealth_GivesTheGeneratorRoomForItsTwelveHourCycle()
    {
        using var factory = new BuscarrApiFactory();
        var client = factory.CreateClient();
        Beat(factory);
        factory.Time.Advance(TimeSpan.FromHours(14));

        var response = await client.GetAsync("/health");
        var report = await ReadAsync(response);

        Assert.True(report.GetProperty("workers").GetProperty("generator").GetProperty("stalled").GetBoolean());
    }

    private static void Beat(BuscarrApiFactory factory)
    {
        var heartbeat = factory.Services.GetRequiredService<WorkerHeartbeat>();
        heartbeat.Beat(WorkerHeartbeat.Search);
        heartbeat.Beat(WorkerHeartbeat.Generator);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return json.RootElement.Clone();
    }
}
