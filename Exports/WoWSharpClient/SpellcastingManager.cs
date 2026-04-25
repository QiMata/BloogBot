using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;
using WoWSharpClient.Utils;

namespace WoWSharpClient
{
    /// <summary>
    /// Handles spell casting, melee/ranged attacks, target selection, and cooldown tracking.
    /// Extracted from WoWSharpObjectManager.Combat.cs to reduce partial-class sprawl.
    /// All public IObjectManager methods are still on WoWSharpObjectManager; this class
    /// provides the implementation and the partial class delegates to it.
    /// </summary>
    internal sealed class SpellcastingManager
    {
        private readonly WoWSharpObjectManager _om;

        // Fishing spell IDs — vanilla fishing casts do not carry an explicit unit or destination payload.
        private static readonly HashSet<int> _fishingSpellIds = [7620, 7731, 7732, 18248, 33095];
        private const int ShootSpellId = 5019;

        // Melee rejection tracking
        private const long RecentMeleeRejectWindowTicks = TimeSpan.TicksPerMillisecond * 1200;
        private const long PendingMeleeAttackConfirmWindowTicks = TimeSpan.TicksPerMillisecond * 1200;
        private long _recentMeleeRangeRejectUntilTicks;
        private ulong _recentMeleeRangeRejectTargetGuid;
        private long _recentMeleeFacingRejectUntilTicks;
        private ulong _recentMeleeFacingRejectTargetGuid;
        private long _pendingMeleeAttackConfirmUntilTicks;
        private ulong _pendingMeleeAttackTargetGuid;
        private ulong _confirmedMeleeAttackTargetGuid;

        // Optional cooldown checker — set by BackgroundBotWorker after SpellCastingNetworkClientComponent is created
        private Func<uint, bool> _spellCooldownChecker;

        internal ulong CurrentTargetGuid;

        public SpellcastingManager(WoWSharpObjectManager objectManager)
        {
            _om = objectManager;
        }

        // ---- Accessors for internal wiring ----

        private WoWClient WoWClient => _om.WoWClientInternal;
        private IWoWLocalPlayer Player => _om.Player;
        private HighGuid PlayerGuid => _om.PlayerGuid;
        private List<Spell> Spells => _om.Spells;
        private object SpellLock => _om.SpellLock;

        // ---- Cooldown checker ----

        public void SetSpellCooldownChecker(Func<uint, bool> checker) => _spellCooldownChecker = checker;

        // ---- Melee rejection tracking ----

        internal void NoteMeleeRangeRejected()
        {
            var targetGuid = CurrentTargetGuid != 0 ? CurrentTargetGuid : Player?.TargetGuid ?? 0;
            if (targetGuid == 0) return;

            ClearTrackedMeleeAttackState(targetGuid);
            _recentMeleeRangeRejectTargetGuid = targetGuid;
            Interlocked.Exchange(ref _recentMeleeRangeRejectUntilTicks, DateTime.UtcNow.Ticks + RecentMeleeRejectWindowTicks);
            Log.Warning("[COMBAT] Marked recent melee range rejection for 0x{Target:X}", targetGuid);
        }

        internal void NoteMeleeFacingRejected()
        {
            var targetGuid = CurrentTargetGuid != 0 ? CurrentTargetGuid : Player?.TargetGuid ?? 0;
            if (targetGuid == 0) return;

            ClearTrackedMeleeAttackState(targetGuid);
            _recentMeleeFacingRejectTargetGuid = targetGuid;
            Interlocked.Exchange(ref _recentMeleeFacingRejectUntilTicks, DateTime.UtcNow.Ticks + RecentMeleeRejectWindowTicks);
            Log.Warning("[COMBAT] Marked recent melee facing rejection for 0x{Target:X}", targetGuid);
        }

        internal void ClearRecentMeleeRejections(ulong targetGuid = 0)
        {
            if (targetGuid == 0 || _recentMeleeRangeRejectTargetGuid == targetGuid)
            {
                _recentMeleeRangeRejectTargetGuid = 0;
                Interlocked.Exchange(ref _recentMeleeRangeRejectUntilTicks, 0);
            }

            if (targetGuid == 0 || _recentMeleeFacingRejectTargetGuid == targetGuid)
            {
                _recentMeleeFacingRejectTargetGuid = 0;
                Interlocked.Exchange(ref _recentMeleeFacingRejectUntilTicks, 0);
            }
        }

