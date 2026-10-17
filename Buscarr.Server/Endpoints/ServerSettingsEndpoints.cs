using Buscarr.Server.Data;
using Buscarr.Server.Settings;
using Buscarr.Server.Slskd;
using Buscarr.Server.Subsonic;
using Buscarr.Server.Users;

namespace Buscarr.Server.Endpoints;

public record ServerSettingsView(
    string SlskdUrl,
    bool HasSlskdApiKey,
    string DownloadsPath,
    string LibraryPath,
    string RejectedPath,
    string SubsonicUrl,
    string SubsonicUser,
    bool HasSubsonicPassword,
    bool HasFanartTvApiKey);

public record ServerSettingsUpdate(
    string? SlskdUrl,
    string? SlskdApiKey,
    string? DownloadsPath,
    string? LibraryPath,
    string? RejectedPath,
    string? SubsonicUrl,
    string? SubsonicUser,
    string? SubsonicPassword,
    string? FanartTvApiKey);

public record ServerSettingsErrors(Dictionary<string, string> Errors);

public record ConnectionStatus(string State, string? Message);

public record ServerStatus(ConnectionStatus Slskd, ConnectionStatus Subsonic);

public static class ServerSettingsEndpoints
{
    public static void MapServerSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/server-settings").RequireAuthorization().AddEndpointFilter(RequireAdmin);

        group.MapGet("/", GetSettings);
        group.MapPut("/", PutSettings);
        group.MapGet("/status", GetStatus);
    }

    private static async ValueTask<object?> RequireAdmin(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var users = http.RequestServices.GetRequiredService<UserStore>();

        if (!await users.IsAdminAsync(http.User.UserId()))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }

    private static ServerSettingsView GetSettings(ServerSettingsStore store)
    {
        var settings = store.Current;

        return new ServerSettingsView(
            settings.SlskdUrl ?? "",
            settings.SlskdApiKey is not null,
            settings.DownloadsPath ?? "",
            settings.LibraryPath ?? "",
            settings.RejectedPath ?? "",
            settings.SubsonicUrl ?? "",
            settings.SubsonicUser ?? "",
            settings.SubsonicPassword is not null,
            settings.FanartTvApiKey is not null);
    }

    private static async Task<IResult> PutSettings(
        ServerSettingsUpdate update,
        ServerSettingsStore store,
        BuscarrDbContext db)
    {
        var current = store.Current;
        var settings = new ServerSettings
        {
            SlskdUrl = Clean(update.SlskdUrl),
            SlskdApiKey = Secret(update.SlskdApiKey, current.SlskdApiKey),
            DownloadsPath = Clean(update.DownloadsPath),
            LibraryPath = Clean(update.LibraryPath),
            RejectedPath = Clean(update.RejectedPath),
            SubsonicUrl = Clean(update.SubsonicUrl),
            SubsonicUser = Clean(update.SubsonicUser),
            SubsonicPassword = Secret(update.SubsonicPassword, current.SubsonicPassword),
            FanartTvApiKey = Secret(update.FanartTvApiKey, current.FanartTvApiKey),
        };
        var errors = ServerSettingsValidator.Validate(settings);

        if (errors.Count > 0)
        {
            return Results.BadRequest(new ServerSettingsErrors(errors));
        }

        await store.SaveAsync(db, settings);

        return Results.NoContent();
    }

    private static async Task<ServerStatus> GetStatus(SlskdClient slskd, SubsonicClient subsonic) =>
        new(await SlskdStatusAsync(slskd), await SubsonicStatusAsync(subsonic));

    private static async Task<ConnectionStatus> SlskdStatusAsync(SlskdClient slskd)
    {
        try
        {
            var state = await slskd.ServerStateAsync();

            if (state is null)
            {
                return new("not-configured", null);
            }

            return state.IsConnected && state.IsLoggedIn ? new("ok", null) : new("offline", null);
        }
        catch (Exception e)
        {
            return new("error", e.Message);
        }
    }

    private static async Task<ConnectionStatus> SubsonicStatusAsync(SubsonicClient subsonic)
    {
        if (!subsonic.IsConfigured)
        {
            return new("not-configured", null);
        }

        try
        {
            await subsonic.PingAsync();
        }
        catch (Exception e)
        {
            return new("error", e.Message);
        }

        return new("ok", null);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Secret(string? value, string? current) =>
        value is null ? current : Clean(value);
}
