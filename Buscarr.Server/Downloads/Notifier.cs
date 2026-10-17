using Buscarr.Server.Settings;

namespace Buscarr.Server.Downloads;

public sealed class Notifier(HttpClient http, Func<ServerSettings> settings)
{
    public Notifier(HttpClient http, string? url)
        : this(http, () => new ServerSettings { NtfyUrl = url })
    {
    }

    public async Task NotifyAsync(string message)
    {
        var url = settings().NtfyUrl;

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
