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

namespace ForegroundBotRunner.Statics
{
    internal readonly record struct SpellKnowledgeState(
        HashSet<uint> PublishedIds,
        HashSet<uint> StickyLearnedIds,
        HashSet<uint> StickyRemovedIds);

    internal static class SpellKnowledgeReconciler
    {
        internal static SpellKnowledgeState Reconcile(
            IReadOnlyCollection<uint> stableIds,
            IReadOnlyCollection<uint> stickyLearnedIds,
            IReadOnlyCollection<uint> stickyRemovedIds)
        {
            var nextStickyLearnedIds = new HashSet<uint>(stickyLearnedIds);
            var nextStickyRemovedIds = new HashSet<uint>(stickyRemovedIds);

            foreach (var stableId in stableIds)
            {
                nextStickyLearnedIds.Remove(stableId);
                nextStickyRemovedIds.Remove(stableId);
            }

            var publishedIds = new HashSet<uint>(stableIds);
            publishedIds.UnionWith(nextStickyLearnedIds);
            publishedIds.ExceptWith(nextStickyRemovedIds);

            return new SpellKnowledgeState(publishedIds, nextStickyLearnedIds, nextStickyRemovedIds);
        }
    }

    public partial class ObjectManager
    {

        // https://vanilla-wow.fandom.com/wiki/API_GetTalentInfo
        // tab index is 1, 2 or 3
        // talentIndex is counter left to right, top to bottom, starting at 1


        // https://vanilla-wow.fandom.com/wiki/API_GetTalentInfo
        // tab index is 1, 2 or 3
        // talentIndex is counter left to right, top to bottom, starting at 1
        public static sbyte GetTalentRank(int tabIndex, int talentIndex)
        {
            var results = MainThreadLuaCallWithResult($"{{0}}, {{1}}, {{2}}, {{3}}, {{4}} = GetTalentInfo({tabIndex},{talentIndex})");

            if (results.Length == 5)
                return Convert.ToSByte(results[4]);

            return -1;
        }



