using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

const float CellSize = 0.2666666f;
const float ContinentCellHeight = 0.25f;
const float TaurenMaleRadius = 0.9747f;
const float TaurenMaleHeight = 2.625f;
const float CapsulePadding = 0.05f;
const uint MapKalimdor = 1;

var dataRoot = ResolveDataRoot(args);
var mapId = GetUIntOption(args, "--map", MapKalimdor);
var tiles = GetTileOptions(args);
if (tiles.Count == 0)
{
    tiles.AddRange(
    [
        new Tile(28, 39), new Tile(28, 40), new Tile(28, 41),
        new Tile(29, 39), new Tile(29, 40), new Tile(29, 41),
        new Tile(30, 39), new Tile(30, 40), new Tile(30, 41),
    ]);
}

var requiredRadius = TaurenMaleRadius + CapsulePadding;
var requiredHeight = TaurenMaleHeight;
var requiredRadiusCells = (int)MathF.Ceiling(requiredRadius / CellSize);
var requiredHeightCells = (int)MathF.Ceiling(requiredHeight / ContinentCellHeight);

var failures = new List<string>();

Console.WriteLine("WWoW navigation data audit");
Console.WriteLine($"Data root: {dataRoot}");
Console.WriteLine($"Target capsule: Tauren Male radius={TaurenMaleRadius:F4} + padding={CapsulePadding:F2} => {requiredRadius:F4}, height={requiredHeight:F4}");
Console.WriteLine($"Required Recast cells: walkableRadius >= {requiredRadiusCells} at cs={CellSize}, walkableHeight >= {requiredHeightCells} at ch={ContinentCellHeight}");
Console.WriteLine();

AuditConfig(dataRoot, mapId, requiredRadius, requiredHeight, requiredRadiusCells, requiredHeightCells, failures);
AuditTileHeaders(dataRoot, mapId, tiles, requiredRadius, requiredHeight, failures);
AuditGameObjectInputs(dataRoot, mapId, tiles, failures);

Console.WriteLine();
if (failures.Count == 0)
{
    Console.WriteLine("RESULT: PASS");
    return 0;
}

Console.WriteLine("RESULT: FAIL");
foreach (var failure in failures)
    Console.WriteLine($"  - {failure}");

return 2;

static string ResolveDataRoot(string[] args)
{
    if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
        return Path.GetFullPath(args[0]);

    var env = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
    if (!string.IsNullOrWhiteSpace(env))
        return Path.GetFullPath(env);

    return Path.GetFullPath("D:/MaNGOS/data");
}

static uint GetUIntOption(string[] args, string name, uint defaultValue)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)
            && uint.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }
    }

    return defaultValue;
}

static List<Tile> GetTileOptions(string[] args)
{
    var result = new List<Tile>();
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (!string.Equals(args[i], "--tile", StringComparison.OrdinalIgnoreCase))
            continue;

        var parts = args[i + 1].Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            result.Add(new Tile(x, y));
        }
    }

    return result;
}

static void AuditConfig(
    string dataRoot,
    uint mapId,
    float requiredRadius,
    float requiredHeight,
    int requiredRadiusCells,
    int requiredHeightCells,
    List<string> failures)
{
    var path = Path.Combine(dataRoot, "config.json");
    if (!File.Exists(path))
    {
        Fail(failures, $"config.json missing at {path}");
        return;
    }

    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    if (!doc.RootElement.TryGetProperty(mapId.ToString(CultureInfo.InvariantCulture), out var mapConfig))
    {
        Fail(failures, $"config.json has no map {mapId} override");
        return;
    }

    var agentRadius = TryGetSingle(mapConfig, "agentRadius");
    var agentHeight = TryGetSingle(mapConfig, "agentHeight");
    var walkableRadius = TryGetInt(mapConfig, "walkableRadius");
    var walkableHeight = TryGetInt(mapConfig, "walkableHeight");

    CheckFloat(failures, $"config map {mapId} agentRadius", agentRadius, requiredRadius);
    CheckFloat(failures, $"config map {mapId} agentHeight", agentHeight, requiredHeight);
    CheckInt(failures, $"config map {mapId} walkableRadius", walkableRadius, requiredRadiusCells);

    if (walkableHeight.HasValue)
        CheckInt(failures, $"config map {mapId} walkableHeight", walkableHeight, requiredHeightCells);
    else
        Info($"config map {mapId} walkableHeight not set; generator must derive >= {requiredHeightCells} from agentHeight.");
}

