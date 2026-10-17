using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Data;
using Buscarr.Server.Endpoints;
using Buscarr.Server.MusicBrainz;
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

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"buscarr-tests-{Guid.NewGuid()}.db");

    public FakeHttpMessageHandler MusicBrainz { get; } = new(RespondLikeMusicBrainz);

    public async Task<HttpClient> CreateLoggedInClientAsync()
    {
        var client = CreateClient();
        var email = $"{Guid.NewGuid()}@exemplo.com";
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Fulano", "segredo123"));
        response.EnsureSuccessStatusCode();

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.ConfigureDbContext<BuscarrDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));
            services.RemoveAll<MusicBrainzClient>();
            services.AddSingleton(new MusicBrainzClient(
                new HttpClient(MusicBrainz),
                new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromSeconds(1) }));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
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
        var query = request.RequestUri.Query;

        if (path == "/ws/2/artist")
        {
            return FakeHttpMessageHandler.Json(query.Contains("query=nirvana")
                ? Artists(("1", "Approaching Nirvana"), (NirvanaId, "Nirvana"))
                : Artists());
        }

        if (path == $"/ws/2/artist/{NirvanaId}")
        {
            return FakeHttpMessageHandler.Json(Artist(NirvanaId, "Nirvana"));
        }

        if (path == "/ws/2/release")
        {
            return FakeHttpMessageHandler.Json(Releases(
                2,
                Release("Nevermind", "1991-09-24", "1991-09-24"),
                Release("MTV Unplugged in New York", "1994-11-01", "1994-11-01", "Live")));
        }

        return FakeHttpMessageHandler.Status(HttpStatusCode.NotFound);
    }
}
