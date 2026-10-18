using Buscarr.Server.Import;

namespace Buscarr.Server.Settings;

public sealed record ServerSettings
{
    public const int SingletonId = 1;

    public int Id { get; init; } = SingletonId;
    public string? SlskdUrl { get; init; }
    public string? SlskdApiKey { get; init; }
    public string? DownloadsPath { get; init; }
    public string? LibraryPath { get; init; }
    public string? RejectedPath { get; init; }
    public string? SubsonicUrl { get; init; }
    public string? SubsonicUser { get; init; }
    public string? SubsonicPassword { get; init; }
    public string? FanartTvApiKey { get; init; }
    public string? SpotifyClientId { get; init; }
    public string? SpotifyClientSecret { get; init; }

    public ImportOptions Import => new(LibraryPath, DownloadsPath, RejectedPath);
}
