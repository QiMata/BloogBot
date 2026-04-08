using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System.Runtime.InteropServices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using ForegroundBotRunner.Diagnostics;

namespace ForegroundBotRunner.Statics
{
    public partial class ObjectManager
    {


        public HighGuid PlayerGuid { get; internal set; } = new HighGuid(new byte[4], new byte[4]);

#pragma warning disable CS0414 // State flags assigned in ObjectManager.Interaction.cs but not yet read
        private bool _ingame1;
        private bool _ingame2;
#pragma warning restore CS0414

        private readonly IWoWActivitySnapshot _characterState;



        public IWoWLocalPlayer Player { get; internal set; }

        /// <summary>
        /// Native pointer to the local player object. Used by WoWUnit.UnitReaction
        /// to call GetUnitReaction(playerPtr, unitPtr) instead of (unitPtr, unitPtr).
        /// </summary>
        internal static nint PlayerPointer { get; set; }



        public IWoWLocalPet Pet { get; private set; }

        /// <summary>
        /// Event fired when the player first enters the world after login.
        /// This fires from the enumeration thread (inside ThreadSynchronizer) when HasEnteredWorld becomes true.
        /// Subscribers should use this to immediately send snapshots while the player is definitely in-world.
        /// </summary>


        /// <summary>
        /// Event fired when the player first enters the world after login.
        /// This fires from the enumeration thread (inside ThreadSynchronizer) when HasEnteredWorld becomes true.
        /// Subscribers should use this to immediately send snapshots while the player is definitely in-world.
        /// </summary>


        public ulong StarTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Star, true);


