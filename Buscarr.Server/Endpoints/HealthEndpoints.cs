using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Slskd;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Endpoints;

public record WorkerHealth(double? AgeSeconds, bool Stalled);

public record HealthReport(
    string Status,
    List<string> Problems,
    bool Database,
    SlskdServerState? Slskd,
    Dictionary<string, WorkerHealth> Workers);

public static class HealthEndpoints
{
    private static readonly TimeSpan SearchLimit = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan GeneratorLimit = TimeSpan.FromHours(13);

    public static void MapHealthEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/health", GetHealth);

    private static async Task<IResult> GetHealth(BuscarrDbContext db, SlskdClient slskd, WorkerHeartbeat heartbeat)
    {
        var problems = new List<string>();
        var database = true;

        try
        {
            await db.Users.AnyAsync();
        }
        catch (Exception e)
        {
            database = false;
            problems.Add($"banco inacessível: {e.Message}");
        }

        SlskdServerState? state = null;

        try
        {
            state = await slskd.ServerStateAsync();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"erro no slskd: {e.Message}");
        }

        var workers = new Dictionary<string, WorkerHealth>
        {
            [WorkerHeartbeat.Search] = Check(heartbeat, WorkerHeartbeat.Search, SearchLimit, problems),
            [WorkerHeartbeat.Generator] = Check(heartbeat, WorkerHeartbeat.Generator, GeneratorLimit, problems),
        };
        var report = new HealthReport(
            problems.Count == 0 ? "ok" : "degraded",
            problems,
            database,
            state,
            workers);

        return problems.Count == 0
            ? Results.Json(report)
            : Results.Json(report, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static WorkerHealth Check(
        WorkerHeartbeat heartbeat,
        string worker,
        TimeSpan limit,
        List<string> problems)
    {
        var age = heartbeat.Age(worker);
        var stalled = age > limit;

        if (stalled)
        {
            problems.Add($"o worker {worker} está parado há {age!.Value:g}");
        }

        return new(age?.TotalSeconds, stalled);
    }
}