        public void RefreshSpells()
        {
            if (Player is not LocalPlayer localPlayer)
            {
                // Throttled diagnostic: log when Player is not a LocalPlayer so we can detect
                // the window after SMSG_UPDATE_OBJECT recreates the player object.
                if ((DateTime.UtcNow - _lastSpellDiagUtc).TotalSeconds >= 2.0)
                {
                    _lastSpellDiagUtc = DateTime.UtcNow;
                    var playerType = Player?.GetType().Name ?? "null";
                    DiagLog($"[SPELLBOOK] RefreshSpells: Player is not LocalPlayer (type={playerType}), _lastKnownSpellIds.Count={_lastKnownSpellIds.Count}");
                }
                return;
            }

            // Throttle to once per 2 seconds — unless a LEARNED_SPELL/UNLEARNED_SPELL event
            // fired since the last refresh (in which case _forceSpellRefresh bypasses the wait).
            var forced = _forceSpellRefresh;
            var hasPendingSpellEvent = forced
                || _eventLearnedIds.Count > 0
                || _eventRemovedIds.Count > 0
                || _pendingLearnedSpellNames.Count > 0
                || _pendingUnlearnedSpellNames.Count > 0;
            if (!forced && (DateTime.UtcNow - _lastSpellRefreshUtc).TotalSeconds < 2.0)
                return;
            _forceSpellRefresh = false;
            _lastSpellRefreshUtc = DateTime.UtcNow;
            if (forced)
                DiagLog("[SPELLBOOK] RefreshSpells: forced by LEARNED_SPELL/UNLEARNED_SPELL event");

            // Reset spell list when the logged-in character changes.
            var charName = localPlayer.Name ?? string.Empty;
            if (charName != _persistentLearnedCharacter)
            {
                var previousCharacter = _persistentLearnedCharacter;
                _persistentLearnedIds.Clear();
                _eventLearnedIds.Clear();
                _eventRemovedIds.Clear();
                _pendingLearnedSpellNames.Clear();
                _pendingUnlearnedSpellNames.Clear();
                _persistentLearnedCharacter = charName;
                _initialSpellsSeeded = false;
                _lastKnownSpellIds = Array.Empty<uint>();
                _spellDeltaEventsArmed = false;
                DiagLog($"[SPELLBOOK] Character changed -> '{charName}' (was '{previousCharacter}'), resetting spell list");
            }

            // STEP 1: Always scan the static spell array at 0x00B700F0.
            // This array is populated at world entry from SMSG_INITIAL_SPELLS and updated by the
            // WoW client when spells are learned/unlearned mid-session (SMSG_LEARNED_SPELL /
            // SMSG_REMOVED_SPELL). Does NOT require the spell name cache — reads raw uint IDs.
            // Clear and rebuild each tick so unlearned spells (.unlearn, SMSG_REMOVED_SPELL)
            // are reflected. The 2s throttle prevents excessive rebuilds. Lua and talent
            // enumeration steps below re-add any spells not in the static array.
            var stableSpellIds = new HashSet<uint>();
            {
                int scanned = 0;
                for (int i = 0; i < 1024; i++)
                {
                    var spellId = (uint)MemoryManager.ReadInt(MemoryAddresses.LocalPlayerSpellsBase + i * 4);
                    if (spellId == 0) break;
                    stableSpellIds.Add(spellId);
                    scanned++;
                }
                if (scanned > 0 && !_initialSpellsSeeded)
                {
                    _initialSpellsSeeded = true;
                    DiagLog($"[SPELLBOOK] Static array: first scan found {scanned} spells");
                }
                else if (scanned == 0 && _initialSpellsSeeded)
                {
                    // Array returned 0 spells after we already seeded — log this anomaly.
                    DiagLog($"[SPELLBOOK] WARNING: static array returned 0 entries (previously seeded={_initialSpellsSeeded}, persistent={_persistentLearnedIds.Count})");
                }
            }

            // STEP 2: Try to build name→ID cache (needed for Lua tab enumeration and LEARNED_SPELL
            // name-based lookup). Non-blocking — just returns false if not ready yet.
            EnsureSpellNameCache();

            if (_spellNameCacheBuilt)
            {
                // STEP 3: Flush any LEARNED_SPELL events that arrived before the cache was ready.
                FlushPendingSpellNameDeltas();

                // STEP 4: Enumerate spell book via Lua using GetNumSpellTabs/GetSpellTabInfo.
                // This is the correct vanilla 1.12.1 API (GetNumSpells does not exist in 1.12.1).
                // Enumerates ALL spell book entries including passive spells learned via .learn.
                // Passive spells appear as grey icons in spell book tabs after being learned.
                try
                {
                    var luaResult = Functions.LuaCallWithResult(
                        "local r='' " +
                        "local tabs=GetNumSpellTabs() " +
                        "if tabs and tabs>0 then " +
                        "for t=1,tabs do " +
                        "local _,_,off,cnt=GetSpellTabInfo(t) " +
                        "if off and cnt then " +
                        "for i=off+1,off+cnt do " +
                        "local n=GetSpellName(i,'spell') " +
                        "if n and n~='' then r=r..'|'..n end " +
                        "end end end end {0}=r");

                    if (luaResult != null && luaResult.Length > 0 && !string.IsNullOrEmpty(luaResult[0]))
                    {
                        var names = luaResult[0].Split('|', StringSplitOptions.RemoveEmptyEntries);
                        int added = MergeNamedSpellIdsIntoStableSet(names, stableSpellIds);

                        if (added > 0)
                            DiagLog($"[SPELLBOOK] Lua tabs: {names.Length} names, +{added} new IDs, total={stableSpellIds.Count}");
                    }
                    else
                    {
                        DiagLog($"[SPELLBOOK] Lua tabs: empty result (spell book not loaded yet?)");
                    }
                }
                catch (Exception ex)
                {
                    DiagLog($"[SPELLBOOK] Lua enum error: {ex.Message}");
                }

                // STEP 5: Enumerate talent data to catch passive talent spells (e.g. Deflection 16462).
                // Passive talents do NOT appear in the spell book tabs (GetSpellTabInfo/GetSpellName)
                // and may NOT be in the static array at 0x00B700F0 when learned via GM .learn command.
                // GetTalentInfo returns the currently allocated rank for each talent.
                // If currentRank > 0, the talent is learned and we add its IDs via the name cache.
                // NOTE: GM .learn teaches the spell directly but may show rank in the talent API too.
                try
                {
                    var talentResult = Functions.LuaCallWithResult(
                        "local r='' " +
                        "local tabs=GetNumTalentTabs() " +
                        "if tabs and tabs>0 then " +
                        "for t=1,tabs do " +
                        "local n=GetNumTalents(t) " +
                        "if n then " +
                        "for i=1,n do " +
                        "local name,_,_,_,cur=GetTalentInfo(t,i) " +
                        "if name and cur and cur>0 then r=r..'|'..name end " +
                        "end end end end {0}=r");

                    if (talentResult != null && talentResult.Length > 0 && !string.IsNullOrEmpty(talentResult[0]))
                    {
                        var names = talentResult[0].Split('|', StringSplitOptions.RemoveEmptyEntries);
                        int added = MergeNamedSpellIdsIntoStableSet(names, stableSpellIds);

                        if (added > 0)
                            DiagLog($"[SPELLBOOK] Lua talents: {names.Length} spent, +{added} new IDs, total={stableSpellIds.Count}");
                    }
                }
                catch (Exception ex)
                {
                    DiagLog($"[SPELLBOOK] Lua talent enum error: {ex.Message}");
                }
            }

            var previousPublishedSpellIds = _lastKnownSpellIds;
            var reconciledSpellKnowledge = SpellKnowledgeReconciler.Reconcile(
                stableSpellIds,
                _eventLearnedIds,
                _eventRemovedIds);
            _eventLearnedIds = reconciledSpellKnowledge.StickyLearnedIds;
            _eventRemovedIds = reconciledSpellKnowledge.StickyRemovedIds;
            PublishKnownSpellIds(localPlayer, reconciledSpellKnowledge.PublishedIds);
            FireSpellDeltaEvents(previousPublishedSpellIds, reconciledSpellKnowledge.PublishedIds, hasPendingSpellEvent);

            // Diagnostic
            if ((DateTime.UtcNow - _lastSpellDiagUtc).TotalSeconds >= 2)
            {
                _lastSpellDiagUtc = DateTime.UtcNow;
                var found = _persistentLearnedIds.Contains(16462);
                DiagLog($"SPELLBOOK-DIAG: 16462 {(found ? "FOUND" : "NOT FOUND")} " +
                        $"(total={_persistentLearnedIds.Count}, seeded={_initialSpellsSeeded}, cacheBuilt={_spellNameCacheBuilt})");
            }
        }

