using BotRunner.Tests.BotTasks;
using BotRunner.Tests.Fixtures;
using Tests.Infrastructure;

namespace BotRunner.Tests;

/// <summary>
/// Integration tests that verify BotTasks against the MaNGOS server stack.
/// These tests auto-launch MaNGOS if needed and skip gracefully when unavailable.
/// </summary>
[RequiresMangosStack]
public class BotTaskIntegrationTests(MangosStackFixture fixture) : IClassFixture<MangosStackFixture>
{
    private readonly MangosStackFixture _fixture = fixture;

    [Fact]
    public void VerifyDatabaseState_ShouldFindCharacterInDatabase()
    {
        global::Tests.Infrastructure.Skip.IfNot(_fixture.IsAvailable, "MaNGOS stack not available");

        var connectionString = $"Server=127.0.0.1;Database=characters;Uid=mangos;Pwd=mangos;Port={_fixture.Config.MySqlPort};";
        var task = new VerifyDatabaseStateTask(connectionString, "Dralrahgra");
        task.Update();

        // This test may legitimately fail if the character doesn't exist yet
        // That's a valid test failure, not an infrastructure issue
        task.AssertSuccess();
    }
}
