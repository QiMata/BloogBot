using BotRunner.Clients;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

var client = new PathfindingClient("127.0.0.1", 5001, NullLogger.Instance);

const uint mapId = 1;
const uint altMapId = 0;
var ghost = new Position(233.5f, -4793.7f, 10.2f);
var corpse = new Position(300.0f, -4788.0f, 10.2f);

static string Format(Position p) => $"({p.X:F1},{p.Y:F1},{p.Z:F1})";

int TryPath(PathfindingClient pf, uint m, Position start, Position end, bool smooth, out string error)
{
    error = string.Empty;
    try
    {
        var path = pf.GetPath(m, start, end, smooth);
        return path.Length;
    }
    catch (Exception ex)
    {
        error = ex.Message;
        return -1;
    }
}

Console.WriteLine($"Ghost={Format(ghost)} Corpse={Format(corpse)} map={mapId}");
Console.WriteLine($"AltMap={altMapId}");

foreach (var smooth in new[] { true, false })
{
    var len = TryPath(client, mapId, ghost, corpse, smooth, out var error);
    Console.WriteLine(len >= 0
        ? $"direct smooth={smooth}: len={len}"
        : $"direct smooth={smooth}: ERROR {error}");

    var altLen = TryPath(client, altMapId, ghost, corpse, smooth, out var altError);
    Console.WriteLine(altLen >= 0
        ? $"direct altMap smooth={smooth}: len={altLen}"
        : $"direct altMap smooth={smooth}: ERROR {altError}");
}

Console.WriteLine("line-of-sight checks:");
foreach (var pair in new (Position A, Position B, string Label)[]
{
    (ghost, corpse, "ghost->corpse"),
    (corpse, ghost, "corpse->ghost"),
    (new Position(300.0f, -4788.0f, 10.2f), new Position(320.0f, -4788.0f, 10.2f), "near-east"),
    (new Position(233.5f, -4793.7f, 10.2f), new Position(238.5f, -4793.7f, 10.2f), "near-ghost-east"),
})
{
    try
    {
        var los = client.IsInLineOfSight(mapId, pair.A, pair.B);
        Console.WriteLine($"  {pair.Label} map={mapId}: {los}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  {pair.Label} map={mapId}: ERROR {ex.Message}");
    }
}

var sanityPairs = new (Position Start, Position End, string Label)[]
{
    (new Position(-616.2514f, -4188.0044f, 82.3167f), new Position(1629.36f, -4373.39f, 50.2564f), "legacy-map0-ish"),
    (new Position(300.0f, -4788.0f, 10.2f), new Position(320.0f, -4788.0f, 10.2f), "near-corpse-east"),
    (new Position(300.0f, -4788.0f, 10.2f), new Position(290.0f, -4800.0f, 10.2f), "near-corpse-southwest"),
    (new Position(233.5f, -4793.7f, 10.2f), new Position(238.5f, -4793.7f, 10.2f), "near-ghost-east"),
    (new Position(233.5f, -4793.7f, 10.2f), new Position(228.5f, -4793.7f, 10.2f), "near-ghost-west"),
    (new Position(233.5f, -4793.7f, 10.2f), new Position(300.0f, -4788.0f, 10.2f), "ghost-to-corpse"),
    (new Position(300.0f, -4788.0f, 10.2f), new Position(233.5f, -4793.7f, 10.2f), "corpse-to-ghost"),
};

Console.WriteLine("sanity matrix:");
foreach (var pair in sanityPairs)
{
    var len = TryPath(client, mapId, pair.Start, pair.End, smooth: false, out var error);
    Console.WriteLine(len >= 0
        ? $"  {pair.Label} map={mapId}: len={len}"
        : $"  {pair.Label} map={mapId}: ERROR {error}");

    var altLen = TryPath(client, altMapId, pair.Start, pair.End, smooth: false, out var altError);
    Console.WriteLine(altLen >= 0
        ? $"  {pair.Label} map={altMapId}: len={altLen}"
        : $"  {pair.Label} map={altMapId}: ERROR {altError}");
}

var radii = new[] { 2f, 4f, 6f, 8f, 10f, 15f, 20f, 30f, 40f, 55f };
var successCount = 0;

foreach (var radius in radii)
{
    for (var deg = 0; deg < 360; deg += 30)
    {
        var radians = MathF.PI * deg / 180f;
        var candidate = new Position(
            ghost.X + MathF.Cos(radians) * radius,
            ghost.Y + MathF.Sin(radians) * radius,
            ghost.Z);

        var len = TryPath(client, mapId, candidate, corpse, smooth: false, out _);
        if (len > 0)
        {
            successCount++;
            Console.WriteLine($"start-probe radius={radius,5:F1} angle={deg,3} candidate={Format(candidate)} len={len}");
        }
    }
}

Console.WriteLine($"start-probe success count={successCount}");

successCount = 0;
foreach (var radius in radii)
{
    for (var deg = 0; deg < 360; deg += 30)
    {
        var radians = MathF.PI * deg / 180f;
        var target = new Position(
            corpse.X + MathF.Cos(radians) * radius,
            corpse.Y + MathF.Sin(radians) * radius,
            corpse.Z);

        var len = TryPath(client, mapId, ghost, target, smooth: false, out _);
        if (len > 0)
        {
            successCount++;
            Console.WriteLine($"target-probe radius={radius,5:F1} angle={deg,3} target={Format(target)} len={len}");
        }
    }
}

Console.WriteLine($"target-probe success count={successCount}");
