using Buscarr.Server.Downloads;

namespace Buscarr.Server.Tests.Downloads;

public class RetryScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1991-09-24")]
    [InlineData("2026-10-17")]
    [InlineData("2026")]
    [InlineData("data inválida")]
    [InlineData("2026-99")]
    public void FirstAttempt_IsNowForAlbumsAlreadyReleased(string? firstReleaseDate)
    {
        Assert.Equal(Now, RetrySchedule.FirstAttempt(Now, firstReleaseDate));
    }

    [Theory]
    [InlineData("2026-11-05", "2026-11-06")]
    [InlineData("2026-11", "2026-11-02")]
    [InlineData("2027", "2027-01-02")]
    public void FirstAttempt_WaitsForTheDayAfterAFutureRelease(string firstReleaseDate, string expected)
    {
        var attempt = RetrySchedule.FirstAttempt(Now, firstReleaseDate);

        Assert.Equal(DateTimeOffset.Parse($"{expected}T00:00:00Z"), attempt);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 7)]
    [InlineData(10, 7)]
    public void NextRetry_SpreadsTheAttemptsOverDaysUpToAWeek(int attempts, int days)
    {
        Assert.Equal(Now.AddDays(days), RetrySchedule.NextRetry(Now, attempts));
    }

    [Fact]
    public void AfterBusy_WaitsFiveMinutes()
    {
        Assert.Equal(Now.AddMinutes(5), RetrySchedule.AfterBusy(Now));
    }

    [Fact]
    public void AfterUnfinished_WaitsOneHour()
    {
        Assert.Equal(Now.AddHours(1), RetrySchedule.AfterUnfinished(Now));
    }
}