        public ulong CircleTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Circle, true);


        public ulong DiamondTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Diamond, true);


        public ulong TriangleTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Triangle, true);


        public ulong MoonTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Moon, true);


        public ulong SquareTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Square, true);


        public ulong CrossTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Cross, true);


        public ulong SkullTargetGuid => MemoryManager.ReadUlong((nint)Offsets.RaidIcon.Skull, true);

        // Note: Memory offset 0xB4B424 (IsIngame) returns 0 on this WoW client version (Elysium).
        // Note: Functions.GetPlayerGuid() returns an object manager index (e.g., 5), not the actual GUID.
        // Reading the player GUID directly from memory: [ManagerBase] + PlayerGuidOffset


        // Note: Memory offset 0xB4B424 (IsIngame) returns 0 on this WoW client version (Elysium).
        // Note: Functions.GetPlayerGuid() returns an object manager index (e.g., 5), not the actual GUID.
        // Reading the player GUID directly from memory: [ManagerBase] + PlayerGuidOffset


        // Note: Memory offset 0xB4B424 (IsIngame) returns 0 on this WoW client version (Elysium).
        // Note: Functions.GetPlayerGuid() returns an object manager index (e.g., 5), not the actual GUID.
        // Reading the player GUID directly from memory: [ManagerBase] + PlayerGuidOffset


        // Note: Memory offset 0xB4B424 (IsIngame) returns 0 on this WoW client version (Elysium).
        // Note: Functions.GetPlayerGuid() returns an object manager index (e.g., 5), not the actual GUID.
        // Reading the player GUID directly from memory: [ManagerBase] + PlayerGuidOffset
        private static int _guidLogCount = 0;

        // Cached player GUID - updated via ThreadSynchronizer to ensure main thread context
        // Functions.GetPlayerGuid() only works reliably from the main WoW thread


        // Cached player GUID - updated via ThreadSynchronizer to ensure main thread context
        // Functions.GetPlayerGuid() only works reliably from the main WoW thread


        // Cached player GUID - updated via ThreadSynchronizer to ensure main thread context
        // Functions.GetPlayerGuid() only works reliably from the main WoW thread


        // Cached player GUID - updated via ThreadSynchronizer to ensure main thread context
        // Functions.GetPlayerGuid() only works reliably from the main WoW thread
        private static ulong _cachedPlayerGuid = 0;


        private static readonly object _guidCacheLock = new();



        public bool IsLoggedIn
        {
            get
            {
                _isLoggedInCallCount++;

                // Fast path: if we have a cached GUID, we're logged in
                lock (_guidCacheLock)
                {
                    if (_cachedPlayerGuid != 0) return true;
                }

                // During world entry phase, use memory-only to avoid ThreadSynchronizer interference
                if (PauseNativeCallsDuringWorldEntry)
                {
                    // Memory-only detection: read GUID from memory
                    var memGuid = GetPlayerGuidFromMemory();
                    if (memGuid != 0)
                    {
                        lock (_guidCacheLock)
                        {
                            _cachedPlayerGuid = memGuid;
                            if (_isLoggedInCallCount <= 10)
                            {
                                DiagLog($"IsLoggedIn[MemoryOnly]: Cached GUID={memGuid} from memory (PauseNative={PauseNativeCallsDuringWorldEntry})");
                            }
                        }
                        return true;
                    }
                    return false;
                }

                // Slow path: check via main thread and cache the result (Mode 2+)
                try
                {
                    var guid = ThreadSynchronizer.RunOnMainThread(() => Functions.GetPlayerGuid());
                    if (guid != 0)
                    {
                        lock (_guidCacheLock)
                        {
                            _cachedPlayerGuid = guid;
                            DiagLog($"IsLoggedIn: Cached GUID={guid} via ThreadSynchronizer");
                        }
                        return true;
                    }
                    else
                    {
                        // Log GUID=0 result periodically (first 10, then every 20th call)
                        if (_isLoggedInCallCount <= 10 || _isLoggedInCallCount % 20 == 0)
                        {
                            DiagLog($"IsLoggedIn[{_isLoggedInCallCount}]: GetPlayerGuid returned 0 (not logged in)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    DiagLog($"IsLoggedIn: ThreadSynchronizer error: {ex.Message}");
                }

                return false;
            }
        }

        /// <summary>
        /// Clears the cached player GUID. Call this when logout is detected.
        /// </summary>


        /// <summary>
        /// Clears the cached player GUID. Call this when logout is detected.
        /// </summary>


        /// <summary>
        /// Clears the cached player GUID. Call this when logout is detected.
        /// </summary>


        /// <summary>
        /// Clears the cached player GUID. Call this when logout is detected.
        /// </summary>
        internal static void ClearCachedGuid()
        {
            lock (_guidCacheLock)
            {
                if (_cachedPlayerGuid != 0)
                {
                    DiagLog($"ClearCachedGuid: Clearing cached GUID={_cachedPlayerGuid}");
                    _cachedPlayerGuid = 0;
                }
            }
        }

        /// <summary>
        /// Updates the cached player GUID. Call this when a valid GUID is detected.
        /// </summary>


        /// <summary>
        /// Updates the cached player GUID. Call this when a valid GUID is detected.
        /// </summary>


        /// <summary>
        /// Updates the cached player GUID. Call this when a valid GUID is detected.
        /// </summary>


        /// <summary>
        /// Updates the cached player GUID. Call this when a valid GUID is detected.
        /// </summary>
        internal static void UpdateCachedGuid(ulong guid)
        {
            if (guid == 0) return;
            lock (_guidCacheLock)
            {
                if (_cachedPlayerGuid != guid)
                {
                    DiagLog($"UpdateCachedGuid: Updating cached GUID from {_cachedPlayerGuid} to {guid}");
                    _cachedPlayerGuid = guid;
                }
            }
        }

        // Volatile to ensure visibility across threads (enumeration thread sets, main loop reads)


        // Volatile to ensure visibility across threads (enumeration thread sets, main loop reads)


        public void AntiAfk()
        {
            var tickCount = Environment.TickCount;
            MemoryManager.WriteInt(MemoryAddresses.LastHardwareAction, tickCount);

            // Log every 20th call (every 10 seconds at 500ms intervals) to verify it's working
            if (++_antiAfkLogCounter % 20 == 1)
            {
                var readBack = MemoryManager.ReadInt((nint)MemoryAddresses.LastHardwareAction);
                var logPath = RecordingFileArtifactGate.ResolveDocumentsPath("BloogBot", "antiafk_log.txt");
                if (string.IsNullOrWhiteSpace(logPath))
                {
                    return;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                    var line = $"[{DateTime.Now:HH:mm:ss}] AntiAfk: wrote {tickCount}, readback={readBack}, match={tickCount == readBack}\n";
                    File.AppendAllText(logPath, line);
                }
                catch { }
            }
        }



        public string ZoneText
        {
            // this is weird and throws an exception right after entering world,
            // so we catch and ignore the exception to avoid console noise
            get
            {
                try
                {
                    var ptr = MemoryManager.ReadIntPtr(MemoryAddresses.ZoneTextPtr);
                    return MemoryManager.ReadString(ptr);
                }
                catch (Exception)
                {
                    return "";
                }
            }
        }



        public string MinimapZoneText
        {
            // this is weird and throws an exception right after entering world,
            // so we catch and ignore the exception to avoid console noise
            get
            {
                try
                {
                    var ptr = MemoryManager.ReadIntPtr(MemoryAddresses.MinimapZoneTextPtr);
                    return MemoryManager.ReadString(ptr);
                }
                catch (Exception)
                {
                    return "";
                }
            }
        }



        public string ServerName
        {
            // this is weird and throws an exception right after entering world,
            // so we catch and ignore the exception to avoid console noise
            get
            {
                try
                {
                    // not exactly sure how this works. seems to return a string like "Endless\WoW.exe" or "Karazhan\WoW.exe"
                    var fullName = MemoryManager.ReadString(MemoryAddresses.ServerName);
                    return fullName.Split('\\').First();
                }
                catch (Exception)
                {
                    return "";
                }
            }
        }



        public IEnumerable<IWoWPlayer> PartyMembers
        {
            get
            {
                var partyMembers = new List<IWoWPlayer>() { Player };

                var partyMember1 = (WoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party1Guid);
                if (partyMember1 != null)
                    partyMembers.Add(partyMember1);

                var partyMember2 = (WoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party2Guid);
                if (partyMember2 != null)
                    partyMembers.Add(partyMember2);

                var partyMember3 = (WoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party3Guid);
                if (partyMember3 != null)
                    partyMembers.Add(partyMember3);

                var partyMember4 = (WoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party4Guid);
                if (partyMember4 != null)
                    partyMembers.Add(partyMember4);

                return partyMembers;
            }
        }



        public IWoWPlayer PartyLeader => Players.FirstOrDefault(p => p.Guid == PartyLeaderGuid);



        public ulong PartyLeaderGuid => MemoryManager.ReadUlong(MemoryAddresses.PartyLeaderGuid);


        public ulong Party1Guid => MemoryManager.ReadUlong(MemoryAddresses.Party1Guid);


        public ulong Party2Guid => MemoryManager.ReadUlong(MemoryAddresses.Party2Guid);


        public ulong Party3Guid => MemoryManager.ReadUlong(MemoryAddresses.Party3Guid);


        public ulong Party4Guid => MemoryManager.ReadUlong(MemoryAddresses.Party4Guid);


        public List<CharacterSelect> CharacterSelects => [];
    }
}
