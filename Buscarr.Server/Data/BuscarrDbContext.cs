using Buscarr.Server.Users;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Data;

public class BuscarrDbContext(DbContextOptions<BuscarrDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Email).UseCollation("NOCASE");
            user.HasIndex(u => u.Email).IsUnique();
        });
    }
}
