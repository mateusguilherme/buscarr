using System.Net;
using System.Text.Json;
using Buscarr.Server.Settings;

namespace Buscarr.Server.Slskd;

public record SlskdServerState(bool IsConnected, bool IsLoggedIn);

public record SlskdSearch(Guid Id, string State, bool IsComplete, int ResponseCount);

public record SlskdResponse(
    string Username,
    bool HasFreeUploadSlot,
    int QueueLength,
    int UploadSpeed,
    List<SlskdFile> Files);

public record SlskdFile(string Filename, long Size, int? BitRate, int? BitDepth, int? Length);

public record SlskdTransfer(Guid Id, string Filename, long Size, string State, long BytesTransferred, int? PlaceInQueue);

public sealed class SlskdClient(HttpClient http, Func<ServerSettings> settings)
{
    private const string UserAgent = "buscarr/0.1.0";

    public SlskdClient(HttpClient http, string? url, string? apiKey)
        : this(http, () => new ServerSettings { SlskdUrl = url, SlskdApiKey = apiKey })
    {
    }

    private static bool IsConfigured(ServerSettings current) =>
        !string.IsNullOrEmpty(current.SlskdUrl) && !string.IsNullOrEmpty(current.SlskdApiKey);

    public async Task<SlskdServerState?> ServerStateAsync()
    {
        if (!IsConfigured(settings()))
        {
            return null;
        }

        var application = await GetAsync<ApplicationState>("application");

        return application.Server;
    }

    public async Task<Guid?> StartSearchAsync(string searchText)
    {
        var id = Guid.NewGuid();
        using var request = Request(HttpMethod.Post, "searches");
        request.Content = JsonContent.Create(new { id, searchText });
        using var response = await http.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return id;
    }

    public Task<SlskdSearch> SearchAsync(Guid id) =>
        GetAsync<SlskdSearch>($"searches/{id}");

    public Task<List<SlskdResponse>> SearchResponsesAsync(Guid id) =>
        GetAsync<List<SlskdResponse>>($"searches/{id}/responses");

    public async Task EnqueueDownloadsAsync(string username, IEnumerable<SlskdFile> files)
    {
        using var request = Request(HttpMethod.Post, Downloads(username));
        request.Content = JsonContent.Create(files.Select(file => new { file.Filename, file.Size }));
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<SlskdTransfer>> DownloadsAsync(string username)
    {
        using var request = Request(HttpMethod.Get, Downloads(username));
        using var response = await http.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<UserTransfers>()
            ?? throw new JsonException("resposta vazia do slskd nas transferências");

        return [.. user.Directories.SelectMany(directory => directory.Files)];
    }

    public async Task CancelDownloadAsync(string username, Guid id)
    {
        using var request = Request(HttpMethod.Delete, $"{Downloads(username)}/{id}");
        using var response = await http.SendAsync(request);

        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    private static string Downloads(string username) =>
        $"transfers/downloads/{Uri.EscapeDataString(username)}";

    private async Task<T> GetAsync<T>(string path)
    {
        using var request = Request(HttpMethod.Get, path);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new JsonException($"resposta vazia do slskd em {path}");
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var current = settings();

        if (!IsConfigured(current))
        {
            throw new InvalidOperationException("slskd sem endereço ou chave de API");
        }

        var request = new HttpRequestMessage(method, $"{current.SlskdUrl!.TrimEnd('/')}/api/v0/{path}");
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("X-API-Key", current.SlskdApiKey);

        return request;
    }

    private sealed class ApplicationState
    {
        public required SlskdServerState Server { get; init; }
    }

    private sealed class UserTransfers
    {
        public List<TransferDirectory> Directories { get; init; } = [];
    }

    private sealed class TransferDirectory
    {
        public List<SlskdTransfer> Files { get; init; } = [];
    }
}
