namespace Buscarr.Server.Downloads;

public sealed class SearchWorker(
    IServiceScopeFactory scopes,
    DownloadRunner runner,
    WorkerHeartbeat heartbeat,
    TimeProvider time)
    : BackgroundService
{
    private static readonly TimeSpan BetweenSearches = TimeSpan.FromSeconds(16);
    private static readonly TimeSpan IdleWait = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        heartbeat.Beat(WorkerHeartbeat.Search);

        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<SearchQueue>().RecoverAsync();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"erro ao recuperar a fila de busca: {e.Message}");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            heartbeat.Beat(WorkerHeartbeat.Search);
            SearchedRequest? searched = null;

            try
            {
                using var scope = scopes.CreateScope();
                searched = await scope.ServiceProvider.GetRequiredService<SearchQueue>().ProcessNextAsync();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"erro na fila de busca: {e.Message}");
            }

            if (searched is { Folders.Count: > 0 })
            {
                runner.Start(searched.RequestId, searched.Folders);
            }

            int? import = null;

            try
            {
                using var scope = scopes.CreateScope();
                import = await scope.ServiceProvider.GetRequiredService<ImportQueue>().ClaimNextAsync();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"erro na fila de importação: {e.Message}");
            }

            if (import is { } requestId)
            {
                runner.StartImport(requestId);
            }

            try
            {
                await Task.Delay(searched is null && import is null ? IdleWait : BetweenSearches, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
