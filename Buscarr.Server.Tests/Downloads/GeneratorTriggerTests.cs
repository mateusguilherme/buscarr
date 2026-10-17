using Buscarr.Server.Downloads;
using Microsoft.Extensions.Time.Testing;

namespace Buscarr.Server.Tests.Downloads;

public class GeneratorTriggerTests
{
    private readonly GeneratorTrigger _trigger = new();
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task WaitAsync_ReturnsAtOnceWhenAlreadyFired()
    {
        _trigger.Fire();

        await _trigger.WaitAsync(TimeSpan.FromHours(12), _time, CancellationToken.None);

        Assert.False(_trigger.Pending);
    }

    [Fact]
    public async Task WaitAsync_ReturnsWhenFiredDuringTheWait()
    {
        var wait = _trigger.WaitAsync(TimeSpan.FromHours(12), _time, CancellationToken.None);

        Assert.False(wait.IsCompleted);

        _trigger.Fire();
        await wait;
    }

    [Fact]
    public async Task WaitAsync_ReturnsWhenTheTimeoutPasses()
    {
        var wait = _trigger.WaitAsync(TimeSpan.FromHours(12), _time, CancellationToken.None);

        Assert.False(wait.IsCompleted);

        _time.Advance(TimeSpan.FromHours(12));
        await wait;
    }

    [Fact]
    public async Task WaitAsync_ConsumesEveryPendingFire()
    {
        _trigger.Fire();
        _trigger.Fire();

        await _trigger.WaitAsync(TimeSpan.FromHours(12), _time, CancellationToken.None);

        Assert.False(_trigger.Pending);
    }
}
