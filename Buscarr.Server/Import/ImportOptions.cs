namespace Buscarr.Server.Import;

public sealed record ImportOptions(string? LibraryPath, string? DownloadsPath, string? RejectedPath)
{
    public bool Enabled => !string.IsNullOrEmpty(LibraryPath) && !string.IsNullOrEmpty(DownloadsPath);

    public string Rejected =>
        string.IsNullOrEmpty(RejectedPath)
            ? Path.Combine(Path.GetDirectoryName(DownloadsPath!.TrimEnd('/', '\\'))!, "rejected")
            : RejectedPath;
}