static void AuditTileHeaders(string dataRoot, uint mapId, List<Tile> tiles, float requiredRadius, float requiredHeight, List<string> failures)
{
    var mmaps = Path.Combine(dataRoot, "mmaps");
    foreach (var tile in tiles)
    {
        var path = Path.Combine(mmaps, $"{mapId:000}{tile.X:00}{tile.Y:00}.mmtile");
        if (!File.Exists(path))
        {
            Fail(failures, $"missing tile {Path.GetFileName(path)}");
            continue;
        }

        var header = ReadTileHeader(path);
        if (header is null)
        {
            Fail(failures, $"could not parse Detour header from {Path.GetFileName(path)}");
            continue;
        }

        var prefix = $"{Path.GetFileName(path)} Detour header";
        CheckFloat(failures, $"{prefix} walkableRadius", header.Value.WalkableRadius, requiredRadius);
        CheckFloat(failures, $"{prefix} walkableHeight", header.Value.WalkableHeight, requiredHeight);
        Info($"{Path.GetFileName(path)}: radius={header.Value.WalkableRadius:F4}, height={header.Value.WalkableHeight:F4}, climb={header.Value.WalkableClimb:F4}");
    }
}

static DetourTileHeader? ReadTileHeader(string path)
{
    const int mmapTileHeaderSize = 20;
    const int detourWalkableHeightOffset = 60;
    const int detourWalkableRadiusOffset = 64;
    const int detourWalkableClimbOffset = 68;

    var bytes = File.ReadAllBytes(path);
    if (bytes.Length < mmapTileHeaderSize + detourWalkableClimbOffset + sizeof(float))
        return null;

    var mmapMagic = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, sizeof(uint)));
    if (mmapMagic != 0x4D4D4150) // MMAP
        return null;

    var detourMagic = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(mmapTileHeaderSize, sizeof(int)));
    if (detourMagic != 0x444E4156) // DNAV
        return null;

    return new DetourTileHeader(
        BitConverter.ToSingle(bytes, mmapTileHeaderSize + detourWalkableHeightOffset),
        BitConverter.ToSingle(bytes, mmapTileHeaderSize + detourWalkableRadiusOffset),
        BitConverter.ToSingle(bytes, mmapTileHeaderSize + detourWalkableClimbOffset));
}

static void AuditGameObjectInputs(string dataRoot, uint mapId, List<Tile> tiles, List<string> failures)
{
    var tempModelsPath = Path.Combine(dataRoot, "vmaps", "temp_gameobject_models");
    var modelDisplayIds = ReadGameObjectModelDisplayIds(tempModelsPath);
    if (modelDisplayIds.Count == 0)
        Fail(failures, $"no displayId model mappings found in {tempModelsPath}");
    else
        Pass($"temp_gameobject_models contains {modelDisplayIds.Count} displayId model mappings.");

    var spawnsPath = Path.Combine(dataRoot, "gameobject_spawns.json");
    var modeledOrgrimmarSpawns = CountModeledOrgrimmarSpawns(spawnsPath, mapId, modelDisplayIds);
    if (modeledOrgrimmarSpawns <= 0)
        Fail(failures, $"no modeled Orgrimmar gameobject spawns found in {spawnsPath}");
    else
        Pass($"gameobject_spawns.json has {modeledOrgrimmarSpawns} modeled Orgrimmar corridor/tower spawns on map {mapId}.");

    var buildLogPath = Path.Combine(dataRoot, $"map{mapId}_build.log");
    if (!File.Exists(buildLogPath))
    {
        Fail(failures, $"missing build log {buildLogPath}");
        return;
    }

    var log = File.ReadAllText(buildLogPath);
    if (!log.Contains("Loaded ", StringComparison.Ordinal) || !log.Contains("gameobject spawns", StringComparison.Ordinal))
        Fail(failures, $"{Path.GetFileName(buildLogPath)} does not show gameobject spawn loading.");
    else
        Pass($"{Path.GetFileName(buildLogPath)} shows gameobject spawn loading.");

    foreach (var tile in tiles)
    {
        var marker = $"[GO] map={mapId} tile={tile.X},{tile.Y}: loaded ";
        var line = FindLine(log, marker);
        if (line is null)
        {
            Fail(failures, $"{Path.GetFileName(buildLogPath)} has no GO bake line for tile {tile.X},{tile.Y}");
            continue;
        }

        var loaded = ParseLoadedCount(line, marker);
        if (loaded <= 0)
            Fail(failures, $"tile {tile.X},{tile.Y} GO bake line loaded {loaded} meshes: {line.Trim()}");
        else
            Pass($"tile {tile.X},{tile.Y} GO bake line loaded {loaded} meshes.");
    }
}

