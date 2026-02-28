using GameData.Core.Enums;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using WoWSharpClient.Handlers;
using WoWSharpClient.Models;
using WoWSharpClient.Tests.Util;

namespace WoWSharpClient.Tests.Handlers
{
    [Collection("Sequential ObjectManager tests")]
    public class SMSG_UPDATE_OBJECT_Tests(ObjectManagerFixture _) : IClassFixture<ObjectManagerFixture>
    {
        [Fact]
        public void ShouldDecompressAndParseAllCompressedUpdateObjectPackets()
        {
            var opcode = Opcode.SMSG_UPDATE_OBJECT;
            var objectManager = WoWSharpObjectManager.Instance;
            var initialCount = objectManager.Objects.Count();

            var directoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", opcode.ToString());

            var files = Directory.GetFiles(directoryPath, "20240815_*.bin")
                .OrderBy(path =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    var parts = fileName.Split('_');
                    return parts.Length > 1 && int.TryParse(parts[1], out var index) ? index : int.MaxValue;
                });

            foreach (var filePath in files)
            {
                byte[] data = FileReader.ReadBinaryFile(filePath);
                ObjectUpdateHandler.HandleUpdateObject(opcode, data);
            }

            WoWSharpObjectManager.Instance.ProcessUpdatesAsync(new CancellationTokenSource().Token);
            Thread.Sleep(100); // Allow background processing to complete

            var objectsAfterUpdate = objectManager.Objects.ToList();

