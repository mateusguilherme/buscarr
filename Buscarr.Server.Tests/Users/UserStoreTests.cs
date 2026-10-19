using Buscarr.Server.Data;
using Buscarr.Server.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Tests.Users;

public sealed class UserStoreTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly BuscarrDbContext _db;
    private readonly UserStore _users;

    public UserStoreTests()
    {
        _connection.Open();
        _db = new BuscarrDbContext(new DbContextOptionsBuilder<BuscarrDbContext>().UseSqlite(_connection).Options);
        _db.Database.Migrate();
        _users = new UserStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateAsync_StoresHashedPassword()
    {
        var user = await _users.CreateAsync("fulano@exemplo.com", "segredo123");

        Assert.NotNull(user);
        Assert.NotEqual(0, user.Id);
        Assert.NotEqual("segredo123", user.PasswordHash);
        Assert.NotEmpty(user.PasswordHash);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNullWhenEmailExistsIgnoringCase()
    {
        await _users.CreateAsync("fulano@exemplo.com", "segredo123");

        var duplicate = await _users.CreateAsync("FULANO@exemplo.com", "outrasenha");

        Assert.Null(duplicate);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsUserForCorrectPasswordIgnoringEmailCase()
    {
        await _users.CreateAsync("fulano@exemplo.com", "segredo123");

        var user = await _users.VerifyAsync("Fulano@Exemplo.com", "segredo123");

        Assert.Equal("fulano@exemplo.com", user?.Email);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNullForWrongPassword()
    {
        await _users.CreateAsync("fulano@exemplo.com", "segredo123");

        var user = await _users.VerifyAsync("fulano@exemplo.com", "errada123");

        Assert.Null(user);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNullForUnknownEmail()
    {
        var user = await _users.VerifyAsync("ninguem@exemplo.com", "segredo123");

        Assert.Null(user);
    }

    [Fact]
    public async Task CreateAsync_MakesOnlyTheFirstUserAnAdmin()
    {
        var first = await _users.CreateAsync("fulano@exemplo.com", "segredo123");
        var second = await _users.CreateAsync("beltrano@exemplo.com", "segredo123");

        Assert.True(await _users.IsAdminAsync(first!.Id));
        Assert.False(await _users.IsAdminAsync(second!.Id));
    }

    [Fact]
    public async Task EnsureAdminAsync_PromotesTheOldestUserWhenNoneIsAdmin()
    {
        var first = await _users.CreateAsync("fulano@exemplo.com", "segredo123");
        var second = await _users.CreateAsync("beltrano@exemplo.com", "segredo123");
        await _db.Users.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.IsAdmin, false));

        await _users.EnsureAdminAsync();

        Assert.True(await _users.IsAdminAsync(first!.Id));
        Assert.False(await _users.IsAdminAsync(second!.Id));
    }

    [Fact]
    public async Task EnsureAdminAsync_KeepsTheAdminThereIs()
    {
        var first = await _users.CreateAsync("fulano@exemplo.com", "segredo123");
        var second = await _users.CreateAsync("beltrano@exemplo.com", "segredo123");
        await _db.Users.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.IsAdmin, u => u.Id == second!.Id));

        await _users.EnsureAdminAsync();

        Assert.False(await _users.IsAdminAsync(first!.Id));
        Assert.True(await _users.IsAdminAsync(second!.Id));
    }

    [Fact]
    public async Task EnsureAdminAsync_DoesNothingWithoutUsers()
    {
        await _users.EnsureAdminAsync();

        Assert.False(await _db.Users.AnyAsync());
    }

    [Fact]
    public async Task IsAdminAsync_IsFalseForAnUnknownUser()
    {
        Assert.False(await _users.IsAdminAsync(42));
    }
}
