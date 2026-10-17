using System.Security.Claims;
using Buscarr.Server.MusicBrainz;
using Buscarr.Server.Slskd;
using Buscarr.Server.Users;

namespace Buscarr.Server.Endpoints;

public record UserSettings(string Edition, string Quality);

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings").RequireAuthorization();

        group.MapGet("/", GetSettings);
        group.MapPut("/", PutSettings);
    }

    private static async Task<UserSettings> GetSettings(UserStore users, ClaimsPrincipal user) =>
        new(await users.EditionPreferenceAsync(user.UserId()), await users.QualityPreferenceAsync(user.UserId()));

    private static async Task<IResult> PutSettings(UserStore users, ClaimsPrincipal user, UserSettings settings)
    {
        if (!Editions.Preferences.Contains(settings.Edition) || !Qualities.Preferences.Contains(settings.Quality))
        {
            return Results.BadRequest();
        }

        await users.SetEditionPreferenceAsync(user.UserId(), settings.Edition);
        await users.SetQualityPreferenceAsync(user.UserId(), settings.Quality);

        return Results.NoContent();
    }
}
