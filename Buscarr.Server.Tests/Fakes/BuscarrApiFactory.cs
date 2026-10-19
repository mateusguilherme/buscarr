using System.Net;
using System.Net.Http.Json;
using System.Web;
using Buscarr.Server.Data;
using Buscarr.Server.Deezer;
using Buscarr.Server.Downloads;
using Buscarr.Server.Endpoints;
using Buscarr.Server.FanartTv;
using Buscarr.Server.Images;
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
        $"https://assets.fanart.tv/fanart/music/{NirvanaId}/artistthumb/nirvana.jpg";

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"buscarr-tests-{Guid.NewGuid()}.db");

    public BuscarrApiFactory()
    {
        FanartTv = new FakeHttpMessageHandler(RespondLikeFanartTv);
    }

    public FakeHttpMessageHandler MusicBrainz { get; } = new(RespondLikeMusicBrainz);

    public FakeHttpMessageHandler FanartTv { get; }

    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public FakeSlskd Slskd { get; } = new();

    public FakeHttpMessageHandler Deezer { get; } = new(_ => FakeHttpMessageHandler.Json(DeezerJson.NotFound()));

    public FakeHttpMessageHandler Spotify { get; } = new(request =>
        request.RequestUri!.Host == "accounts.spotify.com"
            ? FakeHttpMessageHandler.Json(SpotifyJson.Token())
            : FakeHttpMessageHandler.Json(SpotifyJson.Albums()));

    public FakeHttpMessageHandler Subsonic { get; } =
        new(_ => FakeHttpMessageHandler.Json(SubsonicJson.Ok()));

    public FakeHttpMessageHandler Images { get; } =
        new(_ => FakeHttpMessageHandler.Bytes([1, 2, 3], "image/jpeg"));

    public bool FanartTvDown { get; init; }

    public bool SlskdDown { get; init; }

    public async Task<HttpClient> CreateLoggedInClientAsync(bool admin = false)
    {
        var client = CreateClient();
        var email = $"{Guid.NewGuid()}@exemplo.com";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "segredo123"));
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
            services.AddSingleton(new DeezerClient(new HttpClient(Deezer)));
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
            services.RemoveAll<ImageCache>();
            services.AddSingleton(new ImageCache(new HttpClient(Images)));
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
            return FakeHttpMessageHandler.Json(Artist(
                NirvanaId,
                "Nirvana",
                "https://www.deezer.com/artist/412",
                "https://open.spotify.com/artist/sp-nirvana"));
        }

        if (path == "/ws/2/release")
        {
            var artist = HttpUtility.ParseQueryString(request.RequestUri.Query)["artist"];

            return FakeHttpMessageHandler.Json(artist == NirvanaId
                ? Releases(
                    3,
                    WithMedia(
                        WithCoverArt(Release("Nevermind", "1991-09-24", "1991-09-24"), "r1", "g1", front: true),
                        ("CD", 12)),
                    WithMedia(
                        WithDisambiguation(
                            WithCoverArt(Release("Nevermind", "2011-09-26", "1991-09-24"), "r2", "g1", front: true),
                            "deluxe edition"),
                        ("CD", 40)),
                    WithPrimaryType(
                        WithMedia(
                            WithCoverArt(Release("Sliver", "1990-09-01", "1990-09-01"), "r3", "g2", front: true),
                            ("CD", 2)),
                        "Single"))
                : Releases(0));
        }

        if (path == "/ws/2/release/r1")
        {
            return FakeHttpMessageHandler.Json(ReleaseWithTracks(
                "r1",
                "a1",
                "g1",
                LookupMedium(
                    1,
                    LookupTrack(1, "Smells Like Teen Spirit", null, "rec1"),
                    LookupTrack(2, "In Bloom", null, "rec2"),
                    LookupTrack(3, "Territorial Pissings", null, "rec3"))));
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
