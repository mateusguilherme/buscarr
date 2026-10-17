using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Endpoints;
using Buscarr.Server.FanartTv;
using Buscarr.Server.Lists;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;
using Buscarr.Server.Users;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new MusicBrainzClient(new HttpClient(), TimeProvider.System));
builder.Services.AddSingleton(new FanartTvClient(
    new HttpClient { Timeout = TimeSpan.FromSeconds(5) },
    builder.Configuration["FanartTv:ApiKey"]));
builder.Services.AddSingleton(new SlskdClient(
    new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
    builder.Configuration["Slskd:Url"],
    builder.Configuration["Slskd:ApiKey"]));
builder.Services.AddDbContext<BuscarrDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Buscarr")));
builder.Services.AddScoped<UserStore>();
builder.Services.AddScoped<ArtistListStore>();
builder.Services.AddSingleton<GeneratorTrigger>();
builder.Services.AddScoped<RequestGenerator>();
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
    scope.ServiceProvider.GetRequiredService<BuscarrDbContext>().Database.Migrate();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapArtistEndpoints();
app.MapSettingsEndpoints();
app.MapListEndpoints();
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Lifetime.ApplicationStarted.Register(() => Console.WriteLine("Buscarr.Server rodando em http://127.0.0.1:3000"));
app.Run("http://127.0.0.1:3000");
