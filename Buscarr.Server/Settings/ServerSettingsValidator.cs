namespace Buscarr.Server.Settings;

public static class ServerSettingsValidator
{
    public static Dictionary<string, string> Validate(ServerSettings settings)
    {
        var errors = new Dictionary<string, string>();
        CheckUrl(errors, "slskdUrl", settings.SlskdUrl);
        CheckUrl(errors, "subsonicUrl", settings.SubsonicUrl);
        CheckUrl(errors, "ntfyUrl", settings.NtfyUrl);
        CheckFolder(errors, "downloadsPath", settings.DownloadsPath, mustExist: true);
        CheckFolder(errors, "libraryPath", settings.LibraryPath, mustExist: true);
        CheckFolder(errors, "rejectedPath", settings.RejectedPath, mustExist: false);

        if (settings.LibraryPath is not null && settings.DownloadsPath is null)
        {
            errors["downloadsPath"] = "required";
        }

        return errors;
    }

    private static void CheckUrl(Dictionary<string, string> errors, string field, string? value)
    {
        if (value is null)
        {
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors[field] = "invalid-url";
        }
    }

    private static void CheckFolder(Dictionary<string, string> errors, string field, string? path, bool mustExist)
    {
        if (path is null)
        {
            return;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            errors[field] = "not-absolute";
        }
        else if (!Directory.Exists(path))
        {
            if (mustExist)
            {
                errors[field] = "not-found";
            }
        }
        else if (!IsWritable(path))
        {
            errors[field] = "not-writable";
        }
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            using var probe = File.Create(
                Path.Combine(directory, $".buscarr-{Guid.NewGuid():N}"),
                1,
                FileOptions.DeleteOnClose);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return false;
        }

        return true;
    }
}
