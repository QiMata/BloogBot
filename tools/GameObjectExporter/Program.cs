using System.Text.Json;
using MySqlConnector;

// GameObjectExporter: Queries vmangos DB for gameobject spawns and exports
// to JSON for consumption by the navmesh bake (MmapGen) and the runtime
// SceneCacheBuilder.
//
// Usage (backwards-compatible - current callers keep working):
//   GameObjectExporter [<connectionString>] [<outputPath>]
//
// Variant-aware usage:
//   GameObjectExporter --variant base [--connection ...] [--out ...]
//
// With --variant, the exporter emits one named delta set. Callers that need a
// composed world-state (for example base + harvest-festival + org-city-trophies)
// should export each named variant separately, then merge them by GUID.

const string DefaultConnection = "Server=127.0.0.1;Database=mangos;User=root;Password=root;";

var variantDefinitions = CreateVariantDefinitions();

string? connectionString = null;
string? outputPath = null;
string? variant = null;

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
            PrintUsage(variantDefinitions);
            return 0;
        default:
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Unknown flag: {a}");
                PrintUsage(variantDefinitions);
                return 2;
            }

            positional.Add(a);
            break;
    }
}

if (positional.Count >= 1 && connectionString is null)
{
    connectionString = positional[0];
}

if (positional.Count >= 2 && outputPath is null)
{
    outputPath = positional[1];
}

connectionString ??= DefaultConnection;

VariantDefinition? selectedVariant = null;
if (variant is not null)
{
    if (string.IsNullOrWhiteSpace(variant) || variant.Contains('/') || variant.Contains('\\'))
    {
        Console.Error.WriteLine($"Invalid --variant '{variant}': must be a non-empty name without path separators.");
        return 2;
    }

    if (!variantDefinitions.TryGetValue(variant.Trim(), out selectedVariant))
    {
        Console.Error.WriteLine($"Unsupported --variant '{variant}'.");
        Console.Error.WriteLine("Supported variants:");
        foreach (var definition in variantDefinitions.Values.OrderBy(v => v.Id, StringComparer.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"  {definition.Id}");
        }

        return 2;
    }

    outputPath ??= Path.Combine("gameobject_spawns", selectedVariant.Id + ".json");
}

outputPath ??= "gameobject_spawns.json";

Console.WriteLine($"Connecting to: {connectionString}");
Console.WriteLine($"Output: {outputPath}");
Console.WriteLine(selectedVariant is null
    ? "Filter: none (legacy full export)"
    : $"Variant: {selectedVariant.Id} - {selectedVariant.Description}");

var outDir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
{
    Directory.CreateDirectory(outDir);
}

await using var connection = new MySqlConnection(connectionString);
await connection.OpenAsync();

string sql =
    "SELECT g.guid, g.map, g.position_x, g.position_y, g.position_z, g.orientation," +
    "       gt.displayId, gt.size " +
    "FROM gameobject g " +
    "JOIN gameobject_template gt ON g.id = gt.entry " +
    "WHERE gt.displayId != 0 " +
    "  AND gt.patch = (SELECT MAX(gt2.patch) FROM gameobject_template gt2 WHERE gt2.entry = gt.entry)";

if (selectedVariant is not null)
{
    sql += selectedVariant.PredicateSql;
}

sql += " ORDER BY g.map, g.position_x, g.position_y, g.guid";

await using var cmd = new MySqlCommand(sql, connection);
await using var reader = await cmd.ExecuteReaderAsync();

var spawnsByMap = new Dictionary<uint, List<object>>();
var totalCount = 0;

while (await reader.ReadAsync())
{
    var map = reader.GetUInt32(1);
    var spawn = new
    {
        guid = reader.GetUInt32(0),
        displayId = reader.GetUInt32(6),
        x = MathF.Round(reader.GetFloat(2), 4),
        y = MathF.Round(reader.GetFloat(3), 4),
        z = MathF.Round(reader.GetFloat(4), 4),
        o = MathF.Round(reader.GetFloat(5), 4),
        s = MathF.Round(reader.GetFloat(7), 4)
    };

    if (!spawnsByMap.TryGetValue(map, out var list))
    {
        list = new List<object>();
        spawnsByMap[map] = list;
    }

    list.Add(spawn);
    totalCount++;
}

var jsonDict = new Dictionary<string, object>();
foreach (var (map, spawns) in spawnsByMap.OrderBy(kv => kv.Key))
{
    jsonDict[map.ToString()] = spawns;
}

var options = new JsonSerializerOptions { WriteIndented = false };
var json = JsonSerializer.Serialize(jsonDict, options);
await File.WriteAllTextAsync(outputPath, json);