        private int MergeNamedSpellIdsIntoStableSet(IEnumerable<string> names, HashSet<uint> stableSpellIds)
        {
            int added = 0;
            foreach (var name in names)
            {
                if (!_spellNameToIds.TryGetValue(name.Trim(), out var ids))
                    continue;

                foreach (var id in ids)
                {
                    if (stableSpellIds.Add(id))
                        added++;
                }
            }

            return added;
        }

        private void FlushPendingSpellNameDeltas()
        {
            FlushPendingSpellNameDeltas(_pendingLearnedSpellNames, learned: true);
            FlushPendingSpellNameDeltas(_pendingUnlearnedSpellNames, learned: false);
        }

        private void FlushPendingSpellNameDeltas(List<string> pendingNames, bool learned)
        {
            if (pendingNames.Count == 0)
                return;

            var pending = pendingNames.ToArray();
            pendingNames.Clear();
            foreach (var name in pending)
            {
                if (_spellNameToIds.TryGetValue(name, out var ids))
                    ApplyResolvedSpellIds(ids, learned);
                else
                    pendingNames.Add(name);
            }
        }

        private void ApplyResolvedSpellIds(IEnumerable<uint> ids, bool learned)
        {
            foreach (var id in ids)
            {
                if (learned)
                {
                    _eventRemovedIds.Remove(id);
                    _eventLearnedIds.Add(id);
                }
                else
                {
                    _eventLearnedIds.Remove(id);
                    _eventRemovedIds.Add(id);
                }
            }
        }

