using System.Diagnostics;
using BotRunner.Clients;
using GameData.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

var client = new PathfindingClient(
    "127.0.0.1",
    5001,
    NullLogger<PathfindingClient>.Instance,
    pathRequestTimeoutMs: 30000,
    queryTimeoutMs: 10000,
    physicsTimeoutMs: 5000);

var start = new Position(1177.8f, -4464.2f, 21.4f);
var end = new Position(1629.4f, -4373.4f, 31.3f);
var stopwatch = Stopwatch.StartNew();

try
{
    var path = client.GetPath(1, start, end, smoothPath: false);
    stopwatch.Stop();

    Console.WriteLine($"elapsedMs={stopwatch.ElapsedMilliseconds} count={path.Length}");
    for (var i = 0; i < path.Length; i++)
    {
        Console.WriteLine($"[{i}] ({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1})");
    }
}
catch (Exception ex)
{
    stopwatch.Stop();
    Console.WriteLine($"ERROR elapsedMs={stopwatch.ElapsedMilliseconds}");
    Console.WriteLine(ex);
}
finally
{
    client.Dispose();
}