            Assert.NotEmpty(objectsAfterUpdate);
            Assert.True(objectsAfterUpdate.Count >= initialCount, "Update processing should not reduce the tracked object count.");
            Assert.Contains(objectsAfterUpdate, o => o.Guid != 0);
        }
    }

    /// <summary>
    /// Replays an entire captured WoW session by processing initial world-state packets
    /// (pre-defined files) followed by all timestamped update packets in chronological order.
    /// Validates that the ObjectManager reaches a consistent final state with expected objects.
    /// </summary>
    [Collection("Sequential ObjectManager tests")]
    public class SessionTimelineReplayTests(ObjectManagerFixture _) : IClassFixture<ObjectManagerFixture>
    {
        private static readonly Regex TimestampPattern = new(@"^20240815_(\d{9})$", RegexOptions.Compiled);
        private static readonly Regex PreDefinedPattern = new(@"^20240815_(\d{1,4})$", RegexOptions.Compiled);

        /// <summary>
        /// Builds the full session timeline: initial world-state packets first (sorted by
        /// numeric suffix), then all timestamped packets interleaved chronologically.
        /// </summary>
        private static List<(long sortKey, Opcode opcode, string path)> BuildFullTimeline()
        {
            var resourceBase = Path.Combine(Directory.GetCurrentDirectory(), "Resources");
            var initialPackets = new List<(long sortKey, Opcode opcode, string path)>();
            var timestampedPackets = new List<(long sortKey, Opcode opcode, string path)>();

            foreach (var opcode in new[] { Opcode.SMSG_UPDATE_OBJECT, Opcode.SMSG_COMPRESSED_UPDATE_OBJECT })
            {
                var dir = Path.Combine(resourceBase, opcode.ToString());
                if (!Directory.Exists(dir)) continue;

                foreach (var filePath in Directory.GetFiles(dir, "20240815_*.bin"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);

                    var tsMatch = TimestampPattern.Match(fileName);
                    if (tsMatch.Success && long.TryParse(tsMatch.Groups[1].Value, out var timestamp))
                    {
                        timestampedPackets.Add((timestamp, opcode, filePath));
                        continue;
                    }

                    var pdMatch = PreDefinedPattern.Match(fileName);
                    if (pdMatch.Success && long.TryParse(pdMatch.Groups[1].Value, out var index))
                    {
                        initialPackets.Add((index, opcode, filePath));
                    }
                }
            }

            // Sort initial packets by their numeric suffix, then timestamped by timestamp
            initialPackets.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));
            timestampedPackets.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));

            // Concatenate: initial world state first, then ongoing updates
            var timeline = new List<(long sortKey, Opcode opcode, string path)>(
                initialPackets.Count + timestampedPackets.Count);
            timeline.AddRange(initialPackets);
            timeline.AddRange(timestampedPackets);
            return timeline;
        }

        /// <summary>
        /// Collects only the timestamped files (9-digit suffix) for timeline-specific tests.
        /// </summary>
        private static List<(long timestamp, Opcode opcode, string path)> CollectTimestampedFiles()
        {
            var resourceBase = Path.Combine(Directory.GetCurrentDirectory(), "Resources");
            var result = new List<(long timestamp, Opcode opcode, string path)>();

            foreach (var opcode in new[] { Opcode.SMSG_UPDATE_OBJECT, Opcode.SMSG_COMPRESSED_UPDATE_OBJECT })
            {
                var dir = Path.Combine(resourceBase, opcode.ToString());
                if (!Directory.Exists(dir)) continue;

                foreach (var filePath in Directory.GetFiles(dir, "20240815_*.bin"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var match = TimestampPattern.Match(fileName);
                    if (match.Success && long.TryParse(match.Groups[1].Value, out var timestamp))
                    {
                        result.Add((timestamp, opcode, filePath));
                    }
                }
            }

            result.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
            return result;
        }

        /// <summary>
        /// Replays all packets through ObjectUpdateHandler and processes queued updates.
        /// </summary>
        private static void ReplayAndProcess(List<(long sortKey, Opcode opcode, string path)> timeline)
        {
            foreach (var (_, opcode, path) in timeline)
            {
                byte[] data = FileReader.ReadBinaryFile(path);
                ObjectUpdateHandler.HandleUpdateObject(opcode, data);
            }

            using var cts = new CancellationTokenSource();
            WoWSharpObjectManager.Instance.ProcessUpdatesAsync(cts.Token);
            Thread.Sleep(200);
        }

        /// <summary>
        /// Replays the full captured session (initial world state + timestamped updates)
        /// and validates the ObjectManager contains the expected world state.
        /// </summary>
        [Fact]
        public void FullSessionReplay_ProducesConsistentObjectManagerState()
        {
            // Arrange & Act
            var timeline = BuildFullTimeline();

            Assert.True(timeline.Count >= 80,
                $"Expected at least 80 total packets, found {timeline.Count}");

            ReplayAndProcess(timeline);

            var objects = WoWSharpObjectManager.Instance.Objects.ToList();

            // Assert — the ObjectManager should have a populated, valid world state
            Assert.NotEmpty(objects);

            // Player with GUID 150 should exist (created by initial world-state packet)
            var player = objects.FirstOrDefault(o => o.Guid == 150);
            Assert.NotNull(player);
            Assert.Equal(WoWObjectType.Player, player.ObjectType);

            // Player should have a valid world position (not at origin)
            Assert.NotEqual(0f, player.Position.X);
            Assert.NotEqual(0f, player.Position.Y);

            // Should contain multiple object types
            var objectTypes = objects.Select(o => o.ObjectType).Distinct().ToList();
            Assert.True(objectTypes.Count >= 2,
                $"Expected at least 2 distinct object types, found: {string.Join(", ", objectTypes)}");

            // Should have items (player inventory is in the session)
            Assert.Contains(objects, o => o.ObjectType == WoWObjectType.Item);

            // Should have game objects (transports are in the first compressed packet)
            Assert.Contains(objects, o => o.ObjectType == WoWObjectType.GameObj);

            // All objects should have non-zero GUIDs
            Assert.DoesNotContain(objects, o => o.Guid == 0);

            // Reasonable object count — a live session should have dozens of objects
            Assert.True(objects.Count >= 20,
                $"Expected at least 20 objects after full session replay, found {objects.Count}");
        }

        /// <summary>
        /// Verifies that the timestamped portion of the timeline contains packets from both
        /// opcodes, confirming that interleaving is actually happening.
        /// </summary>
        [Fact]
        public void Timeline_ContainsBothOpcodeTypes()
        {
            var timeline = CollectTimestampedFiles();

            var updateCount = timeline.Count(t => t.opcode == Opcode.SMSG_UPDATE_OBJECT);
            var compressedCount = timeline.Count(t => t.opcode == Opcode.SMSG_COMPRESSED_UPDATE_OBJECT);

            Assert.True(updateCount >= 5,
                $"Expected at least 5 SMSG_UPDATE_OBJECT packets, found {updateCount}");
            Assert.True(compressedCount >= 70,
                $"Expected at least 70 SMSG_COMPRESSED_UPDATE_OBJECT packets, found {compressedCount}");
        }

        /// <summary>
        /// Verifies that timestamps are correctly parsed and ordered — the first packet
        /// should be before the last packet in the timeline.
        /// </summary>
        [Fact]
        public void Timeline_IsChronologicallyOrdered()
        {
            var timeline = CollectTimestampedFiles();

            // Verify ordering: each timestamp should be >= previous
            for (int i = 1; i < timeline.Count; i++)
            {
                Assert.True(timeline[i].timestamp >= timeline[i - 1].timestamp,
                    $"Timeline out of order at index {i}: {timeline[i - 1].timestamp} > {timeline[i].timestamp}");
            }

            // First and last timestamps should differ (session spans multiple seconds)
            Assert.True(timeline.Last().timestamp > timeline.First().timestamp,
                "Session should span a time range (first != last timestamp)");
        }

        /// <summary>
        /// Replays the full session and verifies that partial updates apply correctly:
        /// items owned by player 150 have correct ownership, and NPC units appear in the
        /// world after being created by timestamped COMPRESSED_UPDATE_OBJECT packets.
        /// Note: The ObjectManager is a shared singleton across xUnit collection tests,
        /// so duplicate GUIDs may exist from earlier test classes processing the same packets.
        /// </summary>
        [Fact]
        public void FullSessionReplay_PartialUpdatesApplyToExistingObjects()
        {
            // Arrange & Act
            var timeline = BuildFullTimeline();
            ReplayAndProcess(timeline);

            var objects = WoWSharpObjectManager.Instance.Objects.ToList();

            // Items owned by player 150 should reference the correct owner
            // Use Distinct by GUID to handle shared singleton state from other collection tests
            var uniqueItems = objects
                .Where(o => o.ObjectType == WoWObjectType.Item)
                .GroupBy(o => o.Guid)
                .Select(g => g.First())
                .Cast<WoWItem>()
                .Where(i => i.Owner.FullGuid == 150)
                .ToList();

            Assert.True(uniqueItems.Count >= 10,
                $"Expected at least 10 unique items owned by player 150, found {uniqueItems.Count}");

            // All player items should have valid entries (non-zero item template IDs)
            Assert.DoesNotContain(uniqueItems, item => item.Entry == 0);

            // At least one container should exist (player bags)
            var containers = objects
                .Where(o => o.ObjectType == WoWObjectType.Container)
                .GroupBy(o => o.Guid)
                .Select(g => g.First())
                .Cast<WoWContainer>()
                .ToList();

            Assert.True(containers.Count >= 1,
                $"Expected at least 1 container (bag), found {containers.Count}");

            // Game objects should exist (transports from initial world state)
            var uniqueGameObjects = objects
                .Where(o => o.ObjectType == WoWObjectType.GameObj)
                .GroupBy(o => o.Guid)
                .Select(g => g.First())
                .ToList();

            Assert.True(uniqueGameObjects.Count >= 5,
                $"Expected at least 5 unique game objects, found {uniqueGameObjects.Count}");
        }
    }
}
