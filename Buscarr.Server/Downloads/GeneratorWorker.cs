namespace Buscarr.Server.Downloads;

public sealed class GeneratorWorker(
    IServiceScopeFactory scopes,
    GeneratorTrigger trigger,
    WorkerHeartbeat heartbeat,
    TimeProvider time)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            heartbeat.Beat(WorkerHeartbeat.Generator);

            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<RequestGenerator>().GenerateAsync();
            }
            catch (Exception e)
            {
                await Console.Error.WriteLineAsync($"erro ao gerar os pedidos: {e.Message}");
            }

            try
            {
                await trigger.WaitAsync(Interval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