        private void PublishKnownSpellIds(LocalPlayer localPlayer, IReadOnlyCollection<uint> knownSpellIds)
        {
            var spellSnapshot = new HashSet<uint>(knownSpellIds);
            _persistentLearnedIds = spellSnapshot;
            localPlayer.RawSpellBookIds = spellSnapshot;
            _lastKnownSpellIds = spellSnapshot;
        }

        private void FireSpellDeltaEvents(
            IReadOnlyCollection<uint> previousKnownSpellIds,
            IReadOnlyCollection<uint> nextKnownSpellIds,
            bool hasPendingSpellEvent)
        {
            if (!_spellDeltaEventsArmed)
            {
                _spellDeltaEventsArmed = true;
                return;
            }

            if (!hasPendingSpellEvent || EventHandler is not WoWEventHandler eventHandler)
                return;

            foreach (var spellId in nextKnownSpellIds.Except(previousKnownSpellIds).OrderBy(id => id))
                eventHandler.FireOnLearnedSpell(spellId);

            foreach (var spellId in previousKnownSpellIds.Except(nextKnownSpellIds).OrderBy(id => id))
                eventHandler.FireOnUnlearnedSpell(spellId);
        }



        private DateTime _lastSpellDiagUtc = DateTime.MinValue;


        private DateTime _lastSpellRefreshUtc = DateTime.MinValue;


        private bool _initialSpellsSeeded = false;

        /// <summary>
        /// Set by the LEARNED_SPELL / UNLEARNED_SPELL event handler to bypass the 2-second
        /// RefreshSpells throttle and do an immediate rescan on the next bot-loop tick.
        /// This is necessary because WoW fires these events via the no-args SignalEvent function,
        /// so the spell name is not available to do a name-based lookup. Instead we rely on the
        /// static array and Lua GetTalentInfo to detect the change.
        /// </summary>


        /// <summary>
        /// Set by the LEARNED_SPELL / UNLEARNED_SPELL event handler to bypass the 2-second
        /// RefreshSpells throttle and do an immediate rescan on the next bot-loop tick.
        /// This is necessary because WoW fires these events via the no-args SignalEvent function,
        /// so the spell name is not available to do a name-based lookup. Instead we rely on the
        /// static array and Lua GetTalentInfo to detect the change.
        /// </summary>


        /// <summary>
        /// Set by the LEARNED_SPELL / UNLEARNED_SPELL event handler to bypass the 2-second
        /// RefreshSpells throttle and do an immediate rescan on the next bot-loop tick.
        /// This is necessary because WoW fires these events via the no-args SignalEvent function,
        /// so the spell name is not available to do a name-based lookup. Instead we rely on the
        /// static array and Lua GetTalentInfo to detect the change.
        /// </summary>


        /// <summary>
        /// Set by the LEARNED_SPELL / UNLEARNED_SPELL event handler to bypass the 2-second
        /// RefreshSpells throttle and do an immediate rescan on the next bot-loop tick.
        /// This is necessary because WoW fires these events via the no-args SignalEvent function,
        /// so the spell name is not available to do a name-based lookup. Instead we rely on the
        /// static array and Lua GetTalentInfo to detect the change.
        /// </summary>
        private volatile bool _forceSpellRefresh = false;

        /// <summary>
        /// Accumulated spell IDs for the current character: initial spells (from 0x00B700F0 at
        /// world entry) plus dynamically learned spells (from LEARNED_SPELL WoW event via hook).
        /// Reset when the logged-in character changes.
        /// </summary>


