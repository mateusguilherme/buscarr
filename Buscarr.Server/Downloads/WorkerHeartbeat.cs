using System.Collections.Concurrent;

namespace Buscarr.Server.Downloads;

public sealed class WorkerHeartbeat(TimeProvider time)
{
    public const string Search = "search";
    public const string Generator = "generator";

    private readonly ConcurrentDictionary<string, long> _beats = new();

    public void Beat(string worker) => _beats[worker] = time.GetTimestamp();

    public TimeSpan? Age(string worker) =>
        _beats.TryGetValue(worker, out var beat) ? time.GetElapsedTime(beat) : null;
}
