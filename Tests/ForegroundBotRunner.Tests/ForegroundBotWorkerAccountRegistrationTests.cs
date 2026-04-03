namespace ForegroundBotRunner.Tests;

public sealed class ForegroundBotWorkerAccountRegistrationTests
{
    [Fact]
    public void ResolveStateManagerRegistrationAccount_UsesExplicitConfiguredAccount()
    {
        var accountName = ForegroundBotWorker.ResolveStateManagerRegistrationAccount("WSGBOTA1");

        Assert.Equal("WSGBOTA1", accountName);
    }

    [Fact]
    public void ResolveStateManagerRegistrationAccount_FallsBackToLegacyWildcard()
    {
        var accountName = ForegroundBotWorker.ResolveStateManagerRegistrationAccount(null);

        Assert.Equal("?", accountName);
    }
}
