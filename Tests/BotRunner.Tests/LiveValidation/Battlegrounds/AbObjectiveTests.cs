using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotRunner.Tasks.Battlegrounds;
using Communication;
using Xunit;
using Xunit.Abstractions;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

/// <summary>
/// Live Arathi Basin objective coverage:
/// 1. prove a Horde assaulter can interact with a live capture banner after queue/entry.
/// 2. script five uncontested Horde banner assaults and wait for the match to complete.
/// </summary>
[Collection(ArathiBasinCollection.Name)]
public class AbObjectiveTests
{
    private static readonly TimeSpan ArathiBasinPrepWindow = TimeSpan.FromSeconds(130);

    private const uint StableBannerEntry = 180087;
    private const uint BlacksmithBannerEntry = 180088;
    private const uint FarmBannerEntry = 180089;
    private const uint LumberMillBannerEntry = 180090;
    private const uint GoldMineBannerEntry = 180091;

    private static readonly IReadOnlyList<AbBannerAssault> HordeFullMapAssaultPlan =
    [
        new("ABBOT1", "Stable", StableBannerEntry, 1166.79f, 1200.13f, -56.71f),
        new("ABBOT2", "Blacksmith", BlacksmithBannerEntry, 977.08f, 1046.54f, -44.83f),
        new("ABBOT3", "Farm", FarmBannerEntry, 806.18f, 874.27f, -55.99f),
        new("ABBOT4", "Lumber Mill", LumberMillBannerEntry, 856.14f, 1148.90f, 11.18f),
        new("ABBOT5", "Gold Mine", GoldMineBannerEntry, 1146.92f, 848.18f, -110.92f),
    ];

    private static readonly IReadOnlyList<string> AllTrackedAccounts = ArathiBasinFixture.HordeAccountsOrdered
        .Concat(ArathiBasinFixture.AllianceAccountsOrdered)
        .ToArray();
    private static readonly IReadOnlyList<string> HordeTrackedAccounts = ArathiBasinFixture.HordeAccountsOrdered
        .ToArray();
    private static readonly IReadOnlyList<string> HordeAssaulterAccounts = HordeFullMapAssaultPlan
        .Select(step => step.Account)
        .ToArray();

    private readonly ArathiBasinFixture _bot;
    private readonly ITestOutputHelper _output;

