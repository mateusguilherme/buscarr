using Buscarr.Server.Data;
using Buscarr.Server.Deezer;
using Buscarr.Server.Downloads;
using Buscarr.Server.Endpoints;
using Buscarr.Server.FanartTv;
using Buscarr.Server.Import;
using Buscarr.Server.Lists;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Settings;
using Buscarr.Server.Slskd;
using Buscarr.Server.Subsonic;
using Buscarr.Server.Users;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new MusicBrainzClient(new HttpClient(), TimeProvider.System));
builder.Services.AddSingleton(new DeezerClient(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }));
builder.Services.AddSingleton<ServerSettingsStore>();
builder.Services.AddSingleton(services => new FanartTvClient(
    new HttpClient { Timeout = TimeSpan.FromSeconds(5) },
    CurrentSettings(services)));
builder.Services.AddSingleton(services => new SlskdClient(
    new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
    CurrentSettings(services)));
builder.Services.AddSingleton(services => new SubsonicClient(
    new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
    CurrentSettings(services)));
builder.Services.AddDbContext<BuscarrDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Buscarr")));
builder.Services.AddScoped<UserStore>();
builder.Services.AddScoped<ArtistListStore>();
builder.Services.AddSingleton<GeneratorTrigger>();
builder.Services.AddSingleton<WorkerHeartbeat>();
builder.Services.AddScoped<RequestGenerator>();
builder.Services.AddSingleton<AlbumSearch>();
builder.Services.AddSingleton<AlbumDownload>();
builder.Services.AddSingleton<IAudioTools>(new FfmpegAudioTools());
builder.Services.AddSingleton(services =>
{
    var settings = CurrentSettings(services);

    return new ImportPipeline(
        services.GetRequiredService<MusicBrainzClient>(),
        services.GetRequiredService<IAudioTools>(),
        new CoverArt(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }),
        () => settings().Import);
});
builder.Services.AddSingleton<DownloadRunner>();
builder.Services.AddScoped<SearchQueue>();
builder.Services.AddScoped<ImportQueue>();
builder.Services.AddHostedService<GeneratorWorker>();
builder.Services.AddHostedService<SearchWorker>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "buscarr";
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
    db.Database.Migrate();
    await scope.ServiceProvider.GetRequiredService<UserStore>().EnsureAdminAsync();
    await app.Services.GetRequiredService<ServerSettingsStore>().LoadAsync(db, app.Configuration);
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapArtistEndpoints();
app.MapSettingsEndpoints();
app.MapServerSettingsEndpoints();
app.MapListEndpoints();
app.MapDownloadEndpoints();
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

var url = builder.Configuration["urls"] ?? "http://127.0.0.1:3000";
app.Lifetime.ApplicationStarted.Register(() => Console.WriteLine($"Buscarr.Server rodando em {url}"));
app.Run(url);

static Func<ServerSettings> CurrentSettings(IServiceProvider services)
{
    var store = services.GetRequiredService<ServerSettingsStore>();

    return () => store.Current;
}
