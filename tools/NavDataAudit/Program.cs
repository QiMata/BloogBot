using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const float CellSize = 0.2666666f;
const float ContinentCellHeight = 0.25f;
const float MmapTileSize = 533.3333333f;
const float TaurenMaleRadius = 0.9747f;
const float TaurenMaleHeight = 2.625f;
const float CapsulePadding = 0.05f;
const uint MapKalimdor = 1;
const uint MmapMagic = 0x4D4D4150; // MMAP
const int MmapWrapperVersion = 6;
const int DetourNavMeshMagic = 0x444E4156; // DNAV
const int DetourNavMeshVersion = 7;
const int MmapTileHeaderSize = 20;
const int RefWidthBits = 64;

var dataRoot = ResolveDataRoot(args);
var mapId = GetUIntOption(args, "--map", MapKalimdor);
var buildLogPath = ResolveBuildLogPath(args, dataRoot, mapId);
var manifestPath = GetStringOption(args, "--write-manifest");
var tiles = GetTileOptions(args);
if (tiles.Count == 0)
{
    tiles.AddRange(
    [
        new Tile(28, 39), new Tile(28, 40), new Tile(28, 41), new Tile(28, 42),
        new Tile(29, 39), new Tile(29, 40), new Tile(29, 41), new Tile(29, 42),
        new Tile(30, 39), new Tile(30, 40), new Tile(30, 41), new Tile(30, 42),
        new Tile(31, 39), new Tile(31, 40), new Tile(31, 41), new Tile(31, 42),
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
var tileAudits = AuditTileHeaders(dataRoot, mapId, tiles, requiredRadius, requiredHeight, failures);
AuditGameObjectInputs(dataRoot, mapId, tiles, buildLogPath, failures);
if (!string.IsNullOrWhiteSpace(manifestPath))
    WriteManifest(manifestPath, dataRoot, mapId, buildLogPath, requiredRadius, requiredHeight, requiredRadiusCells, requiredHeightCells, tileAudits);

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

static string ResolveBuildLogPath(string[] args, string dataRoot, uint mapId)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], "--build-log", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(args[i + 1]);
    }

    return Path.Combine(dataRoot, $"map{mapId}_build.log");
}

static string? GetStringOption(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(args[i + 1]);
    }

    return null;
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

static List<MMapTileAudit> AuditTileHeaders(string dataRoot, uint mapId, List<Tile> tiles, float requiredRadius, float requiredHeight, List<string> failures)
{
    var audited = new List<MMapTileAudit>();
    var mmaps = Path.Combine(dataRoot, "mmaps");
    foreach (var tile in tiles)
    {
        var path = Path.Combine(mmaps, $"{mapId:000}{tile.Y:00}{tile.X:00}.mmtile");
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
        if (header.Value.MmapVersion != MmapWrapperVersion)
            Fail(failures, $"{Path.GetFileName(path)} mmap wrapper version {header.Value.MmapVersion} != required {MmapWrapperVersion}");
        if (header.Value.FileDetourVersion != DetourNavMeshVersion)
            Fail(failures, $"{Path.GetFileName(path)} wrapper Detour version {header.Value.FileDetourVersion} != required {DetourNavMeshVersion}");
        if (header.Value.DetourVersion != DetourNavMeshVersion)
            Fail(failures, $"{Path.GetFileName(path)} payload Detour version {header.Value.DetourVersion} != required {DetourNavMeshVersion}");
        if (header.Value.UsesLiquids != 0u && header.Value.UsesLiquids != 1u)
            Fail(failures, $"{Path.GetFileName(path)} usesLiquids value {header.Value.UsesLiquids} is not a uint boolean.");
        if (header.Value.TileDataSize <= 0)
            Fail(failures, $"{Path.GetFileName(path)} has an empty Detour tile payload.");

        CheckFloat(failures, $"{prefix} walkableRadius", header.Value.WalkableRadius, requiredRadius);
        CheckFloat(failures, $"{prefix} walkableHeight", header.Value.WalkableHeight, requiredHeight);
        Info($"{Path.GetFileName(path)}: mmapVersion={header.Value.MmapVersion}, wrapperDetourVersion={header.Value.FileDetourVersion}, payloadDetourVersion={header.Value.DetourVersion}, usesLiquids={header.Value.UsesLiquids}, radius={header.Value.WalkableRadius:F4}, height={header.Value.WalkableHeight:F4}, climb={header.Value.WalkableClimb:F4}");
        audited.Add(new MMapTileAudit(tile, path, header.Value));
    }

    return audited;
}

