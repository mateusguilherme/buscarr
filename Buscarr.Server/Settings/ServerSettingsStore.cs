using Buscarr.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Settings;

public sealed class ServerSettingsStore
{
    private ServerSettings _current = new();

    public ServerSettings Current => Volatile.Read(ref _current);

    public async Task LoadAsync(BuscarrDbContext db, IConfiguration configuration)
    {
        var saved = await db.ServerSettings.AsNoTracking().SingleOrDefaultAsync();

        if (saved is null)
        {
            saved = FromConfiguration(configuration);
            db.ServerSettings.Add(saved);
            await db.SaveChangesAsync();
        }

        Volatile.Write(ref _current, saved);
    }

    public async Task SaveAsync(BuscarrDbContext db, ServerSettings settings)
    {
        var stored = settings with { Id = ServerSettings.SingletonId };

        var existing = await db.ServerSettings.FindAsync(ServerSettings.SingletonId);

        if (existing is null)
        {
            db.ServerSettings.Add(stored);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(stored);
        }

        await db.SaveChangesAsync();
        Volatile.Write(ref _current, stored);
    }

    private static ServerSettings FromConfiguration(IConfiguration configuration)
    {
        string? Value(string key) =>
            string.IsNullOrWhiteSpace(configuration[key]) ? null : configuration[key]!.Trim();

        return new ServerSettings
        {
            SlskdUrl = Value("Slskd:Url"),
            SlskdApiKey = Value("Slskd:ApiKey"),
            DownloadsPath = Value("Slskd:DownloadsPath"),
            LibraryPath = Value("Library:Path"),
            RejectedPath = Value("Library:RejectedPath"),
            SubsonicUrl = Value("Subsonic:Url"),
            SubsonicUser = Value("Subsonic:User"),
            SubsonicPassword = Value("Subsonic:Password"),
            FanartTvApiKey = Value("FanartTv:ApiKey"),
        };
    }
}
