using System.Text.Json;
using MySqlConnector;

// GameObjectExporter: Queries vmangos DB for gameobject spawns and exports
// to JSON for consumption by the navmesh bake (MmapGen) and the runtime
// SceneCacheBuilder.
//
// Usage (backwards-compatible — current callers keep working):
//   GameObjectExporter [<connectionString>] [<outputPath>]
//
// Phase 4 variant-aware usage:
//   GameObjectExporter --variant base [--connection ...] [--out ...]
//
// When --variant is supplied, spawns are filtered by event/pool linkage:
//   variant=base: NOT EXISTS in game_event_gameobject AND NOT EXISTS in pool_gameobject
//                 (i.e. always-on spawns; the foundation set Phase A consumes)
// Other variant names are reserved for Phase B (per-event filters land in the
// MmapGen variant manifest, not in this CLI).

const string defaultConnection = "Server=127.0.0.1;Database=mangos;User=root;Password=root;";

string? connectionString = null;
string? outputPath = null;
string? variant = null;

// First pass: extract --flag args, leave positional args.
var positional = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    var a = args[i];
    switch (a)
    {
        case "--connection" when i + 1 < args.Length:
            connectionString = args[++i];
            break;
        case "--out" when i + 1 < args.Length:
            outputPath = args[++i];
            break;
        case "--variant" when i + 1 < args.Length:
            variant = args[++i];
            break;
        case "--help" or "-h" or "/?":
            PrintUsage();
            return 0;
        default:
            if (a.StartsWith("--"))
            {
                Console.Error.WriteLine($"Unknown flag: {a}");
                PrintUsage();
                return 2;
            }
            positional.Add(a);
            break;
    }
}

// Backwards-compat: positional args still work as <connectionString> [<outputPath>].
if (positional.Count >= 1 && connectionString is null) connectionString = positional[0];
if (positional.Count >= 2 && outputPath is null) outputPath = positional[1];

connectionString ??= defaultConnection;

if (variant is not null)
{
    if (string.IsNullOrWhiteSpace(variant) || variant.Contains('/') || variant.Contains('\\'))
    {
        Console.Error.WriteLine($"Invalid --variant '{variant}': must be a non-empty name without path separators.");
        return 2;
    }
    outputPath ??= Path.Combine("gameobject_spawns", variant + ".json");
}
outputPath ??= "gameobject_spawns.json";

// Filter clause for the variant. Empty string = no filter (legacy behavior).
string variantFilter = variant switch
{
    null => string.Empty,
    "base" =>
        " AND NOT EXISTS (SELECT 1 FROM game_event_gameobject WHERE guid = g.guid)" +
        " AND NOT EXISTS (SELECT 1 FROM pool_gameobject WHERE guid = g.guid)",
    _ =>
        // For any non-base variant the CLI does not yet know the per-event
        // filter — those land via the Phase B manifest. Fail fast so callers
        // don't accidentally produce a duplicate of base.json.
        throw new ArgumentException(
            $"--variant '{variant}' is not supported by this tool. " +
            "Phase A only knows 'base'; per-event variants come from the MmapGen variant manifest.")
};

Console.WriteLine($"Connecting to: {connectionString}");
Console.WriteLine($"Output: {outputPath}");
Console.WriteLine(variant is null
    ? "Filter: none (legacy full export)"
    : $"Variant: {variant}");

// Ensure output directory exists.
var outDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
    Directory.CreateDirectory(outDir);

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

// gameobject_template has a composite PK (entry, patch) — use MAX(patch) for the latest.
// gameobject.guid is the spawn-unique key (joined to game_event_gameobject / pool_gameobject by guid).
string sql =
    "SELECT g.map, g.position_x, g.position_y, g.position_z, g.orientation," +
    "       gt.displayId, gt.size " +
    "FROM gameobject g " +
    "JOIN gameobject_template gt ON g.id = gt.entry " +
    "WHERE gt.displayId != 0 " +
    "  AND gt.patch = (SELECT MAX(gt2.patch) FROM gameobject_template gt2 WHERE gt2.entry = gt.entry)" +
    variantFilter +
    " ORDER BY g.map, g.position_x";

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

foreach (var (map, spawns) in spawnsByMap.OrderByDescending(kv => kv.Value.Count).Take(10))
    Console.WriteLine($"  Map {map}: {spawns.Count} spawns");

return 0;

static void PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  GameObjectExporter [<connectionString>] [<outputPath>]");
    Console.Error.WriteLine("  GameObjectExporter [--connection <str>] [--out <path>] [--variant <name>]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Variants:");
    Console.Error.WriteLine("  base    Always-on spawns: NOT EXISTS in game_event_gameobject AND pool_gameobject.");
    Console.Error.WriteLine("          Default output: gameobject_spawns/base.json");
    Console.Error.WriteLine();
    Console.Error.WriteLine("With no flags, exports the full spawn set to gameobject_spawns.json (legacy behavior).");
}
