namespace Buscarr.Server.Import;

public sealed class Transcoder(IAudioTools tools)
{
    public async Task<List<string>?> ToMp3Async(IReadOnlyList<string> files)
    {
        var transcoded = new List<string>();

        foreach (var source in files)
        {
            var target = Path.ChangeExtension(source, ".mp3");

            if (!await tools.TranscodeToMp3Async(source, target))
            {
                return null;
            }

            transcoded.Add(target);
        }

        foreach (var source in files)
        {
            File.Delete(source);
        }

        return transcoded;
    }
}