        /// <summary>
        /// Accumulated spell IDs for the current character: initial spells (from 0x00B700F0 at
        /// world entry) plus dynamically learned spells (from LEARNED_SPELL WoW event via hook).
        /// Reset when the logged-in character changes.
        /// </summary>


        /// <summary>
        /// Accumulated spell IDs for the current character: initial spells (from 0x00B700F0 at
        /// world entry) plus dynamically learned spells (from LEARNED_SPELL WoW event via hook).
        /// Reset when the logged-in character changes.
        /// </summary>


        /// <summary>
        /// Accumulated spell IDs for the current character: initial spells (from 0x00B700F0 at
        /// world entry) plus dynamically learned spells (from LEARNED_SPELL WoW event via hook).
        /// Reset when the logged-in character changes.
        /// </summary>
        private HashSet<uint> _persistentLearnedIds = new();

        private HashSet<uint> _eventLearnedIds = new();

        private HashSet<uint> _eventRemovedIds = new();


        private string _persistentLearnedCharacter = string.Empty;

        /// <summary>
        /// Thread-safe snapshot of the last known spell set. Updated by both RefreshSpells()
        /// (bot-loop thread) and the LEARNED_SPELL event handler (WoW main thread).
        /// Volatile ensures cross-thread visibility of the latest reference.
        /// KnownSpellIds reads from this directly, bypassing LocalPlayer.RawSpellBookIds
        /// so newly-learned spells are visible even if RefreshSpells() hasn't run yet.
        /// </summary>


        /// <summary>
        /// Thread-safe snapshot of the last known spell set. Updated by both RefreshSpells()
        /// (bot-loop thread) and the LEARNED_SPELL event handler (WoW main thread).
        /// Volatile ensures cross-thread visibility of the latest reference.
        /// KnownSpellIds reads from this directly, bypassing LocalPlayer.RawSpellBookIds
        /// so newly-learned spells are visible even if RefreshSpells() hasn't run yet.
        /// </summary>


        /// <summary>
        /// Thread-safe snapshot of the last known spell set. Updated by both RefreshSpells()
        /// (bot-loop thread) and the LEARNED_SPELL event handler (WoW main thread).
        /// Volatile ensures cross-thread visibility of the latest reference.
        /// KnownSpellIds reads from this directly, bypassing LocalPlayer.RawSpellBookIds
        /// so newly-learned spells are visible even if RefreshSpells() hasn't run yet.
        /// </summary>


        /// <summary>
        /// Thread-safe snapshot of the last known spell set. Updated by both RefreshSpells()
        /// (bot-loop thread) and the LEARNED_SPELL event handler (WoW main thread).
        /// Volatile ensures cross-thread visibility of the latest reference.
        /// KnownSpellIds reads from this directly, bypassing LocalPlayer.RawSpellBookIds
        /// so newly-learned spells are visible even if RefreshSpells() hasn't run yet.
        /// </summary>
        private volatile IReadOnlyCollection<uint> _lastKnownSpellIds = Array.Empty<uint>();

        private bool _spellDeltaEventsArmed = false;

        /// <summary>
        /// Spell names queued from LEARNED_SPELL events that arrived before the name→ID cache
        /// was ready. Flushed into _persistentLearnedIds once the cache is built.
        /// </summary>


        /// <summary>
        /// Spell names queued from LEARNED_SPELL events that arrived before the name→ID cache
        /// was ready. Flushed into _persistentLearnedIds once the cache is built.
        /// </summary>


        /// <summary>
        /// Spell names queued from LEARNED_SPELL events that arrived before the name→ID cache
        /// was ready. Flushed into _persistentLearnedIds once the cache is built.
        /// </summary>


        /// <summary>
        /// Spell names queued from LEARNED_SPELL events that arrived before the name→ID cache
        /// was ready. Flushed into _persistentLearnedIds once the cache is built.
        /// </summary>
        private readonly List<string> _pendingLearnedSpellNames = new();

        private readonly List<string> _pendingUnlearnedSpellNames = new();

