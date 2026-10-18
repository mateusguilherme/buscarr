using System.Net;
using System.Net.Http.Json;
using System.Web;
using Buscarr.Server.Data;
using Buscarr.Server.Deezer;
using Buscarr.Server.Downloads;
using Buscarr.Server.Endpoints;
using Buscarr.Server.FanartTv;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Settings;
using Buscarr.Server.Slskd;
using Buscarr.Server.Spotify;
using Buscarr.Server.Subsonic;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using static Buscarr.Server.Tests.Fakes.MusicBrainzJson;

namespace Buscarr.Server.Tests.Fakes;

public sealed class BuscarrApiFactory : WebApplicationFactory<Program>
{
    public const string NirvanaId = "5b11f4ce-a62d-471e-81fc-a69a8278c7da";

    public const string NirvanaImage =
        $"https://assets.fanart.tv/preview/music/{NirvanaId}/artistthumb/nirvana.jpg";

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"buscarr-tests-{Guid.NewGuid()}.db");

    public BuscarrApiFactory()
    {
        FanartTv = new FakeHttpMessageHandler(RespondLikeFanartTv);
    }

    private FakeHttpMessageHandler MusicBrainz { get; } = new(RespondLikeMusicBrainz);

    public FakeHttpMessageHandler FanartTv { get; }

    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public FakeSlskd Slskd { get; } = new();

    public FakeHttpMessageHandler Spotify { get; } = new(request =>
        request.RequestUri!.Host == "accounts.spotify.com"
            ? FakeHttpMessageHandler.Json(SpotifyJson.Token())
            : FakeHttpMessageHandler.Json(SpotifyJson.Albums()));

    public FakeHttpMessageHandler Subsonic { get; } =
        new(_ => FakeHttpMessageHandler.Json(SubsonicJson.Ok()));

    public bool FanartTvDown { get; init; }

    public bool SlskdDown { get; init; }

    public async Task<HttpClient> CreateLoggedInClientAsync(bool admin = false)
    {
        var client = CreateClient();
        var email = $"{Guid.NewGuid()}@exemplo.com";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Fulano", "segredo123"));
        response.EnsureSuccessStatusCode();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BuscarrDbContext>().Users
            .Where(u => u.Email == email)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.IsAdmin, admin));

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("hostBuilder:reloadConfigOnChange", "false");
        builder.ConfigureTestServices(services =>
        {
            services.ConfigureDbContext<BuscarrDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.RemoveAll<MusicBrainzClient>();
            services.AddSingleton(new MusicBrainzClient(
                new HttpClient(MusicBrainz),
                new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromSeconds(1) }));
            services.RemoveAll<DeezerClient>();
            services.AddSingleton(new DeezerClient(
                new HttpClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json(DeezerJson.Albums())))));
            services.RemoveAll<FanartTvClient>();
            services.AddSingleton(new FanartTvClient(new HttpClient(FanartTv), "chave-de-teste"));
            services.RemoveAll<SlskdClient>();
            services.AddSingleton(new SlskdClient(
                new HttpClient(SlskdDown
                    ? new FakeHttpMessageHandler(_ => throw new HttpRequestException("fora do ar"))
                    : Slskd.Handler),
                "http://slskd.local:5030",
                "chave-de-teste"));
            services.RemoveAll<SubsonicClient>();
            services.AddSingleton(provider =>
            {
                var store = provider.GetRequiredService<ServerSettingsStore>();

                return new SubsonicClient(new HttpClient(Subsonic), () => store.Current);
            });
            services.RemoveAll<SpotifyClient>();
            services.AddSingleton(provider =>
            {
                var store = provider.GetRequiredService<ServerSettingsStore>();

                return new SpotifyClient(new HttpClient(Spotify), () => store.Current, Time);
            });
            services.AddDataProtection().UseEphemeralDataProtectionProvider();

            foreach (var worker in services
                .Where(s => s.ImplementationType == typeof(GeneratorWorker) || s.ImplementationType == typeof(SearchWorker))
                .ToList())
            {
                services.Remove(worker);
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    private static HttpResponseMessage RespondLikeMusicBrainz(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        var search = HttpUtility.ParseQueryString(request.RequestUri.Query)["query"] ?? "";

        if (path == "/ws/2/artist")
        {
            return FakeHttpMessageHandler.Json(search.Contains("nirvana")
                ? SearchResults(
                    SearchResult("1", "Approaching Nirvana"),
                    SearchResult(NirvanaId, "Nirvana", "United States", "90s US grunge band"))
                : Artists());
        }

        if (path == $"/ws/2/artist/{NirvanaId}")
        {
            return FakeHttpMessageHandler.Json(Artist(NirvanaId, "Nirvana"));
        }

        return FakeHttpMessageHandler.Status(HttpStatusCode.NotFound);
    }

    private HttpResponseMessage RespondLikeFanartTv(HttpRequestMessage request)
    {
        if (FanartTvDown)
        {
            return FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError);
        }

        return request.RequestUri!.AbsolutePath == $"/v3/music/{NirvanaId}"
            ? FakeHttpMessageHandler.Json(FanartTvJson.Artist((
                "artistthumb",
                $"https://assets.fanart.tv/fanart/music/{NirvanaId}/artistthumb/nirvana.jpg",
                3)))
            : FakeHttpMessageHandler.Json(FanartTvJson.NotFound(), HttpStatusCode.NotFound);
    }
}
