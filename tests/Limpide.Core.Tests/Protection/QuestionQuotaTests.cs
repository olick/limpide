using Limpide.Core.Protection;

namespace Limpide.Core.Tests.Protection;

public class QuestionQuotaTests
{
    private readonly ManualClock clock = new(new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Twenty_quick_questions_from_one_visitor_only_ten_pass()
    {
        var quota = new QuestionQuota(new QuotaOptions(PerClientPerHour: 10, GlobalPerDay: 100), clock);

        var decisions = Enumerable.Range(0, 20).Select(_ => quota.TryConsume("203.0.113.7")).ToList();

        Assert.Equal(10, decisions.Count(d => d.Allowed));
        Assert.All(decisions.Skip(10), d => Assert.Equal(QuotaVerdict.ClientLimitReached, d.Verdict));
        Assert.Equal(TimeSpan.FromHours(1), decisions[10].RetryAfter);
        Assert.Equal(0, decisions[9].Remaining);
    }

    [Fact]
    public void The_visitor_window_reopens_after_an_hour()
    {
        var quota = new QuestionQuota(new QuotaOptions(PerClientPerHour: 2, GlobalPerDay: 100), clock);
        quota.TryConsume("a");
        clock.Advance(TimeSpan.FromMinutes(40));
        quota.TryConsume("a");

        Assert.Equal(TimeSpan.FromMinutes(20), quota.TryConsume("a").RetryAfter);

        clock.Advance(TimeSpan.FromMinutes(20));
        Assert.True(quota.TryConsume("a").Allowed);
    }

    [Fact]
    public void Visitors_are_counted_separately()
    {
        var quota = new QuestionQuota(new QuotaOptions(PerClientPerHour: 1, GlobalPerDay: 100), clock);

        Assert.True(quota.TryConsume("a").Allowed);
        Assert.False(quota.TryConsume("a").Allowed);
        Assert.True(quota.TryConsume("b").Allowed);
    }

    [Fact]
    public void The_daily_ceiling_holds_against_many_addresses_until_midnight_utc()
    {
        var quota = new QuestionQuota(new QuotaOptions(PerClientPerHour: 10, GlobalPerDay: 3), clock);
        for (var i = 0; i < 3; i++)
            Assert.True(quota.TryConsume($"adresse-{i}").Allowed);

        var refused = quota.TryConsume("adresse-nouvelle");

        Assert.Equal(QuotaVerdict.GlobalLimitReached, refused.Verdict);
        Assert.Equal(TimeSpan.FromHours(14), refused.RetryAfter); // 10 h UTC → minuit UTC

        clock.Advance(TimeSpan.FromHours(14));
        Assert.True(quota.TryConsume("adresse-nouvelle").Allowed);
    }

    [Fact]
    public void A_refused_question_consumes_nothing()
    {
        var quota = new QuestionQuota(new QuotaOptions(PerClientPerHour: 1, GlobalPerDay: 2), clock);
        quota.TryConsume("a");
        quota.TryConsume("a"); // refusée par la limite du visiteur : ne doit pas entamer le plafond global

        Assert.True(quota.TryConsume("b").Allowed);
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now += duration;
    }
}