        public bool HadRecentMeleeRangeRejection(ulong targetGuid)
            => targetGuid != 0
                && _recentMeleeRangeRejectTargetGuid == targetGuid
                && DateTime.UtcNow.Ticks < Interlocked.Read(ref _recentMeleeRangeRejectUntilTicks);

        public bool HadRecentMeleeFacingRejection(ulong targetGuid)
            => targetGuid != 0
                && _recentMeleeFacingRejectTargetGuid == targetGuid
                && DateTime.UtcNow.Ticks < Interlocked.Read(ref _recentMeleeFacingRejectUntilTicks);

        internal void NotePendingMeleeAttackStart(ulong targetGuid)
        {
            if (targetGuid == 0) return;
            _pendingMeleeAttackTargetGuid = targetGuid;
            Interlocked.Exchange(ref _pendingMeleeAttackConfirmUntilTicks, DateTime.UtcNow.Ticks + PendingMeleeAttackConfirmWindowTicks);
        }

        internal bool HasConfirmedMeleeAttackStart(ulong targetGuid)
            => targetGuid != 0 && _confirmedMeleeAttackTargetGuid == targetGuid;

        internal bool HasPendingMeleeAttackStart(ulong targetGuid)
        {
            if (targetGuid == 0 || _pendingMeleeAttackTargetGuid != targetGuid)
                return false;

            if (DateTime.UtcNow.Ticks < Interlocked.Read(ref _pendingMeleeAttackConfirmUntilTicks))
                return true;

            ClearPendingMeleeAttackStart(targetGuid);
            return false;
        }

        internal void ClearPendingMeleeAttackStart(ulong targetGuid = 0)
        {
            if (targetGuid != 0 && _pendingMeleeAttackTargetGuid != targetGuid)
                return;

            _pendingMeleeAttackTargetGuid = 0;
            Interlocked.Exchange(ref _pendingMeleeAttackConfirmUntilTicks, 0);
        }

        internal void ClearConfirmedMeleeAttackStart(ulong targetGuid = 0)
        {
            if (targetGuid != 0 && _confirmedMeleeAttackTargetGuid != targetGuid)
                return;

            _confirmedMeleeAttackTargetGuid = 0;
        }

        internal void ClearTrackedMeleeAttackState(ulong targetGuid = 0)
        {
            ClearPendingMeleeAttackStart(targetGuid);
            ClearConfirmedMeleeAttackStart(targetGuid);
        }

        internal void ConfirmMeleeAttackStarted(ulong targetGuid = 0)
        {
            var confirmedTargetGuid = targetGuid != 0
                ? targetGuid
                : (CurrentTargetGuid != 0 ? CurrentTargetGuid : Player?.TargetGuid ?? 0);
            if (confirmedTargetGuid == 0) return;

            ClearPendingMeleeAttackStart(confirmedTargetGuid);
            ClearRecentMeleeRejections(confirmedTargetGuid);
            _confirmedMeleeAttackTargetGuid = confirmedTargetGuid;
        }

        // ---- Spell readiness ----

        public bool IsSpellReady(string spellName)
        {
            var knownIds = Spells.Select(s => s.Id);
            var spellId = GameData.Core.Constants.SpellData.GetHighestKnownRank(spellName, knownIds);
            if (spellId == 0) return false;
            if (_spellCooldownChecker != null)
                return _spellCooldownChecker(spellId);
            return true;
        }

        // ---- Casting ----

        public void StopCasting()
        {
            if (WoWClient == null) return;
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_CANCEL_CAST, []);
        }

        public void CastSpell(string spellName, int rank = -1, bool castOnSelf = false)
        {
            var knownIds = Spells.Select(s => s.Id);
            var spellId = GameData.Core.Constants.SpellData.GetHighestKnownRank(spellName, knownIds);

            if (spellId == 0)
            {
                Log.Warning("[CastSpell] Spell '{SpellName}' not found in known spells or SpellData lookup", spellName);
                return;
            }

            CastSpell((int)spellId, rank, castOnSelf);
        }

