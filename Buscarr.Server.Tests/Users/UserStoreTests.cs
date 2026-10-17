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
        var user = await _users.CreateAsync("fulano@exemplo.com", "Fulano", "segredo123");

        Assert.NotNull(user);
        Assert.NotEqual(0, user.Id);
        Assert.NotEqual("segredo123", user.PasswordHash);
        Assert.NotEmpty(user.PasswordHash);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNullWhenEmailExistsIgnoringCase()
    {
        await _users.CreateAsync("fulano@exemplo.com", "Fulano", "segredo123");

        var duplicate = await _users.CreateAsync("FULANO@exemplo.com", "Outro", "outrasenha");

        Assert.Null(duplicate);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsUserForCorrectPasswordIgnoringEmailCase()
    {
        await _users.CreateAsync("fulano@exemplo.com", "Fulano", "segredo123");

        var user = await _users.VerifyAsync("Fulano@Exemplo.com", "segredo123");

        Assert.Equal("Fulano", user?.Name);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNullForWrongPassword()
    {
        await _users.CreateAsync("fulano@exemplo.com", "Fulano", "segredo123");

        var user = await _users.VerifyAsync("fulano@exemplo.com", "errada123");

        Assert.Null(user);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNullForUnknownEmail()
    {
        var user = await _users.VerifyAsync("ninguem@exemplo.com", "segredo123");

        Assert.Null(user);
    }
}