        /// <summary>
        /// Maps spell name → list of spell IDs from the client spell DB (0x00C0D788 pointer chain).
        /// Built once (non-null DB pointer required). Used to translate LEARNED_SPELL arg1 names
        /// into IDs. Different ranks share a name (e.g. Deflection ranks 1-5 = IDs 16462-16466).
        /// </summary>


        /// <summary>
        /// Maps spell name → list of spell IDs from the client spell DB (0x00C0D788 pointer chain).
        /// Built once (non-null DB pointer required). Used to translate LEARNED_SPELL arg1 names
        /// into IDs. Different ranks share a name (e.g. Deflection ranks 1-5 = IDs 16462-16466).
        /// </summary>


        private bool _spellNameCacheBuilt = false;

        /// <summary>
        /// Scans the client spell DB (0x00C0D788 pointer chain) once to build a
        /// name → [id1, id2, ...] lookup. Different ranks of the same spell share
        /// a name but have distinct IDs (e.g. Deflection rank 1-5 = 16462-16466).
        /// </summary>


        /// <summary>
        /// Scans the client spell DB (0x00C0D788 pointer chain) once to build a
        /// name → [id1, id2, ...] lookup. Different ranks of the same spell share
        /// a name but have distinct IDs (e.g. Deflection rank 1-5 = 16462-16466).
        /// </summary>


        /// <summary>
        /// Scans the client spell DB (0x00C0D788 pointer chain) once to build a
        /// name → [id1, id2, ...] lookup. Different ranks of the same spell share
        /// a name but have distinct IDs (e.g. Deflection rank 1-5 = 16462-16466).
        /// </summary>


        /// <summary>
        /// Scans the client spell DB (0x00C0D788 pointer chain) once to build a
        /// name → [id1, id2, ...] lookup. Different ranks of the same spell share
        /// a name but have distinct IDs (e.g. Deflection rank 1-5 = 16462-16466).
        /// </summary>
        private void EnsureSpellNameCache()
        {
            if (_spellNameCacheBuilt) return;

            var spellsBasePtr = MemoryManager.ReadIntPtr(0x00C0D788);
            DiagLog($"[SPELLDB] 0x00C0D788 ptr={spellsBasePtr:X}");
            if (spellsBasePtr == nint.Zero)
            {
                DiagLog("[SPELLDB] Spell DB pointer is null — will retry next call");
                return;  // Don't set _spellNameCacheBuilt — retry on next RefreshSpells tick
            }

            // Test first few entries to validate DB structure before full scan
            int validEntries = 0;
            for (int id = 1; id <= 20; id++)
            {
                try
                {
                    var testPtr = MemoryManager.ReadIntPtr(spellsBasePtr + id * 4);
                    if (testPtr != nint.Zero) validEntries++;
                }
                catch { }
            }
            DiagLog($"[SPELLDB] First 20 IDs: {validEntries} non-null pointers");

            var count = 0;
            for (int id = 1; id < 25000; id++)
            {
                try
                {
                    var spellPtr = MemoryManager.ReadIntPtr(spellsBasePtr + id * 4);
                    if (spellPtr == nint.Zero) continue;
                    var spellNamePtr = MemoryManager.ReadIntPtr(spellPtr + 0x1E0);
                    if (spellNamePtr == nint.Zero) continue;
                    var name = MemoryManager.ReadString(spellNamePtr);
                    if (string.IsNullOrEmpty(name) || name.Length > 100) continue;
                    if (!_spellNameToIds.TryGetValue(name, out var idList))
                    {
                        idList = new List<uint>();
                        _spellNameToIds[name] = idList;
                    }
                    idList.Add((uint)id);
                    count++;
                }
                catch { /* skip bad spell DB entries */ }
            }

            // Only mark built if we found at least some entries; retry if DB not yet loaded
            if (count > 0)
            {
                _spellNameCacheBuilt = true;
                DiagLog($"[SPELLDB] Cache built: {count} IDs across {_spellNameToIds.Count} unique names");
                Log.Information("[SPELLDB] Spell name→ID cache built: {Count} IDs across {Names} unique names",
                    count, _spellNameToIds.Count);
            }
            else
            {
                DiagLog("[SPELLDB] Cache scan found 0 entries — will retry next call");
                _spellNameToIds.Clear();
            }
        }



