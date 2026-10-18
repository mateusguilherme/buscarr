using Buscarr.Server.Downloads;
using Buscarr.Server.Lists;
using Buscarr.Server.Settings;
using Buscarr.Server.Users;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Data;

public class BuscarrDbContext(DbContextOptions<BuscarrDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<ListedArtist> ListedArtists => Set<ListedArtist>();

    public DbSet<DownloadRequest> DownloadRequests => Set<DownloadRequest>();

    public DbSet<ServerSettings> ServerSettings => Set<ServerSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Email).HasMaxLength(254).UseCollation("NOCASE");
            user.Property(u => u.Name).HasMaxLength(256);
            user.Property(u => u.PasswordHash).HasMaxLength(256);
            user.Property(u => u.EditionPreference).HasMaxLength(32);
            user.Property(u => u.QualityPreference).HasMaxLength(32);
            user.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<ListedArtist>(artist =>
        {
            artist.HasKey(a => new { a.UserId, a.ArtistId });
            artist.Property(a => a.ArtistId).HasMaxLength(36);
            artist.Property(a => a.Name).HasMaxLength(256);
            artist.Property(a => a.Disambiguation).HasMaxLength(256);
            artist.Property(a => a.Area).HasMaxLength(256);
            artist.Property(a => a.Image).HasMaxLength(2048);
            artist.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DownloadRequest>(request =>
        {
            request.HasIndex(r => new { r.ReleaseGroupId, r.TrackCount, r.Quality }).IsUnique();
            request.Property(r => r.ArtistId).HasMaxLength(36);
            request.Property(r => r.ArtistName).HasMaxLength(256);
            request.Property(r => r.ReleaseGroupId).HasMaxLength(36);
            request.Property(r => r.Title).HasMaxLength(512);
            request.Property(r => r.Type).HasMaxLength(32);
            request.Property(r => r.FirstReleaseDate).HasMaxLength(10);
            request.Property(r => r.EditionReleaseId).HasMaxLength(36);
            request.Property(r => r.Quality).HasMaxLength(32);
            request.Property(r => r.CoverUrl).HasMaxLength(2048);
            request.Property(r => r.Failure).HasMaxLength(2048);
            request.Property(r => r.Username).HasMaxLength(256);
            request.Property(r => r.Directory).HasMaxLength(1024);
            request.Property(r => r.NextAttemptAt).HasConversion(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value));
        });

        modelBuilder.Entity<ServerSettings>(settings =>
        {
            settings.Property(s => s.Id).ValueGeneratedNever();
            settings.Property(s => s.SlskdUrl).HasMaxLength(2048);
            settings.Property(s => s.SlskdApiKey).HasMaxLength(256);
            settings.Property(s => s.DownloadsPath).HasMaxLength(1024);
            settings.Property(s => s.LibraryPath).HasMaxLength(1024);
            settings.Property(s => s.RejectedPath).HasMaxLength(1024);
            settings.Property(s => s.SubsonicUrl).HasMaxLength(2048);
            settings.Property(s => s.SubsonicUser).HasMaxLength(256);
            settings.Property(s => s.SubsonicPassword).HasMaxLength(256);
            settings.Property(s => s.FanartTvApiKey).HasMaxLength(256);
            settings.Property(s => s.SpotifyClientId).HasMaxLength(256);
            settings.Property(s => s.SpotifyClientSecret).HasMaxLength(256);
            settings.Ignore(s => s.Import);
        });
    }
}
