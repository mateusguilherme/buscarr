using System.Diagnostics;
using Buscarr.Server.Import;

namespace Buscarr.Server.Tests.Import;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_ReturnsTheOutputOfAFinishedProcess()
    {
        var result = await ProcessRunner.RunAsync("sh", ["-c", "echo saida; echo erro 1>&2"], TimeSpan.FromSeconds(10));

        Assert.Equal((0, false), (result.ExitCode, result.TimedOut));
        Assert.Equal("saida", result.Output.Trim());
        Assert.Equal("erro", result.Error.Trim());
    }

    [Fact]
    public async Task RunAsync_KillsTheProcessWhenTheTimeLimitExpires()
    {
        var watch = Stopwatch.StartNew();

        var result = await ProcessRunner.RunAsync("sleep", ["30"], TimeSpan.FromMilliseconds(200));

        Assert.Equal((-1, true), (result.ExitCode, result.TimedOut));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RunAsync_ReportsAToolThatDoesNotExist()
    {
        var result = await ProcessRunner.RunAsync("buscarr-ferramenta-inexistente", [], TimeSpan.FromSeconds(1));

        Assert.Equal((-1, false), (result.ExitCode, result.TimedOut));
    }
}
