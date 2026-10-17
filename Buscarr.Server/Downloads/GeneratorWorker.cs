namespace Buscarr.Server.Downloads;

public sealed class GeneratorWorker(IServiceScopeFactory scopes, GeneratorTrigger trigger, TimeProvider time)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<RequestGenerator>().GenerateAsync();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"erro ao gerar os pedidos: {e.Message}");
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