static DetourTileHeader? ReadTileHeader(string path)
{
    const int detourWalkableHeightOffset = 60;
    const int detourWalkableRadiusOffset = 64;
    const int detourWalkableClimbOffset = 68;

    var bytes = File.ReadAllBytes(path);
    if (bytes.Length < MmapTileHeaderSize + detourWalkableClimbOffset + sizeof(float))
        return null;

    var mmapMagic = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, sizeof(uint)));
    if (mmapMagic != MmapMagic)
        return null;

    var detourMagic = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(MmapTileHeaderSize, sizeof(int)));
    if (detourMagic != DetourNavMeshMagic)
        return null;

    var dtVersion = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4, sizeof(int)));
    var mmapVersion = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8, sizeof(int)));
    var tileDataSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12, sizeof(int)));
    var usesLiquids = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16, sizeof(uint)));
    var detourVersion = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(MmapTileHeaderSize + 4, sizeof(int)));

    return new DetourTileHeader(
        dtVersion,
        mmapVersion,
        tileDataSize,
        usesLiquids,
        detourVersion,
        BitConverter.ToSingle(bytes, MmapTileHeaderSize + detourWalkableHeightOffset),
        BitConverter.ToSingle(bytes, MmapTileHeaderSize + detourWalkableRadiusOffset),
        BitConverter.ToSingle(bytes, MmapTileHeaderSize + detourWalkableClimbOffset),
        Convert.ToHexString(SHA256.HashData(bytes)));
}

