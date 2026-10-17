namespace Buscarr.Server.Downloads;

public sealed class SearchWorker(IServiceScopeFactory scopes, DownloadRunner runner, TimeProvider time)
    : BackgroundService
{
    private static readonly TimeSpan BetweenSearches = TimeSpan.FromSeconds(16);
    private static readonly TimeSpan IdleWait = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

            try
            {
                await Task.Delay(searched is null ? IdleWait : BetweenSearches, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
