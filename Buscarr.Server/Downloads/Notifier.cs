namespace Buscarr.Server.Downloads;

public sealed class Notifier(HttpClient http, string? url)
{
    public async Task NotifyAsync(string message)
    {
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        try
        {
            using var response = await http.PostAsync(url, new StringContent(message));
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            Console.Error.WriteLine($"erro ao notificar: {e.Message}");
        }
    }
}