    public AbObjectiveTests(ArathiBasinFixture bot, ITestOutputHelper output)
    {
        _bot = bot;
        _output = output;
        _bot.SetOutput(output);
        global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ?? "Live bot not ready");
    }

    private void LogPhase(string message)
    {
        _output.WriteLine(message);
        Console.WriteLine(message);
    }

    [SkippableFact]
    public async Task AB_NodeAssault_HordeInteractsWithBlacksmithBanner()
    {
        await PrepareAndEnterBattlegroundAsync();

        var assault = HordeFullMapAssaultPlan.First(step => step.Name == "Blacksmith");
        await CapturePacketTraceAroundAssaultAsync(
            assault.Account,
            phaseName: "AB:BlacksmithAssault:Packets",
            operation: async () =>
            {
                var baselineSnapshots = await _bot.QueryAllSnapshotsAsync();
                var baselineWorldStateCounts = BgTestHelper.CaptureTrackedChatMatchCounts(
                    baselineSnapshots,
                    AllTrackedAccounts,
                    ContainsAnyWorldStateMessage);

                var baselineObjectiveSurface = await AssaultBannerAsync(
                    assault,
                    objectivePhaseName: "AB:BlacksmithAssault",
                    probeWorldStateAfterAssault: true,
                    worldStateProbeTimeout: TimeSpan.FromSeconds(75));
                var baselineNearbySignature = BuildNearbyObjectiveSurfaceSignature(baselineObjectiveSurface);

                await ObserveTrackedPostAssaultSignalsAsync(
                    assault,
                    baselineObjectiveSurface,
                    baselineWorldStateCounts,
                    baselineNearbySignature,
                    phaseName: "AB:BlacksmithAssault:Observe",
                    maxTimeout: TimeSpan.FromSeconds(90));
            });
    }

    [SkippableFact]
    public async Task AB_FullGame_CompletesToVictoryOrDefeat()
    {
        await BgTestHelper.WaitForBotsAsync(_bot, _output, ArathiBasinFixture.TotalBotCount, "AB:Baseline");
        await _bot.RefreshSnapshotsAsync();
        var baselineMarkCounts = BgTestHelper.CaptureTrackedBagItemCounts(
            _bot.AllBots,
            HordeAssaulterAccounts,
            BgRewardCollectionTask.AbMarkOfHonor);
        await PrepareAndEnterBattlegroundAsync();

        foreach (var assault in HordeFullMapAssaultPlan)
        {
            await AssaultBannerAsync(assault, objectivePhaseName: $"AB:{assault.Name}");
            await Task.Delay(500);
        }

        await BgTestHelper.WaitForBattlegroundCompletionAsync(
            _bot,
            _output,
            HordeAssaulterAccounts,
            ArathiBasinFixture.AbMapId,
            phaseName: "AB:Completion",
            queryTrackedAccountsIndividually: true,
            maxTimeout: TimeSpan.FromMinutes(35),
            completionChatPredicate: ContainsBattlegroundResultMessage);

        var rewardCounts = await BgTestHelper.WaitForTrackedBagItemIncreaseAsync(
            _bot,
            _output,
            baselineMarkCounts,
            BgRewardCollectionTask.AbMarkOfHonor,
            phaseName: "AB:Rewards",
            minimumAccountsWithIncrease: 3,
            queryTrackedAccountsIndividually: true,
            maxTimeout: TimeSpan.FromMinutes(2));
        var rewardedAccounts = BgTestHelper.FindAccountsWithBagItemIncrease(baselineMarkCounts, rewardCounts);
        Assert.Contains(HordeFullMapAssaultPlan[0].Account, rewardedAccounts);
    }

    private async Task PrepareAndEnterBattlegroundAsync()
    {
        Assert.True(_bot.IsReady, _bot.FailureReason ?? "Fixture not ready");
        if (!string.IsNullOrWhiteSpace(_bot.BgAccountName))
            await _bot.EnsureCleanSlateAsync(_bot.BgAccountName!, "BG");

        await BgTestHelper.WaitForBotsAsync(_bot, _output, ArathiBasinFixture.TotalBotCount, "AB");
        await _bot.ReprepareAsync();
        Assert.Equal(ResponseResult.Success, await _bot.SetRuntimeCoordinatorEnabledAsync(true));
        await BgTestHelper.WaitForBgEntryAsync(_bot, _output, ArathiBasinFixture.AbMapId, ArathiBasinFixture.TotalBotCount, "AB");
        await BgTestHelper.WaitForTrackedAccountsOnMapStableAsync(
            _bot,
            _output,
            AllTrackedAccounts,
            ArathiBasinFixture.AbMapId,
            phaseName: "AB:BG-Stable",
            stableWindow: TimeSpan.FromSeconds(20),
            maxTimeout: TimeSpan.FromMinutes(4),
            queryTrackedAccountsIndividually: true);

        LogPhase($"[AB:PrepWindow] waiting {ArathiBasinPrepWindow.TotalSeconds:F0}s for gates to open before scripted banner assaults");
        await Task.Delay(ArathiBasinPrepWindow);
        Assert.Equal(ResponseResult.Success, await _bot.SetRuntimeCoordinatorEnabledAsync(false));
        await _bot.QuiesceAccountsAsync(AllTrackedAccounts, "AB:QuiesceAfterPrep");
    }

    private async Task<ObjectiveSurfaceObservation> AssaultBannerAsync(
        AbBannerAssault assault,
        string objectivePhaseName,
        bool probeWorldStateAfterAssault = false,
        TimeSpan? worldStateProbeTimeout = null)
    {
        LogPhase(
            $"[{objectivePhaseName}] teleporting {assault.Account} to {assault.Name} banner entry={assault.Entry} at ({assault.X:F1},{assault.Y:F1},{assault.Z:F1})");
        await _bot.BotTeleportAsync(assault.Account, (int)ArathiBasinFixture.AbMapId, assault.X, assault.Y, assault.Z);

        var banner = await BgTestHelper.WaitForNearbyGameObjectAsync(
            _bot,
            _output,
            assault.Account,
            ArathiBasinFixture.AbMapId,
            gameObject => gameObject.Entry == assault.Entry,
            phaseName: $"{objectivePhaseName}:FindBanner",
            maxTimeout: TimeSpan.FromSeconds(20));

        await _bot.RefreshSnapshotsAsync();
        var preInteractSnapshot = await _bot.GetSnapshotAsync(assault.Account);
        var preInteractObjectiveSurface = CaptureNearbyObjectiveSurface(preInteractSnapshot, assault);
        LogPhase($"[{objectivePhaseName}] preInteractObjectiveObjects={BuildNearbyObjectiveSurfaceSignature(preInteractObjectiveSurface)}");

        var interactResult = await _bot.SendActionAsync(
            assault.Account,
            new ActionMessage
            {
                ActionType = ActionType.InteractWith,
                Parameters = { new RequestParameter { LongParam = (long)banner.Guid } }
            });
        Assert.Equal(ResponseResult.Success, interactResult);
        LogPhase(
            $"[{objectivePhaseName}] {assault.Account} interacted with {assault.Name} banner guid=0x{banner.Guid:X}");

        await BgTestHelper.WaitForNearbyGameObjectAbsentAsync(
            _bot,
            _output,
            assault.Account,
            ArathiBasinFixture.AbMapId,
            gameObject => gameObject.Entry == assault.Entry,
            phaseName: $"{objectivePhaseName}:BannerStateChange",
            maxTimeout: TimeSpan.FromSeconds(15));
        LogPhase($"[{objectivePhaseName}] banner state change observed");

        if (probeWorldStateAfterAssault)
        {
            await LogRecentWorldStateMessagesAsync(
                assault.Account,
                phaseName: $"{objectivePhaseName}:WorldState",
                maxTimeout: worldStateProbeTimeout ?? TimeSpan.FromSeconds(30));
        }

        return preInteractObjectiveSurface;
    }

    private async Task LogRecentWorldStateMessagesAsync(string account, string phaseName, TimeSpan maxTimeout)
    {
        var deadline = DateTime.UtcNow + maxTimeout;
        var lastSignature = string.Empty;

        while (DateTime.UtcNow < deadline)
        {
            var snapshot = await _bot.GetSnapshotAsync(account);
            var worldStateMessages = snapshot?.RecentChatMessages
                .Where(message => message.Contains("[WORLDSTATE", StringComparison.Ordinal))
                .TakeLast(8)
                .ToArray() ?? Array.Empty<string>();
            if (worldStateMessages.Length > 0)
            {
                var signature = string.Join(" || ", worldStateMessages);
                if (!string.Equals(signature, lastSignature, StringComparison.Ordinal))
                {
                    LogPhase($"[{phaseName}] {signature}");
                    lastSignature = signature;
                }

                if (worldStateMessages.Any(message => message.StartsWith("[WORLDSTATE]", StringComparison.Ordinal)))
                    return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        LogPhase($"[{phaseName}] no world-state messages observed within {maxTimeout.TotalSeconds:F0}s");
    }

    private async Task ObserveTrackedPostAssaultSignalsAsync(
        AbBannerAssault assault,
        ObjectiveSurfaceObservation baselineObjectiveSurface,
        IReadOnlyDictionary<string, int> baselineWorldStateCounts,
        string baselineNearbySignature,
        string phaseName,
        TimeSpan maxTimeout)
    {
        var deadline = DateTime.UtcNow + maxTimeout;
        var lastNearbySignature = baselineNearbySignature;
        var lastProgressAt = DateTime.MinValue;
        var loggedWorldStateAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loggedReplacementBanner = false;
        var loggedObjectiveRemoval = false;

        while (DateTime.UtcNow < deadline)
        {
            if (_bot.ClientCrashed)
                Assert.Fail($"[{phaseName}] CRASHED");

            await _bot.RefreshSnapshotsAsync();
            var snapshots = await _bot.QueryAllSnapshotsAsync();

            var worldStateCounts = BgTestHelper.CaptureTrackedChatMatchCounts(
                snapshots,
                AllTrackedAccounts,
                ContainsAnyWorldStateMessage);
            var worldStateAccounts = BgTestHelper.FindAccountsWithChatMatchIncrease(
                baselineWorldStateCounts,
                worldStateCounts);
            var newlyLoggedWorldStateAccounts = worldStateAccounts
                .Where(account => loggedWorldStateAccounts.Add(account))
                .ToArray();

            if (newlyLoggedWorldStateAccounts.Length > 0)
            {
                var worldStateMessages = BgTestHelper.FindTrackedChatMatches(
                    snapshots,
                    newlyLoggedWorldStateAccounts,
                    ContainsAnyWorldStateMessage);
                LogPhase(
                    $"[{phaseName}] tracked world-state deltas on {worldStateAccounts.Count}/{AllTrackedAccounts.Count} accounts: " +
                    $"{string.Join(", ", worldStateAccounts.Take(8))}");
                if (worldStateMessages.Count > 0)
                    LogPhase($"[{phaseName}] worldStateMessages={string.Join(" || ", worldStateMessages.Take(6))}");
            }

            var assaulterSnapshot = snapshots.FirstOrDefault(snapshot =>
                string.Equals(snapshot.AccountName, assault.Account, StringComparison.OrdinalIgnoreCase));
            var nearbyObjectiveSurface = CaptureNearbyObjectiveSurface(assaulterSnapshot, assault);
            var nearbySignature = BuildNearbyObjectiveSurfaceSignature(nearbyObjectiveSurface);
            if (!string.Equals(nearbySignature, lastNearbySignature, StringComparison.Ordinal))
            {
                LogPhase($"[{phaseName}] nearbyObjectiveObjects={nearbySignature}");
                lastNearbySignature = nearbySignature;
            }

            var objectiveMutation = FindNearbyObjectiveMutation(nearbyObjectiveSurface, baselineObjectiveSurface);
            if (!loggedReplacementBanner && objectiveMutation != null)
            {
                loggedReplacementBanner = true;
                LogPhase(
                    $"[{phaseName}] objectiveMutation source={objectiveMutation.Source} entry={objectiveMutation.Entry} guid=0x{objectiveMutation.Guid:X} " +
                    $"name='{objectiveMutation.Name}' dist={objectiveMutation.DistanceToPlayer:F1} " +
                    $"state=type:{objectiveMutation.GameObjectType} goState:{objectiveMutation.GoState} flags:0x{objectiveMutation.Flags:X} " +
                    $"dyn:0x{objectiveMutation.DynamicFlags:X} artKit:{objectiveMutation.ArtKit} anim:{objectiveMutation.AnimProgress} " +
                    $"pos=({objectiveMutation.Position?.X:F1},{objectiveMutation.Position?.Y:F1},{objectiveMutation.Position?.Z:F1})");
            }

            var objectiveRemoval = FindNearbyObjectiveRemoval(nearbyObjectiveSurface, baselineObjectiveSurface);
            if (!loggedObjectiveRemoval && objectiveRemoval != null)
            {
                loggedObjectiveRemoval = true;
                LogPhase(
                    $"[{phaseName}] objectiveRemoval source={objectiveRemoval.Source} entry={objectiveRemoval.Entry} guid=0x{objectiveRemoval.Guid:X} " +
                    $"name='{objectiveRemoval.Name}' dist={objectiveRemoval.DistanceToPlayer:F1} " +
                    $"state=type:{objectiveRemoval.GameObjectType} goState:{objectiveRemoval.GoState} flags:0x{objectiveRemoval.Flags:X} " +
                    $"dyn:0x{objectiveRemoval.DynamicFlags:X} artKit:{objectiveRemoval.ArtKit} anim:{objectiveRemoval.AnimProgress} " +
                    $"pos=({objectiveRemoval.Position?.X:F1},{objectiveRemoval.Position?.Y:F1},{objectiveRemoval.Position?.Z:F1})");
            }

            if (DateTime.UtcNow - lastProgressAt >= TimeSpan.FromSeconds(10))
            {
                var position = assaulterSnapshot?.Player?.Unit?.GameObject?.Base?.Position;
                var chats = assaulterSnapshot?.RecentChatMessages?.TakeLast(4).ToArray() ?? Array.Empty<string>();
                LogPhase(
                    $"[{phaseName}] account={assault.Account} pos=({position?.X:F1},{position?.Y:F1},{position?.Z:F1}) " +
                    $"worldStateAccounts={worldStateAccounts.Count}/{AllTrackedAccounts.Count} " +
                    $"snapshotNearby={nearbyObjectiveSurface.SnapshotObjects.Count} movementNearby={nearbyObjectiveSurface.MovementObjects.Count} " +
                    $"objectiveMutation={(objectiveMutation != null ? $"{objectiveMutation.Source}:0x{objectiveMutation.Guid:X}" : "none")} " +
                    $"objectiveRemoval={(objectiveRemoval != null ? $"{objectiveRemoval.Source}:0x{objectiveRemoval.Guid:X}" : "none")} " +
                    $"recentChats={(chats.Length == 0 ? "(none)" : string.Join(" || ", chats))}");
                lastProgressAt = DateTime.UtcNow;
            }

            if (worldStateAccounts.Count > 0 || objectiveMutation != null)
                return;

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        Assert.Fail(
            $"[{phaseName}] no tracked world-state delta or objective-state mutation appeared within {maxTimeout.TotalSeconds:F0}s");
    }

    private async Task CapturePacketTraceAroundAssaultAsync(
        string account,
        string phaseName,
        Func<Task> operation)
    {
        var recordingDir = RecordingArtifactHelper.GetRecordingDirectory();
        RecordingArtifactHelper.DeleteRecordingArtifacts(recordingDir, account, "packets", "transform", "physics");

        var startRecordingResult = await _bot.SendActionAsync(
            account,
            new ActionMessage { ActionType = ActionType.StartPhysicsRecording });
        Assert.Equal(ResponseResult.Success, startRecordingResult);

        try
        {
            await operation();
        }
        finally
        {
            var stopRecordingResult = await _bot.SendActionAsync(
                account,
                new ActionMessage { ActionType = ActionType.StopPhysicsRecording });
            Assert.Equal(ResponseResult.Success, stopRecordingResult);
        }

        await LogPacketTraceSummaryAsync(account, phaseName, recordingDir);
    }

    private async Task LogPacketTraceSummaryAsync(string account, string phaseName, string recordingDir)
    {
        var packetTracePath = PacketTraceArtifactHelper.WaitForPacketTrace(recordingDir, account, TimeSpan.FromSeconds(5));
        if (string.IsNullOrWhiteSpace(packetTracePath))
        {
            LogPhase($"[{phaseName}] no packet trace captured for {account}");
            return;
        }

        var packets = PacketTraceArtifactHelper.LoadPacketCsv(packetTracePath);
        var interestingOpcodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "CMSG_GAMEOBJ_USE",
            "CMSG_CAST_SPELL",
            "SMSG_BATTLEFIELD_STATUS",
            "SMSG_INIT_WORLD_STATES",
            "SMSG_UPDATE_WORLD_STATE",
            "SMSG_PVP_CREDIT",
            "SMSG_BATTLEGROUND_PLAYER_JOINED",
            "SMSG_BATTLEGROUND_PLAYER_LEFT",
            "SMSG_SPELL_START",
            "SMSG_SPELL_GO",
            "SMSG_SPELL_FAILURE",
            "SMSG_UPDATE_OBJECT",
            "SMSG_COMPRESSED_UPDATE_OBJECT",
            "SMSG_DESTROY_OBJECT"
        };

        var interestingPackets = packets
            .Where(packet => interestingOpcodes.Contains(packet.OpcodeName))
            .ToArray();

        var summary = interestingPackets
            .GroupBy(packet => packet.OpcodeName, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}={group.Count()}")
            .ToArray();
        LogPhase(
            $"[{phaseName}] trace={System.IO.Path.GetFileName(packetTracePath)} totalPackets={packets.Count} interesting={string.Join(", ", summary)}");

        foreach (var packet in interestingPackets.TakeLast(12))
        {
            LogPhase(
                $"[{phaseName}] packet idx={packet.Index} t={packet.ElapsedMs}ms dir={packet.Direction} opcode={packet.OpcodeName} size={packet.Size}");
        }
    }

    private static bool ContainsBattlegroundResultMessage(string message)
    {
        return message.Contains("wins", StringComparison.OrdinalIgnoreCase)
            || message.Contains("victory", StringComparison.OrdinalIgnoreCase)
            || message.Contains("defeat", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAnyWorldStateMessage(string message)
        => message.Contains("[WORLDSTATE", StringComparison.Ordinal);

    private static ObjectiveSurfaceObservation CaptureNearbyObjectiveSurface(
        WoWActivitySnapshot? snapshot,
        AbBannerAssault assault)
        => new(
            CaptureNearbySnapshotObjectiveObjects(snapshot, assault),
            CaptureNearbyMovementObjectiveObjects(snapshot, assault));

    private static IReadOnlyList<ObjectiveObjectObservation> CaptureNearbySnapshotObjectiveObjects(
        WoWActivitySnapshot? snapshot,
        AbBannerAssault assault)
    {
        var playerPosition = snapshot?.Player?.Unit?.GameObject?.Base?.Position;
        return snapshot?.NearbyObjects?
            .Where(gameObject =>
                gameObject.Base?.Position != null
                && Distance2D(gameObject.Base.Position.X, gameObject.Base.Position.Y, assault.X, assault.Y) <= 20f)
            .Select(gameObject =>
                new ObjectiveObjectObservation(
                    "snapshot",
                    gameObject.Base.Guid,
                    gameObject.Entry,
                    gameObject.DisplayId,
                    gameObject.GameObjectType,
                    gameObject.Flags,
                    gameObject.DynamicFlags,
                    gameObject.GoState,
                    gameObject.ArtKit,
                    gameObject.AnimProgress,
                    gameObject.Name ?? string.Empty,
                    gameObject.Base.Position,
                    playerPosition == null
                        ? 0f
                        : Distance2D(gameObject.Base.Position.X, gameObject.Base.Position.Y, playerPosition.X, playerPosition.Y)))
            .OrderBy(gameObject => gameObject.DistanceToPlayer)
            .ThenBy(gameObject => gameObject.Entry)
            .ThenBy(gameObject => gameObject.Guid)
            .ToArray()
            ?? Array.Empty<ObjectiveObjectObservation>();
    }

    private static IReadOnlyList<ObjectiveObjectObservation> CaptureNearbyMovementObjectiveObjects(
        WoWActivitySnapshot? snapshot,
        AbBannerAssault assault)
    {
        return snapshot?.MovementData?.NearbyGameObjects?
            .Where(gameObject =>
                gameObject.Position != null
                && Distance2D(gameObject.Position.X, gameObject.Position.Y, assault.X, assault.Y) <= 20f)
            .Select(gameObject =>
                new ObjectiveObjectObservation(
                    "movement",
                    gameObject.Guid,
                    gameObject.Entry,
                    gameObject.DisplayId,
                    gameObject.GameObjectType,
                    gameObject.Flags,
                    0u,
                    gameObject.GoState,
                    0u,
                    gameObject.AnimProgress,
                    gameObject.Name ?? string.Empty,
                    gameObject.Position,
                    gameObject.DistanceToPlayer))
            .OrderBy(gameObject => gameObject.DistanceToPlayer)
            .ThenBy(gameObject => gameObject.Entry)
            .ThenBy(gameObject => gameObject.Guid)
            .ToArray()
            ?? Array.Empty<ObjectiveObjectObservation>();
    }

    private static string BuildNearbyObjectiveSurfaceSignature(ObjectiveSurfaceObservation nearbySurface)
        => $"snapshot=[{BuildNearbyObjectiveObjectSignature(nearbySurface.SnapshotObjects)}]; movement=[{BuildNearbyObjectiveObjectSignature(nearbySurface.MovementObjects)}]";

    private static string BuildNearbyObjectiveObjectSignature(IReadOnlyList<ObjectiveObjectObservation> nearbyObjects)
    {
        var signatureParts = nearbyObjects
            .Select(gameObject =>
                $"{gameObject.Source}:{gameObject.Entry}:0x{gameObject.Guid:X}:{gameObject.Name}" +
                $"@{gameObject.DistanceToPlayer:F1}" +
                $"[disp={gameObject.DisplayId} type={gameObject.GameObjectType} goState={gameObject.GoState} " +
                $"flags=0x{gameObject.Flags:X} dyn=0x{gameObject.DynamicFlags:X} artKit={gameObject.ArtKit} anim={gameObject.AnimProgress}]")
            .ToArray();
        return signatureParts.Length == 0 ? "(none)" : string.Join(", ", signatureParts);
    }

    private static ObjectiveObjectObservation? FindNearbyObjectiveMutation(
        ObjectiveSurfaceObservation nearbySurface,
        ObjectiveSurfaceObservation baselineSurface)
    {
        var baselineStateKeys = new HashSet<string>(
            EnumerateObjectiveObjects(baselineSurface).Select(BuildObjectiveStateKey),
            StringComparer.Ordinal);

        return EnumerateObjectiveObjects(nearbySurface)
            .FirstOrDefault(gameObject => !baselineStateKeys.Contains(BuildObjectiveStateKey(gameObject)));
    }

    private static ObjectiveObjectObservation? FindNearbyObjectiveRemoval(
        ObjectiveSurfaceObservation nearbySurface,
        ObjectiveSurfaceObservation baselineSurface)
    {
        var currentStateKeys = new HashSet<string>(
            EnumerateObjectiveObjects(nearbySurface).Select(BuildObjectiveStateKey),
            StringComparer.Ordinal);

        return EnumerateObjectiveObjects(baselineSurface)
            .FirstOrDefault(gameObject => !currentStateKeys.Contains(BuildObjectiveStateKey(gameObject)));
    }

    private static IEnumerable<ObjectiveObjectObservation> EnumerateObjectiveObjects(ObjectiveSurfaceObservation surface)
        => surface.SnapshotObjects.Concat(surface.MovementObjects);

    private static string BuildObjectiveStateKey(ObjectiveObjectObservation gameObject)
        => $"{gameObject.Source}:{gameObject.Guid:X16}:{gameObject.Entry}:{gameObject.DisplayId}:{gameObject.GameObjectType}:{gameObject.Flags}:{gameObject.DynamicFlags}:{gameObject.GoState}:{gameObject.ArtKit}:{gameObject.AnimProgress}:{gameObject.Name}:{gameObject.Position?.X:F2}:{gameObject.Position?.Y:F2}:{gameObject.Position?.Z:F2}";

    private static float Distance2D(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private sealed record AbBannerAssault(
        string Account,
        string Name,
        uint Entry,
        float X,
        float Y,
        float Z);

    private sealed record ObjectiveObjectObservation(
        string Source,
        ulong Guid,
        uint Entry,
        uint DisplayId,
        uint GameObjectType,
        uint Flags,
        uint DynamicFlags,
        uint GoState,
        uint ArtKit,
        uint AnimProgress,
        string Name,
        global::Game.Position? Position,
        float DistanceToPlayer);

    private sealed record ObjectiveSurfaceObservation(
        IReadOnlyList<ObjectiveObjectObservation> SnapshotObjects,
        IReadOnlyList<ObjectiveObjectObservation> MovementObjects);
}