static void AuditGameObjectInputs(string dataRoot, uint mapId, List<Tile> tiles, string buildLogPath, List<string> failures)
{
    var tempModelsPath = Path.Combine(dataRoot, "vmaps", "temp_gameobject_models");
    var modelDisplayIds = ReadGameObjectModelDisplayIds(tempModelsPath);
    if (modelDisplayIds.Count == 0)
        Fail(failures, $"no displayId model mappings found in {tempModelsPath}");
    else
        Pass($"temp_gameobject_models contains {modelDisplayIds.Count} displayId model mappings.");

    var spawnsPath = Path.Combine(dataRoot, "gameobject_spawns.json");
    var modeledTileSpawns = CountModeledTileSpawnsByTile(spawnsPath, mapId, tiles, modelDisplayIds);
    var modeledSpawnTotal = modeledTileSpawns.Values.Sum();
    if (modeledSpawnTotal <= 0)
        Fail(failures, $"no modeled gameobject spawns found in audited tiles for map {mapId} in {spawnsPath}");
    else
        Pass($"gameobject_spawns.json has {modeledSpawnTotal} modeled gameobject spawns in audited tiles on map {mapId}.");

    if (!File.Exists(buildLogPath))
    {
        Fail(failures, $"missing build log {buildLogPath}");
        return;
    }

    var log = File.ReadAllText(buildLogPath).Replace("\0", string.Empty, StringComparison.Ordinal);
    if (!log.Contains("Loaded ", StringComparison.Ordinal) || !log.Contains("gameobject spawns", StringComparison.Ordinal))
        Fail(failures, $"{Path.GetFileName(buildLogPath)} does not show gameobject spawn loading.");
    else
        Pass($"{Path.GetFileName(buildLogPath)} shows gameobject spawn loading.");

    foreach (var tile in tiles)
    {
        var expectedOriginSpawns = modeledTileSpawns.GetValueOrDefault(tile);
        var line = FindGameObjectBakeLine(log, mapId, tile, out var bakedCount, out var candidateCount);
        if (line is null)
        {
            Fail(failures, $"{Path.GetFileName(buildLogPath)} has no GO geometry bake line for tile {tile.X},{tile.Y}");
            continue;
        }

        if (expectedOriginSpawns > 0 && candidateCount <= 0)
            Fail(failures, $"tile {tile.X},{tile.Y} has {expectedOriginSpawns} modeled spawn origins but GO bake line reports candidates={candidateCount}: {line.Trim()}");
        else if (candidateCount > 0 && bakedCount <= 0)
            Fail(failures, $"tile {tile.X},{tile.Y} GO geometry bake found candidates={candidateCount} but baked {bakedCount}: {line.Trim()}");
        else
            Pass($"tile {tile.X},{tile.Y} GO geometry bake line baked={bakedCount}, candidates={candidateCount}, modeledOriginSpawns={expectedOriginSpawns}.");
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

static Dictionary<Tile, int> CountModeledTileSpawnsByTile(string path, uint mapId, List<Tile> tiles, HashSet<uint> modelDisplayIds)
{
    var result = tiles.Distinct().ToDictionary(tile => tile, _ => 0);
    if (!File.Exists(path) || modelDisplayIds.Count == 0)
        return result;

    var tileSet = tiles.ToHashSet();
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    if (!doc.RootElement.TryGetProperty(mapId.ToString(CultureInfo.InvariantCulture), out var mapSpawns))
        return result;

    foreach (var spawn in mapSpawns.EnumerateArray())
    {
        var displayId = spawn.GetProperty("displayId").GetUInt32();
        if (!modelDisplayIds.Contains(displayId))
            continue;

        var x = spawn.GetProperty("x").GetSingle();
        var y = spawn.GetProperty("y").GetSingle();
        var tile = new Tile(ToMmapTile(x), ToMmapTile(y));
        if (tileSet.Contains(tile))
            result[tile]++;
    }

    return result;
}

static int ToMmapTile(float coordinate) => (int)(32.0f - coordinate / MmapTileSize);

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

static string? FindGameObjectBakeLine(string text, uint mapId, Tile tile, out int bakedCount, out int candidateCount)
{
    var bakedMarker = $"[GO] map={mapId} tile={tile.X},{tile.Y}: baked ";

    var bakedLine = FindLine(text, bakedMarker);
    if (bakedLine is not null)
    {
        bakedCount = ParseCountAfterMarker(bakedLine, bakedMarker);
        candidateCount = ParseNamedCount(bakedLine, "candidates=");
        return bakedLine;
    }

    bakedCount = -1;
    candidateCount = -1;
    return null;
}

static int ParseCountAfterMarker(string line, string marker)
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

static int ParseNamedCount(string line, string marker)
{
    var start = line.IndexOf(marker, StringComparison.Ordinal);
    if (start < 0)
        return -1;

    start += marker.Length;
    var end = start;
    while (end < line.Length && char.IsDigit(line[end]))
        end++;

    return end > start && int.TryParse(line[start..end], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
        ? value
        : -1;
}

static void WriteManifest(
    string manifestPath,
    string dataRoot,
    uint mapId,
    string buildLogPath,
    float requiredRadius,
    float requiredHeight,
    int requiredRadiusCells,
    int requiredHeightCells,
    List<MMapTileAudit> tileAudits)
{
    Directory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? ".");

    var orderedTiles = tileAudits
        .OrderBy(tile => tile.Tile.X)
        .ThenBy(tile => tile.Tile.Y)
        .ToArray();

    var manifest = new
    {
        schemaVersion = 2,
        createdAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        dataRoot,
        mapId,
        buildLogPath,
        configPath = Path.Combine(dataRoot, "config.json"),
        generatorPath = ResolveKnownGeneratorPath(),
        detourNavMeshVersion = DetourNavMeshVersion,
        mmapWrapperVersion = MmapWrapperVersion,
        refWidthBits = RefWidthBits,
        mmapTileHeaderBytes = MmapTileHeaderSize,
        mmapSchema = $"MMAP_VERSION={MmapWrapperVersion};DT_NAVMESH_VERSION={DetourNavMeshVersion};DT_POLYREF={RefWidthBits};GO_BAKE=model-geometry",
        gameObjectBake = new
        {
            mode = "model-geometry-with-aabb-fallback",
            sourceModels = Path.Combine(dataRoot, "vmaps", "temp_gameobject_models"),
            sourceSpawns = Path.Combine(dataRoot, "gameobject_spawns.json"),
            requiredLogMarker = "[GO] map=<map> tile=<x>,<y>: baked <count> gameobject model(s), triangles=<n> vertices=<n> candidates=<n> missing=<n>"
        },
        agent = new
        {
            radius = requiredRadius,
            height = requiredHeight,
            walkableRadiusCells = requiredRadiusCells,
            walkableHeightCells = requiredHeightCells,
            source = "Tauren Male radius plus capsule padding"
        },
        navDataSignature = ComputeSignature(mapId, requiredRadius, requiredHeight, orderedTiles),
        tiles = orderedTiles.Select(tile => new
        {
            fileName = Path.GetFileName(tile.Path),
            tileX = tile.Tile.X,
            tileY = tile.Tile.Y,
            mmapVersion = tile.Header.MmapVersion,
            wrapperDetourVersion = tile.Header.FileDetourVersion,
            payloadDetourVersion = tile.Header.DetourVersion,
            usesLiquids = tile.Header.UsesLiquids,
            tileDataSize = tile.Header.TileDataSize,
            walkableRadius = tile.Header.WalkableRadius,
            walkableHeight = tile.Header.WalkableHeight,
            walkableClimb = tile.Header.WalkableClimb,
            sha256 = tile.Header.Sha256
        })
    };

    var options = new JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, options), Encoding.UTF8);
    Pass($"wrote mmap manifest {manifestPath}");
}

static string? ResolveKnownGeneratorPath()
{
    var path = Path.GetFullPath("D:/MaNGOS/source/bin/MoveMapGenerator.exe");
    return File.Exists(path) ? path : null;
}

static string ComputeSignature(uint mapId, float requiredRadius, float requiredHeight, IEnumerable<MMapTileAudit> tiles)
{
    var builder = new StringBuilder();
    builder.Append(CultureInfo.InvariantCulture, $"map={mapId};mmap={MmapWrapperVersion};detour={DetourNavMeshVersion};ref={RefWidthBits};");
    builder.Append(CultureInfo.InvariantCulture, $"radius={requiredRadius:R};height={requiredHeight:R};");
    foreach (var tile in tiles)
    {
        builder.Append(CultureInfo.InvariantCulture, $"{Path.GetFileName(tile.Path)}:{tile.Header.Sha256};");
    }

    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
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

readonly record struct DetourTileHeader(
    int FileDetourVersion,
    int MmapVersion,
    int TileDataSize,
    uint UsesLiquids,
    int DetourVersion,
    float WalkableHeight,
    float WalkableRadius,
    float WalkableClimb,
    string Sha256);

readonly record struct MMapTileAudit(Tile Tile, string Path, DetourTileHeader Header);
