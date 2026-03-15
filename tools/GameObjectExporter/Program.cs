using System.Text.Json;
using MySqlConnector;

// GameObjectExporter: Queries vmangos DB for gameobject spawns and exports
// to JSON for consumption by the MoveMapGenerator navmesh builder.

var connectionString = args.Length > 0 ? args[0] : "Server=127.0.0.1;Database=mangos;User=root;Password=root;";
var outputPath = args.Length > 1 ? args[1] : "gameobject_spawns.json";

Console.WriteLine($"Connecting to: {connectionString}");
Console.WriteLine($"Output: {outputPath}");

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

// Query gameobject spawns joined with template for displayId and size.
// gameobject_template has a composite PK (entry, patch) — use MAX(patch) to get latest.
const string sql = """
    SELECT g.map, g.position_x, g.position_y, g.position_z, g.orientation,
           gt.displayId, gt.size
    FROM gameobject g
    JOIN gameobject_template gt ON g.id = gt.entry
    WHERE gt.displayId != 0
      AND gt.patch = (SELECT MAX(gt2.patch) FROM gameobject_template gt2 WHERE gt2.entry = gt.entry)
    ORDER BY g.map, g.position_x
    """;

await using var cmd = new MySqlCommand(sql, connection);
await using var reader = await cmd.ExecuteReaderAsync();

var spawnsByMap = new Dictionary<uint, List<object>>();
var totalCount = 0;

while (await reader.ReadAsync())
{
    var map = reader.GetUInt32(0);
    var spawn = new
    {
        displayId = reader.GetUInt32(5),
        x = MathF.Round(reader.GetFloat(1), 4),
        y = MathF.Round(reader.GetFloat(2), 4),
        z = MathF.Round(reader.GetFloat(3), 4),
        o = MathF.Round(reader.GetFloat(4), 4),
        s = MathF.Round(reader.GetFloat(6), 4)
    };

    if (!spawnsByMap.TryGetValue(map, out var list))
    {
        list = new List<object>();
        spawnsByMap[map] = list;
    }
    list.Add(spawn);
    totalCount++;
}

// Write JSON grouped by map ID (keys as strings for nlohmann::json compatibility)
var jsonDict = new Dictionary<string, object>();
foreach (var (map, spawns) in spawnsByMap.OrderBy(kv => kv.Key))
    jsonDict[map.ToString()] = spawns;

var options = new JsonSerializerOptions { WriteIndented = false };
var json = JsonSerializer.Serialize(jsonDict, options);
await File.WriteAllTextAsync(outputPath, json);

Console.WriteLine($"Exported {totalCount} gameobject spawns across {spawnsByMap.Count} maps to {outputPath}");
Console.WriteLine($"File size: {new FileInfo(outputPath).Length / 1024}KB");

// Print per-map breakdown
foreach (var (map, spawns) in spawnsByMap.OrderByDescending(kv => kv.Value.Count).Take(10))
    Console.WriteLine($"  Map {map}: {spawns.Count} spawns");
