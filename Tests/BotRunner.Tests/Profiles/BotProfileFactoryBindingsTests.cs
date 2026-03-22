using BotProfiles.Common;
using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using Moq;

namespace BotRunner.Tests.Profiles;

/// <summary>
/// Guards that all BotBase profile subclasses wire factory methods correctly.
/// Prevents regressions like BP-MISS-001 where PvP factories returned PvE tasks.
/// </summary>
public class BotProfileFactoryBindingsTests
{
    private static readonly IBotContext MockContext = CreateMockContext();

    /// <summary>
    /// All BotBase subclasses must return a PvPRotationTask (not PvERotationTask) from CreatePvPRotationTask.
    /// Some constructors may throw due to missing runtime dependencies — those are skipped
    /// but the cross-wiring check still applies to all that succeed.
    /// </summary>
    [Fact]
    public void AllProfiles_CreatePvPRotationTask_DoesNotReturnPvERotationTask()
    {
        var profileTypes = GetAllBotBaseSubclasses();
        Assert.NotEmpty(profileTypes);

        int checkedCount = 0;
        foreach (var profileType in profileTypes)
        {
            var profile = (BotBase)Activator.CreateInstance(profileType)!;
            IBotTask task;
            try
            {
                task = profile.CreatePvPRotationTask(MockContext);
            }
            catch (NullReferenceException)
            {
                // Some constructors access deep context (EventHandler, etc.) — skip
                continue;
            }

            Assert.NotNull(task);
            checkedCount++;

            var taskTypeName = task.GetType().Name;
            Assert.False(
                taskTypeName.Contains("PvE", StringComparison.OrdinalIgnoreCase),
                $"Profile {profile.Name} ({profileType.FullName}): CreatePvPRotationTask returned " +
                $"'{taskTypeName}' which appears to be a PvE task. Expected a PvP task type.");
        }

        // At least half the profiles should be checkable with our mock
        Assert.True(checkedCount >= profileTypes.Count / 2,
            $"Only {checkedCount}/{profileTypes.Count} profiles were checkable. Mock context may be insufficient.");
    }

    /// <summary>
    /// All BotBase subclasses must return a PvERotationTask (not PvPRotationTask) from CreatePvERotationTask.
    /// </summary>
    [Fact]
    public void AllProfiles_CreatePvERotationTask_DoesNotReturnPvPRotationTask()
    {
        var profileTypes = GetAllBotBaseSubclasses();

        int checkedCount = 0;
        foreach (var profileType in profileTypes)
        {
            var profile = (BotBase)Activator.CreateInstance(profileType)!;
            IBotTask task;
            try
            {
                task = profile.CreatePvERotationTask(MockContext);
            }
            catch (NullReferenceException)
            {
                continue;
            }

            Assert.NotNull(task);
            checkedCount++;

            var taskTypeName = task.GetType().Name;
            Assert.False(
                taskTypeName.Contains("PvP", StringComparison.OrdinalIgnoreCase),
                $"Profile {profile.Name} ({profileType.FullName}): CreatePvERotationTask returned " +
                $"'{taskTypeName}' which appears to be a PvP task. Expected a PvE task type.");
        }

        Assert.True(checkedCount >= profileTypes.Count / 2,
            $"Only {checkedCount}/{profileTypes.Count} profiles were checkable. Mock context may be insufficient.");
    }

    /// <summary>
    /// Ensures all expected profiles are discovered by reflection.
    /// </summary>
    [Fact]
    public void ProfileDiscovery_FindsExpectedProfileCount()
    {
        var profileTypes = GetAllBotBaseSubclasses();

        // 27 profiles: 3 Warrior + 3 Paladin + 3 Hunter + 3 Rogue + 3 Priest
        //            + 3 Shaman + 3 Mage + 3 Warlock + 3 Druid = 27
        Assert.True(
            profileTypes.Count >= 27,
            $"Expected at least 27 BotBase subclasses but found {profileTypes.Count}. " +
            "If profiles were removed, update this threshold.");
    }

    /// <summary>
    /// Each profile must have a non-empty Name and FileName.
    /// </summary>
    [Fact]
    public void AllProfiles_HaveValidNameAndFileName()
    {
        var profileTypes = GetAllBotBaseSubclasses();

        foreach (var profileType in profileTypes)
        {
            var profile = (BotBase)Activator.CreateInstance(profileType)!;
            Assert.False(string.IsNullOrWhiteSpace(profile.Name),
                $"{profileType.FullName} has null/empty Name.");
            Assert.False(string.IsNullOrWhiteSpace(profile.FileName),
                $"{profileType.FullName} has null/empty FileName.");
        }
    }

    private static List<Type> GetAllBotBaseSubclasses()
    {
        var botProfileAssembly = typeof(BotBase).Assembly;
        return botProfileAssembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(BotBase)) && !t.IsAbstract)
            .OrderBy(t => t.FullName)
            .ToList();
    }

    private static IBotContext CreateMockContext()
    {
        var mockContext = new Mock<IBotContext>();

        var mockObjectManager = new Mock<IObjectManager>();
        mockContext.Setup(c => c.ObjectManager).Returns(mockObjectManager.Object);

        var mockPlayer = new Mock<IWoWLocalPlayer>();
        mockObjectManager.Setup(om => om.Player).Returns(mockPlayer.Object);

        var mockEventHandler = new Mock<IWoWEventHandler>();
        mockContext.Setup(c => c.EventHandler).Returns(mockEventHandler.Object);

        return mockContext.Object;
    }
}
