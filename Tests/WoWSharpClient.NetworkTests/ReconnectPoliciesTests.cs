using WoWSharpClient.Networking.Implementation;

namespace WowSharpClient.NetworkTests;

public class ExponentialBackoffPolicyTests
{
    [Fact]
    public void FirstAttempt_ReturnsInitialDelay()
    {
        var policy = new ExponentialBackoffPolicy(
            maxAttempts: 10,
            initialDelay: TimeSpan.FromSeconds(2));

        var delay = policy.GetDelay(1, null);

        Assert.NotNull(delay);
        Assert.Equal(TimeSpan.FromSeconds(2), delay.Value);
    }

    [Fact]
    public void SecondAttempt_ReturnsDoubled()
    {
        var policy = new ExponentialBackoffPolicy(
            maxAttempts: 10,
            initialDelay: TimeSpan.FromSeconds(1),
            backoffMultiplier: 2.0);

        var delay = policy.GetDelay(2, null);

        // 1 * 2^(2-1) = 2
        Assert.NotNull(delay);
        Assert.Equal(TimeSpan.FromSeconds(2), delay.Value);
    }

    [Fact]
    public void ThirdAttempt_ReturnsQuadrupled()
    {
        var policy = new ExponentialBackoffPolicy(
            maxAttempts: 10,
            initialDelay: TimeSpan.FromSeconds(1),
            backoffMultiplier: 2.0);

        var delay = policy.GetDelay(3, null);

        // 1 * 2^(3-1) = 4
        Assert.NotNull(delay);
        Assert.Equal(TimeSpan.FromSeconds(4), delay.Value);
    }

    [Fact]
    public void FifthAttempt_ExponentialGrowth()
    {
        var policy = new ExponentialBackoffPolicy(
            maxAttempts: 10,
            initialDelay: TimeSpan.FromSeconds(1),
            backoffMultiplier: 2.0);

        var delay = policy.GetDelay(5, null);

        // 1 * 2^(5-1) = 16
        Assert.NotNull(delay);
        Assert.Equal(TimeSpan.FromSeconds(16), delay.Value);
    }

    [Fact]
    public void DelayCappedAtMaxDelay()
    {
        var policy = new ExponentialBackoffPolicy(
            maxAttempts: 100,
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromSeconds(30),
            backoffMultiplier: 2.0);

        // 2^9 = 512 seconds, should be capped at 30
        var delay = policy.GetDelay(10, null);

        Assert.NotNull(delay);
        Assert.Equal(TimeSpan.FromSeconds(30), delay.Value);
    }

    [Fact]
    public void ExceedsMaxAttempts_ReturnsNull()
    {
        var policy = new ExponentialBackoffPolicy(maxAttempts: 3);

        Assert.NotNull(policy.GetDelay(1, null));
        Assert.NotNull(policy.GetDelay(2, null));
        Assert.NotNull(policy.GetDelay(3, null));
        Assert.Null(policy.GetDelay(4, null));
    }

    [Fact]
    public void ExactlyMaxAttempts_StillReturnsDelay()
    {
        var policy = new ExponentialBackoffPolicy(maxAttempts: 5);

        var delay = policy.GetDelay(5, null);

        Assert.NotNull(delay);
    }

    [Fact]
    public void ZeroMaxAttempts_Unlimited()
    {
        var policy = new ExponentialBackoffPolicy(
            maxAttempts: 0,
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromSeconds(10));

        // Should never return null for any attempt count
        Assert.NotNull(policy.GetDelay(100, null));
        Assert.NotNull(policy.GetDelay(1000, null));
    }

    [Fact]
    public void DefaultParameters_ReasonableValues()
    {
        var policy = new ExponentialBackoffPolicy();

        var first = policy.GetDelay(1, null);
        Assert.NotNull(first);
        Assert.Equal(TimeSpan.FromSeconds(1), first.Value);

        // Max attempts defaults to 10
        Assert.Null(policy.GetDelay(11, null));
    }

    [Fact]
    public void BackoffMultiplierAtOrBelowOne_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ExponentialBackoffPolicy(backoffMultiplier: 1.0));

        Assert.Throws<ArgumentException>(() =>
            new ExponentialBackoffPolicy(backoffMultiplier: 0.5));
    }

    [Fact]
    public void CustomMultiplier_ThreeX()
    {
        var policy = new ExponentialBackoffPolicy(
            maxAttempts: 10,
            initialDelay: TimeSpan.FromSeconds(1),
            backoffMultiplier: 3.0);

        // 1 * 3^(3-1) = 9
        var delay = policy.GetDelay(3, null);
        Assert.NotNull(delay);
        Assert.Equal(TimeSpan.FromSeconds(9), delay.Value);
    }

    [Fact]
    public void LastError_PassedButIgnored()
    {
        var policy = new ExponentialBackoffPolicy(maxAttempts: 5);
        var error = new Exception("test error");

        var delay = policy.GetDelay(1, error);

        Assert.NotNull(delay);
    }
}

public class FixedDelayPolicyTests
{
    [Fact]
    public void FirstAttempt_ReturnsFixedDelay()
    {
        var policy = new FixedDelayPolicy(TimeSpan.FromSeconds(5));

        var delay = policy.GetDelay(1, null);

        Assert.NotNull(delay);
        Assert.Equal(TimeSpan.FromSeconds(5), delay.Value);
    }

    [Fact]
    public void MultipleAttempts_AlwaysSameDelay()
    {
        var policy = new FixedDelayPolicy(TimeSpan.FromSeconds(3));

        for (int i = 1; i <= 10; i++)
        {
            var delay = policy.GetDelay(i, null);
            Assert.NotNull(delay);
            Assert.Equal(TimeSpan.FromSeconds(3), delay.Value);
        }
    }

    [Fact]
    public void DefaultMaxAttempts_Unlimited()
    {
        var policy = new FixedDelayPolicy(TimeSpan.FromSeconds(1));

        Assert.NotNull(policy.GetDelay(100, null));
        Assert.NotNull(policy.GetDelay(10000, null));
    }

    [Fact]
    public void ExceedsMaxAttempts_ReturnsNull()
    {
        var policy = new FixedDelayPolicy(TimeSpan.FromSeconds(1), maxAttempts: 3);

        Assert.NotNull(policy.GetDelay(1, null));
        Assert.NotNull(policy.GetDelay(3, null));
        Assert.Null(policy.GetDelay(4, null));
    }

    [Fact]
    public void ZeroDelay_ReturnsZero()
    {
        var policy = new FixedDelayPolicy(TimeSpan.Zero);

        var delay = policy.GetDelay(1, null);

        Assert.NotNull(delay);
        Assert.Equal(TimeSpan.Zero, delay.Value);
    }

    [Fact]
    public void LastError_PassedButIgnored()
    {
        var policy = new FixedDelayPolicy(TimeSpan.FromSeconds(1));
        var error = new InvalidOperationException("socket error");

        var delay = policy.GetDelay(1, error);

        Assert.NotNull(delay);
        Assert.Equal(TimeSpan.FromSeconds(1), delay.Value);
    }
}
