using System.Security.Claims;
using Buscarr.Server.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Buscarr.Server.Endpoints;

public record RegisterRequest(string? Email, string? Name, string? Password);

public record LoginRequest(string? Email, string? Password);

public record UserInfo(string Email, string Name, bool IsAdmin);

public static class AuthEndpoints
{
    private const int MinPasswordLength = 8;

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/logout", Logout);
        group.MapGet("/me", Me).RequireAuthorization();
    }

    private static async Task<IResult> Me(ClaimsPrincipal user, UserStore users) =>
        Results.Json(new UserInfo(
            user.FindFirstValue(ClaimTypes.Email) ?? "",
            user.FindFirstValue(ClaimTypes.Name) ?? "",
            await users.IsAdminAsync(user.UserId())));

    private static async Task<IResult> Register(
        RegisterRequest request,
        UserStore users,
        HttpContext context)
    {
        var email = request.Email?.Trim() ?? "";
        var name = request.Name?.Trim() ?? "";
        var password = request.Password ?? "";

        if (email.Length == 0 || name.Length == 0 || password.Length < MinPasswordLength)
        {
            return Results.BadRequest();
        }

        var user = await users.CreateAsync(email, name, password);

        if (user is null)
        {
            return Results.Conflict();
        }

        await SignInAsync(context, user);

        return Results.Json(new UserInfo(user.Email, user.Name, user.IsAdmin));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserStore users,
        HttpContext context)
    {
        var user = await users.VerifyAsync(request.Email?.Trim() ?? "", request.Password ?? "");

        if (user is null)
        {
            return Results.Unauthorized();
        }

        await SignInAsync(context, user);

        return Results.Json(new UserInfo(user.Email, user.Name, user.IsAdmin));
    }

    private static IResult Logout() =>
        Results.SignOut(authenticationSchemes: [CookieAuthenticationDefaults.AuthenticationScheme]);

    private static Task SignInAsync(HttpContext context, User user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Name),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}
