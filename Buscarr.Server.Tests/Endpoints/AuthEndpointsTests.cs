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
        Assert.Equal(new UserInfo(email, "Fulano"), me);
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
