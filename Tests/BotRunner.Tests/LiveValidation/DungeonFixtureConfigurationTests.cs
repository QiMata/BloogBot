using System;
using BotRunner.Tests.LiveValidation.Dungeons;
using BotRunner.Tests.LiveValidation.Raids;
using BotRunner.Travel;
using WoWStateManager.Coordination;

namespace BotRunner.Tests.LiveValidation;

public sealed class DungeonFixtureConfigurationTests
{
    [Fact]
    public void DungeonFixtures_DisableForegroundPacketHooks_ForCrossMapTransfers()
    {
        var originalInjectionDisablePacketHooks = Environment.GetEnvironmentVariable("Injection__DisablePacketHooks");
        var originalDisablePacketHooks = Environment.GetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS");

        try
        {
            var fixture = new TestableStratholmeLivingFixture();

            fixture.ApplyCoordinatorEnvironment();

            Assert.Equal("true", Environment.GetEnvironmentVariable("Injection__DisablePacketHooks"));
            Assert.Equal("1", Environment.GetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Injection__DisablePacketHooks", originalInjectionDisablePacketHooks);
            Environment.SetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS", originalDisablePacketHooks);
        }
    }

    [Fact]
    public void DungeonFixtures_DefaultLeaderRunnerType_IsBackground()
    {
        Assert.Equal("Background", DungeonInstanceFixture.ResolveRunnerType(index: 0, useForegroundLeader: false));
        Assert.Equal("BG", DungeonInstanceFixture.ResolveExecutionLabel(index: 0, useForegroundLeader: false));
        Assert.Equal("AQ20BOT1", DungeonInstanceFixture.ResolveLeaderAccountName("AQ20BOT", useForegroundLeader: false));
        Assert.Equal("AQ20BOT1", DungeonInstanceFixture.ResolveAccountName(index: 0, accountPrefix: "AQ20BOT", useForegroundLeader: false));
        Assert.Equal("AQ20BOT2", DungeonInstanceFixture.ResolveAccountName(index: 1, accountPrefix: "AQ20BOT", useForegroundLeader: false));
    }

    [Fact]
    public void DungeonFixtures_CanStillOptIntoForegroundLeader_WhenExplicitlyRequested()
    {
        Assert.Equal("Foreground", DungeonInstanceFixture.ResolveRunnerType(index: 0, useForegroundLeader: true));
        Assert.Equal("FG", DungeonInstanceFixture.ResolveExecutionLabel(index: 0, useForegroundLeader: true));
        Assert.Equal("TESTBOT1", DungeonInstanceFixture.ResolveLeaderAccountName("AQ20BOT", useForegroundLeader: true));
        Assert.Equal("TESTBOT1", DungeonInstanceFixture.ResolveAccountName(index: 0, accountPrefix: "AQ20BOT", useForegroundLeader: true));
        Assert.Equal("Background", DungeonInstanceFixture.ResolveRunnerType(index: 1, useForegroundLeader: true));
        Assert.Equal("AQ20BOT2", DungeonInstanceFixture.ResolveAccountName(index: 1, accountPrefix: "AQ20BOT", useForegroundLeader: true));
    }

    [Fact]
    public void DungeonFixtures_PublishDungeonTarget_ForCoordinator()
    {
        var originalName = Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetNameEnvVar);
        var originalMap = Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetMapEnvVar);
        var originalX = Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetXEnvVar);
        var originalY = Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetYEnvVar);
        var originalZ = Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetZEnvVar);

        try
        {
            var fixture = new TestableStratholmeLivingFixture();

            fixture.ApplyCoordinatorEnvironment();

            Assert.Equal(DungeonEntryData.StratholmeLiving.Abbreviation, Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetNameEnvVar));
            Assert.Equal(DungeonEntryData.StratholmeLiving.InstanceMapId.ToString(), Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetMapEnvVar));
            Assert.Equal(DungeonEntryData.StratholmeLiving.InstanceEntryPosition.X.ToString(System.Globalization.CultureInfo.InvariantCulture), Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetXEnvVar));
            Assert.Equal(DungeonEntryData.StratholmeLiving.InstanceEntryPosition.Y.ToString(System.Globalization.CultureInfo.InvariantCulture), Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetYEnvVar));
            Assert.Equal(DungeonEntryData.StratholmeLiving.InstanceEntryPosition.Z.ToString(System.Globalization.CultureInfo.InvariantCulture), Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetZEnvVar));
        }
        finally
        {
            Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetNameEnvVar, originalName);
            Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetMapEnvVar, originalMap);
            Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetXEnvVar, originalX);
            Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetYEnvVar, originalY);
            Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetZEnvVar, originalZ);
        }
    }

    [Fact]
    public void RaidFixtures_ReuseDungeonTransferSafetyConfiguration()
    {
        var originalInjectionDisablePacketHooks = Environment.GetEnvironmentVariable("Injection__DisablePacketHooks");
        var originalDisablePacketHooks = Environment.GetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS");
        var originalName = Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetNameEnvVar);
        var originalMap = Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetMapEnvVar);

        try
        {
            var fixture = new TestableMoltenCoreFixture();

            fixture.ApplyCoordinatorEnvironment();

            Assert.Equal("true", Environment.GetEnvironmentVariable("Injection__DisablePacketHooks"));
            Assert.Equal("1", Environment.GetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS"));
            Assert.Equal(RaidEntryData.MoltenCore.Abbreviation, Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetNameEnvVar));
            Assert.Equal(RaidEntryData.MoltenCore.InstanceMapId.ToString(), Environment.GetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetMapEnvVar));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Injection__DisablePacketHooks", originalInjectionDisablePacketHooks);
            Environment.SetEnvironmentVariable("WWOW_DISABLE_PACKET_HOOKS", originalDisablePacketHooks);
            Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetNameEnvVar, originalName);
            Environment.SetEnvironmentVariable(DungeoneeringCoordinator.DungeonTargetMapEnvVar, originalMap);
        }
    }

    private sealed class TestableStratholmeLivingFixture : StratholmeLivingFixture
    {
        public void ApplyCoordinatorEnvironment() => ConfigureCoordinatorEnvironment();
    }

    private sealed class TestableMoltenCoreFixture : MoltenCoreFixture
    {
        public void ApplyCoordinatorEnvironment() => ConfigureCoordinatorEnvironment();
    }
}
