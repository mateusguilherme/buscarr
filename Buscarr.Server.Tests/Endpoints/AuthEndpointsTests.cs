using System.Net;
using System.Net.Http.Json;
using Buscarr.Server.Endpoints;
using Buscarr.Server.Tests.Fakes;

namespace Buscarr.Server.Tests.Endpoints;

public class AuthEndpointsTests(BuscarrApiFactory factory) : IClassFixture<BuscarrApiFactory>
{
    [Fact]
    public async Task Me_WithoutLogin_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_CreatesAccountAndStartsSession()
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Fulano", "segredo123"));
        var me = await client.GetFromJsonAsync<UserInfo>("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, me?.Email);
        Assert.Equal("Fulano", me?.Name);
    }

    [Fact]
    public async Task Register_MakesOnlyTheFirstUserAnAdmin()
    {
        using var fresh = new BuscarrApiFactory();
        var first = fresh.CreateClient();
        var second = fresh.CreateClient();
        var firstEmail = NewEmail();
        var secondEmail = NewEmail();

        await first.PostAsJsonAsync("/api/auth/register", new RegisterRequest(firstEmail, "Fulano", "segredo123"));
        await second.PostAsJsonAsync("/api/auth/register", new RegisterRequest(secondEmail, "Beltrano", "segredo123"));

        Assert.Equal(new UserInfo(firstEmail, "Fulano", true), await first.GetFromJsonAsync<UserInfo>("/api/auth/me"));
        Assert.Equal(new UserInfo(secondEmail, "Beltrano", false), await second.GetFromJsonAsync<UserInfo>("/api/auth/me"));
    }

    [Fact]
    public async Task Register_WithShortPassword_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(NewEmail(), "Fulano", "curta"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithExistingEmail_Returns409()
    {
        var email = NewEmail();
        await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Fulano", "segredo123"));

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email.ToUpperInvariant(), "Outro", "outrasenha"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_StartsSession()
    {
        var email = NewEmail();
        await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Fulano", "segredo123"));
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "segredo123"));
        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var email = NewEmail();
        await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Fulano", "segredo123"));

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "errada123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var client = await factory.CreateLoggedInClientAsync();

        await client.PostAsync("/api/auth/logout", null);
        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    private static string NewEmail() => $"{Guid.NewGuid()}@exemplo.com";
}