        /// <summary>
        /// Reads the spell name directly from the client spell DB (0x00C0D788 pointer chain).
        /// Returns null if the DB isn't loaded or the spell ID doesn't exist.
        /// </summary>
        internal string? GetSpellNameFromDb(int spellId)
        {
            try
            {
                var spellsBasePtr = MemoryManager.ReadIntPtr(0x00C0D788);
                if (spellsBasePtr == nint.Zero) return null;
                var spellPtr = MemoryManager.ReadIntPtr(spellsBasePtr + spellId * 4);
                if (spellPtr == nint.Zero) return null;
                var spellNamePtr = MemoryManager.ReadIntPtr(spellPtr + 0x1E0);
                if (spellNamePtr == nint.Zero) return null;
                var name = MemoryManager.ReadString(spellNamePtr);
                return string.IsNullOrEmpty(name) || name.Length > 100 ? null : name;
            }
            catch
            {
                return null;
            }
        }

        internal IReadOnlyList<uint> GetSpellIdsByName(string spellName)
        {
            if (string.IsNullOrWhiteSpace(spellName))
                return Array.Empty<uint>();

            EnsureSpellNameCache();
            return _spellNameToIds.TryGetValue(spellName.Trim(), out var ids)
                ? ids
                : Array.Empty<uint>();
        }



        public void RefreshSkills()
        {
            if (Player is not LocalPlayer localPlayer) return;

            localPlayer.PlayerSkills.Clear();
            var skillPtr1 = MemoryManager.ReadIntPtr(nint.Add(localPlayer.Pointer, 8));
            if (skillPtr1 == nint.Zero) return;

            var skillPtr2 = nint.Add(skillPtr1, 0xB38);

            var maxSkills = MemoryManager.ReadInt(0x00B700B4);
            // Sanity check to prevent infinite loops
            if (maxSkills < 0 || maxSkills > 1000) return;

            for (var i = 0; i < maxSkills + 12; i++)
            {
                var curPointer = nint.Add(skillPtr2, i * 12);

                var id = (Skills)MemoryManager.ReadShort(curPointer);
                if (!Enum.IsDefined(typeof(Skills), id))
                {
                    continue;
                }

                localPlayer.PlayerSkills.Add((short)id);
            }
        }
        // EnumerateVisibleObjects callback for Vanilla 1.12.1: ThisCall with (filter, guid)
        // Parameter order is swapped compared to non-Vanilla clients

        // EnumerateVisibleObjects callback for Vanilla 1.12.1: ThisCall with (filter, guid)
        // Parameter order is swapped compared to non-Vanilla clients



        public IReadOnlyCollection<uint> KnownSpellIds
        {
            get
            {
                // _lastKnownSpellIds is updated by both RefreshSpells() and the LEARNED_SPELL
                // event handler. Using it as primary source avoids two failure modes:
                //   1. LocalPlayer recreated by SMSG_UPDATE_OBJECT → RawSpellBookIds = [] for 2s
                //   2. Bot-loop thread hung → RefreshSpells() not called → new spells never published
                // The LEARNED_SPELL handler runs on WoW's main thread and updates _lastKnownSpellIds
                // directly, so newly-learned spells are visible even if RefreshSpells() hasn't run.
                var last = _lastKnownSpellIds;
                if (last.Count > 0) return last;

                // Fallback: LocalPlayer.RawSpellBookIds covers the startup window before
                // RefreshSpells() has had a chance to run (so _lastKnownSpellIds is still empty).
                if (Player is LocalPlayer lp && lp.RawSpellBookIds.Count > 0)
                    return lp.RawSpellBookIds;

                return Array.Empty<uint>();
            }
        }
    }
}
