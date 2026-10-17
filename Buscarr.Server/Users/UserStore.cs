using Buscarr.Server.Data;
using Buscarr.Server.MusicBrainz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Users;

public class UserStore(BuscarrDbContext db)
{
    private static readonly PasswordHasher<User> Hasher = new();

    public async Task<User?> CreateAsync(string email, string name, string password)
    {
        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return null;
        }

        var user = new User { Email = email, Name = name };
        user.PasswordHash = Hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return user;
    }

    public async Task<string> EditionPreferenceAsync(int userId) =>
        await db.Users.Where(u => u.Id == userId).Select(u => u.EditionPreference).SingleAsync()
            ?? Editions.Original;

    public async Task SetEditionPreferenceAsync(int userId, string edition)
    {
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.EditionPreference, edition));
    }

    public async Task<User?> VerifyAsync(string email, string password)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            return null;
        }

        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, password);

        return result == PasswordVerificationResult.Failed ? null : user;
    }
}