static HashSet<uint> ReadGameObjectModelDisplayIds(string path)
{
    const int modelBoundsBytes = sizeof(float) * 6;
    var result = new HashSet<uint>();
    if (!File.Exists(path))
        return result;

    var bytes = File.ReadAllBytes(path);
    var offset = 0;
    while (offset + 8 <= bytes.Length)
    {
        var displayId = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
        var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
        offset += 8;

        if (length > bytes.Length - offset)
            break;

        result.Add(displayId);
        offset += checked((int)length);
        if (offset + modelBoundsBytes <= bytes.Length)
            offset += modelBoundsBytes;
    }

    return result;
}

static int CountModeledOrgrimmarSpawns(string path, uint mapId, HashSet<uint> modelDisplayIds)
{
    if (!File.Exists(path) || modelDisplayIds.Count == 0)
        return 0;

    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    if (!doc.RootElement.TryGetProperty(mapId.ToString(CultureInfo.InvariantCulture), out var mapSpawns))
        return 0;

    var count = 0;
    foreach (var spawn in mapSpawns.EnumerateArray())
    {
        var displayId = spawn.GetProperty("displayId").GetUInt32();
        if (!modelDisplayIds.Contains(displayId))
            continue;

        var x = spawn.GetProperty("x").GetSingle();
        var y = spawn.GetProperty("y").GetSingle();
        if (x is >= 1300f and <= 1685f && y is >= -4665f and <= -4300f)
            count++;
    }

    return count;
}

static string? FindLine(string text, string marker)
{
    using var reader = new StringReader(text);
    string? line;
    while ((line = reader.ReadLine()) is not null)
    {
        if (line.Contains(marker, StringComparison.Ordinal))
            return line;
    }

    return null;
}

static int ParseLoadedCount(string line, string marker)
{
    var start = line.IndexOf(marker, StringComparison.Ordinal);
    if (start < 0)
        return -1;

    start += marker.Length;
    var end = line.IndexOf(' ', start);
    if (end < 0)
        return -1;

    return int.TryParse(line[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var loaded)
        ? loaded
        : -1;
}

static float? TryGetSingle(JsonElement element, string property)
{
    if (!element.TryGetProperty(property, out var value))
        return null;

    return value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var result)
        ? result
        : null;
}

static int? TryGetInt(JsonElement element, string property)
{
    if (!element.TryGetProperty(property, out var value))
        return null;

    return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
        ? result
        : null;
}

static void CheckFloat(List<string> failures, string label, float? actual, float required)
{
    if (actual.HasValue && actual.Value + 0.0001f >= required)
    {
        Pass($"{label}={actual.Value:F4} >= {required:F4}");
        return;
    }

    Fail(failures, $"{label}={(actual.HasValue ? actual.Value.ToString("F4", CultureInfo.InvariantCulture) : "<missing>")} < {required:F4}");
}

static void CheckInt(List<string> failures, string label, int? actual, int required)
{
    if (actual.HasValue && actual.Value >= required)
    {
        Pass($"{label}={actual.Value} >= {required}");
        return;
    }

    Fail(failures, $"{label}={(actual.HasValue ? actual.Value.ToString(CultureInfo.InvariantCulture) : "<missing>")} < {required}");
}

static void Pass(string message) => Console.WriteLine($"PASS: {message}");

static void Info(string message) => Console.WriteLine($"INFO: {message}");

static void Fail(List<string> failures, string message)
{
    Console.WriteLine($"FAIL: {message}");
    failures.Add(message);
}

readonly record struct Tile(int X, int Y);

readonly record struct DetourTileHeader(float WalkableHeight, float WalkableRadius, float WalkableClimb);