        public void CastSpell(int spellId, int rank = -1, bool castOnSelf = false)
        {
            if (WoWClient == null) return;

            var isFishingSpell = _fishingSpellIds.Contains(spellId);
            var forceNoTarget = isFishingSpell;
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((uint)spellId);

            if (castOnSelf || forceNoTarget || CurrentTargetGuid == 0 || CurrentTargetGuid == PlayerGuid.FullGuid)
            {
                w.Write((ushort)0x0000);
                Log.Information("[CastSpell] spell={SpellId} targetFlags=0x0000 currentTarget=0x{Guid:X} fishing={IsFishing}",
                    spellId, CurrentTargetGuid, forceNoTarget);
            }
            else
            {
                w.Write((ushort)0x0002);
                ReaderUtils.WritePackedGuid(w, CurrentTargetGuid);
                Log.Information("[CastSpell] spell={SpellId} targetUnit=0x{Guid:X} packetHex={Hex}",
                    spellId, CurrentTargetGuid, BitConverter.ToString(ms.ToArray()));
            }

            var payload = ms.ToArray();
            Log.Information("[CastSpell] Sending CMSG_CAST_SPELL ({Len} bytes): {Hex}",
                payload.Length, BitConverter.ToString(payload));
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_CAST_SPELL, payload)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Log.Error(t.Exception, "[CastSpell] SEND FAILED for spell {SpellId}", spellId);
                    else
                        Log.Information("[CastSpell] SEND OK for spell {SpellId}", spellId);
                }, TaskScheduler.Default);
        }

        public void CastSpellAtLocation(int spellId, float x, float y, float z)
        {
            if (WoWClient == null) return;

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((uint)spellId);
            w.Write((ushort)0x0040); // TARGET_FLAG_DEST_LOCATION
            w.Write(x);
            w.Write(y);
            w.Write(z);

            var payload = ms.ToArray();
            Log.Information("[CastSpellAtLocation] spell={SpellId} loc=({X:F1},{Y:F1},{Z:F1}) ({Len} bytes): {Hex}",
                spellId, x, y, z, payload.Length, BitConverter.ToString(payload));
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_CAST_SPELL, payload)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Log.Error(t.Exception, "[CastSpellAtLocation] SEND FAILED for spell {SpellId}", spellId);
                    else
                        Log.Information("[CastSpellAtLocation] SEND OK for spell {SpellId}", spellId);
                }, TaskScheduler.Default);
        }

        public void CastSpellOnGameObject(int spellId, ulong gameObjectGuid)
        {
            if (WoWClient == null) return;

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((uint)spellId);
            w.Write((ushort)0x0800); // TARGET_FLAG_OBJECT
            ReaderUtils.WritePackedGuid(w, gameObjectGuid);

            var payload = ms.ToArray();
            Log.Information("[CastSpellOnGameObject] spell={SpellId} target=0x{Guid:X} ({Len} bytes): {Hex}",
                spellId, gameObjectGuid, payload.Length, BitConverter.ToString(payload));
            WoWClient.SendMSGPackedAsync(Opcode.CMSG_CAST_SPELL, payload).GetAwaiter().GetResult();
        }

        public bool CanCastSpell(int spellId, ulong targetGuid)
        {
            lock (SpellLock)
                return Spells.Any(s => s.Id == (uint)spellId);
        }

        public IReadOnlyCollection<uint> KnownSpellIds
        {
            get { lock (SpellLock) return Spells.Select(s => s.Id).ToArray(); }
        }

        public void CancelAura(uint spellId)
        {
            if (WoWClient == null) return;
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_CANCEL_AURA, BitConverter.GetBytes(spellId));
        }

        // ---- Wand ----

        public void StartWandAttack() => CastSpell(ShootSpellId);

        public void StopWandAttack() => StopCasting();

        // ---- Target ----

        public void SetTarget(ulong guid)
        {
            if (WoWClient == null) return;
            if (guid != CurrentTargetGuid)
            {
                ClearRecentMeleeRejections();
                ClearTrackedMeleeAttackState();
            }
            CurrentTargetGuid = guid;
            if (Player is WoWLocalPlayer localPlayer)
            {
                localPlayer.TargetGuid = guid;
                localPlayer.TargetHighGuid.LowGuidValue = BitConverter.GetBytes((uint)(guid & 0xFFFFFFFF));
                localPlayer.TargetHighGuid.HighGuidValue = BitConverter.GetBytes((uint)(guid >> 32));
            }
            else if (Player is WoWUnit unit)
            {
                unit.TargetGuid = guid;
            }
            var payload = BitConverter.GetBytes(guid);
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_SET_SELECTION, payload);
        }

        // ---- Attack ----

        public void StopAttack()
        {
            if (WoWClient == null) return;
            if (Player is WoWLocalPlayer lp)
                lp.IsAutoAttacking = false;
            ClearRecentMeleeRejections();
            ClearTrackedMeleeAttackState();
            _ = WoWClient.SendMSGPackedAsync(Opcode.CMSG_ATTACKSTOP, []);
        }

        public void StartMeleeAttack()
        {
            if (WoWClient == null) return;

            if (CurrentTargetGuid != 0)
            {
                if (Player is WoWLocalPlayer localPlayer)
                {
                    var previousTargetGuid = localPlayer.TargetGuid;
                    bool switchingTargets = previousTargetGuid != 0 && previousTargetGuid != CurrentTargetGuid;

                    localPlayer.TargetGuid = CurrentTargetGuid;
                    localPlayer.TargetHighGuid.LowGuidValue = BitConverter.GetBytes((uint)(CurrentTargetGuid & 0xFFFFFFFF));
                    localPlayer.TargetHighGuid.HighGuidValue = BitConverter.GetBytes((uint)(CurrentTargetGuid >> 32));

                    if (localPlayer.IsAutoAttacking && !switchingTargets)
                    {
                        if (HasConfirmedMeleeAttackStart(CurrentTargetGuid))
                            return;

                        if (HasPendingMeleeAttackStart(CurrentTargetGuid))
                            return;

                        Log.Warning("[StartMeleeAttack] Retrying CMSG_ATTACKSWING on 0x{Target:X} after missing server confirmation",
                            CurrentTargetGuid);
                    }

                    Log.Information("[StartMeleeAttack] Sending CMSG_ATTACKSWING on 0x{Target:X} (re-engage={ReEngage}, switchingTargets={SwitchingTargets})",
                        CurrentTargetGuid, previousTargetGuid != 0, switchingTargets);

                    var gameTimeMs = (uint)_om.WorldTimeNowMs;
                    var heartbeat = MovementPacketHandler.BuildMovementInfoBuffer(localPlayer, gameTimeMs, 0);
                    WoWClient.SendMovementOpcodeAsync(Opcode.MSG_MOVE_HEARTBEAT, heartbeat).GetAwaiter().GetResult();

                    localPlayer.IsAutoAttacking = true;
                    NotePendingMeleeAttackStart(CurrentTargetGuid);
                }
                else if (Player is WoWUnit unit)
                {
                    unit.TargetGuid = CurrentTargetGuid;
                }
            }

            var payload = BitConverter.GetBytes(CurrentTargetGuid);
            WoWClient.SendMSGPackedAsync(Opcode.CMSG_ATTACKSWING, payload).GetAwaiter().GetResult();
        }

        public void StartRangedAttack() => StartMeleeAttack();

        // ---- Talent ----

        public sbyte GetTalentRank(uint tabIndex, uint talentIndex)
        {
            var factory = _om.AgentFactoryAccessor?.Invoke();
            var tree = factory?.TalentAgent?.GetTalentTreeInfo(tabIndex);
            if (tree?.Talents == null) return -1;
            var talent = tree.Talents.FirstOrDefault(t => t.TalentIndex == talentIndex);
            if (talent == null) return -1;
            return (sbyte)talent.CurrentRank;
        }

        public uint GetManaCost(string spellName) => 0;
    }
}
