using Buscarr.Server.Downloads;
using Microsoft.Extensions.Time.Testing;

namespace Buscarr.Server.Tests.Downloads;

public sealed class WorkerHeartbeatTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void Age_IsUnknownBeforeTheFirstBeat()
    {
        var heartbeat = new WorkerHeartbeat(_time);

        Assert.Null(heartbeat.Age(WorkerHeartbeat.Search));
    }

    [Fact]
    public void Age_GrowsSinceTheLastBeat()
    {
        var heartbeat = new WorkerHeartbeat(_time);

        heartbeat.Beat(WorkerHeartbeat.Search);
        _time.Advance(TimeSpan.FromMinutes(3));

        Assert.Equal(TimeSpan.FromMinutes(3), heartbeat.Age(WorkerHeartbeat.Search));
    }

    [Fact]
    public void Beat_ResetsTheAge()
    {
        var heartbeat = new WorkerHeartbeat(_time);

        heartbeat.Beat(WorkerHeartbeat.Generator);
        _time.Advance(TimeSpan.FromHours(2));
        heartbeat.Beat(WorkerHeartbeat.Generator);

        Assert.Equal(TimeSpan.Zero, heartbeat.Age(WorkerHeartbeat.Generator));
    }
}