Console.WriteLine($"Exported {totalCount} gameobject spawns across {spawnsByMap.Count} maps to {outputPath}");
Console.WriteLine($"File size: {new FileInfo(outputPath).Length / 1024}KB");

foreach (var (map, spawns) in spawnsByMap.OrderByDescending(kv => kv.Value.Count).Take(10))
{
    Console.WriteLine($"  Map {map}: {spawns.Count} spawns");
}

return 0;

static Dictionary<string, VariantDefinition> CreateVariantDefinitions()
{
    return new Dictionary<string, VariantDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        ["base"] = new(
            "base",
            "Always-on spawns: excludes event-linked and pool-linked gameobjects.",
            " AND NOT EXISTS (SELECT 1 FROM game_event_gameobject ge WHERE ge.guid = g.guid)" +
            " AND NOT EXISTS (SELECT 1 FROM pool_gameobject pg WHERE pg.guid = g.guid)"),
        ["midsummer-fire-festival"] = EventVariant(
            "midsummer-fire-festival",
            "Midsummer Fire Festival decorations and bonfires.",
            1),
        ["feast-of-winter-veil"] = EventVariant(
            "feast-of-winter-veil",
            "Winter Veil decorations, gifts, and related props.",
            2, 21),
        ["darkmoon-elwynn"] = EventVariant(
            "darkmoon-elwynn",
            "Darkmoon Faire when hosted in Elwynn Forest.",
            4, 23),
        ["darkmoon-mulgore"] = EventVariant(
            "darkmoon-mulgore",
            "Darkmoon Faire when hosted in Mulgore.",
            5, 24),
        ["fireworks"] = EventVariant(
            "fireworks",
            "Short-lived fireworks event props.",
            6, 39),
        ["lunar-festival"] = EventVariant(
            "lunar-festival",
            "Lunar Festival banners, lanterns, and related city decorations.",
            7, 38),
        ["love-is-in-the-air"] = EventVariant(
            "love-is-in-the-air",
            "Love is in the Air city decorations, including popularity-winner overlays.",
            8, 110, 111, 112, 113, 114, 115),
        ["harvest-festival"] = EventVariant(
            "harvest-festival",
            "Harvest Festival city props and food tables.",
            11),
        ["hallows-end"] = EventVariant(
            "hallows-end",
            "Hallow's End pumpkins, skull lights, and doorway props.",
            12),
        ["noblegarden"] = EventVariant(
            "noblegarden",
            "Noblegarden eggs and related decorations.",
            28),
        ["new-years-eve"] = EventVariant(
            "new-years-eve",
            "New Year's Eve props.",
            34),
        ["org-city-trophies"] = new(
            "org-city-trophies",
            "Orgrimmar city-gate dragon trophy displays.",
            " AND g.map = 1" +
            " AND gt.name IN ('The Severed Head of Onyxia', 'The Severed Head of Nefarian')"),
        ["sw-city-trophies"] = new(
            "sw-city-trophies",
            "Stormwind city-gate dragon trophy displays.",
            " AND g.map = 0" +
            " AND gt.name IN ('The Severed Head of Onyxia', 'The Severed Head of Nefarian')"),
        ["major-city-trophies"] = new(
            "major-city-trophies",
            "Stormwind and Orgrimmar dragon trophy displays.",
            " AND gt.name IN ('The Severed Head of Onyxia', 'The Severed Head of Nefarian')")
    };
}

static VariantDefinition EventVariant(string id, string description, params int[] eventIds)
{
    if (eventIds is null || eventIds.Length == 0)
    {
        throw new ArgumentException("Event variant requires at least one event id.", nameof(eventIds));
    }

    var idList = string.Join(",", eventIds.OrderBy(x => x));
    return new VariantDefinition(
        id,
        description,
        $" AND EXISTS (SELECT 1 FROM game_event_gameobject ge WHERE ge.guid = g.guid AND ge.event IN ({idList}))");
}

static void PrintUsage(IReadOnlyDictionary<string, VariantDefinition> variants)
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  GameObjectExporter [<connectionString>] [<outputPath>]");
    Console.Error.WriteLine("  GameObjectExporter [--connection <str>] [--out <path>] [--variant <name>]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("With no flags, exports the full spawn set to gameobject_spawns.json (legacy behavior).");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Supported variants:");
    foreach (var definition in variants.Values.OrderBy(v => v.Id, StringComparer.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"  {definition.Id}");
        Console.Error.WriteLine($"    {definition.Description}");
    }
}

internal sealed record VariantDefinition(string Id, string Description, string PredicateSql);
