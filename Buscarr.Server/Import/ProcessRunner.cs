using System.Diagnostics;

namespace Buscarr.Server.Import;

public sealed record ProcessResult(int ExitCode, string Output, string Error, bool TimedOut);

public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(string tool, IReadOnlyList<string> arguments, TimeSpan limit)
    {
        using var process = new Process();
        process.StartInfo.FileName = tool;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            process.Start();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"erro ao rodar o {tool}: {e.Message}");

            return new(-1, "", "", false);
        }

        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();

        using (var expiry = new CancellationTokenSource(limit))
        {
            try
            {
                await process.WaitForExitAsync(expiry.Token);
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine($"o {tool} excedeu o tempo limite de {limit} e foi encerrado");
                Kill(process);
                await process.WaitForExitAsync();

                return new(-1, await output, await error, true);
            }
        }

        return new(process.ExitCode, await output, await error, false);
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
