namespace Buscarr.Server.Downloads;

public static class RetrySchedule
{
    public static DateTimeOffset FirstAttempt(DateTimeOffset now, string? firstReleaseDate)
    {
        var release = Parse(firstReleaseDate);

        return release > now ? release.Value.AddDays(1) : now;
    }

    public static DateTimeOffset NextRetry(DateTimeOffset now, int attempts) =>
        now.AddDays(attempts switch
        {
            <= 1 => 1,
            2 => 2,
            3 => 4,
            _ => 7,
        });

    public static DateTimeOffset AfterBusy(DateTimeOffset now) => now.AddMinutes(5);

    public static DateTimeOffset AfterUnfinished(DateTimeOffset now) => now.AddHours(1);

    private static DateTimeOffset? Parse(string? date)
    {
        var parts = (date ?? "").Split('-');
        var numbers = new int[3];

        for (var i = 0; i < 3; i++)
        {
            if (i < parts.Length && !int.TryParse(parts[i], out numbers[i]))
            {
                return null;
            }
        }

        try
        {
            return new DateTimeOffset(
                numbers[0],
                Math.Max(numbers[1], 1),
                Math.Max(numbers[2], 1),
                0,
                0,
                0,
                TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
