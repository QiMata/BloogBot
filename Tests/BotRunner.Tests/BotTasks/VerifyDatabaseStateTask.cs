using Tests.Infrastructure.BotTasks;

namespace BotRunner.Tests.BotTasks;

/// <summary>
/// BotTask that verifies the MaNGOS database contains valid character state.
/// Reads character position from the database and validates it against known bounds.
/// This task requires a running MySQL server with MaNGOS databases.
/// </summary>
public class VerifyDatabaseStateTask : TestBotTask
{
    private readonly string _connectionString;
    private readonly string _characterName;

    public VerifyDatabaseStateTask(string connectionString, string characterName)
        : base("VerifyDatabaseState")
    {
        _connectionString = connectionString;
        _characterName = characterName;
        Timeout = TimeSpan.FromSeconds(10);
    }

    public override void Update()
    {
        try
        {
            using var connection = new MySql.Data.MySqlClient.MySqlConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT position_x, position_y, position_z, map FROM characters.characters WHERE name = @name LIMIT 1";
            cmd.Parameters.AddWithValue("@name", _characterName);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                Fail($"Character '{_characterName}' not found in database");
                return;
            }

            float x = reader.GetFloat(0);
            float y = reader.GetFloat(1);
            float z = reader.GetFloat(2);
            int mapId = reader.GetInt32(3);

            // Validate position is within world bounds (WoW maps are roughly -17066 to 17066)
            const float maxCoord = 17100f;
            if (MathF.Abs(x) > maxCoord || MathF.Abs(y) > maxCoord)
            {
                Fail($"Character position ({x:F1}, {y:F1}) is outside world bounds");
                return;
            }

            if (z < -5000f || z > 5000f)
            {
                Fail($"Character Z={z:F1} is outside reasonable bounds");
                return;
            }

            if (mapId < 0)
            {
                Fail($"Character mapId={mapId} is invalid");
                return;
            }

            Complete();
        }
        catch (Exception ex)
        {
            Fail($"Database query failed: {ex.Message}");
        }
    }
}
