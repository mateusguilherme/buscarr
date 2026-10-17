using Buscarr.Server.Downloads;
using Buscarr.Server.Lists;
using Buscarr.Server.Users;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Data;

public class BuscarrDbContext(DbContextOptions<BuscarrDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<ListedArtist> ListedArtists => Set<ListedArtist>();

    public DbSet<DownloadRequest> DownloadRequests => Set<DownloadRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Email).UseCollation("NOCASE");
            user.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<ListedArtist>(artist =>
        {
            artist.HasKey(a => new { a.UserId, a.ArtistId });
            artist.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DownloadRequest>(request =>
        {
            request.HasIndex(r => new { r.ReleaseGroupId, r.TrackCount, r.Quality }).IsUnique();
            request.Property(r => r.NextAttemptAt).HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value));
        });
    }
}
