using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Parsers;
using WoWSharpClient.Screens;
using WoWSharpClient.Utils;
using static GameData.Core.Enums.UpdateFields;
using Enum = System.Enum;
using Timer = System.Timers.Timer;

namespace WoWSharpClient
{
    public partial class WoWSharpObjectManager
    {
        private ulong _currentTargetGuid;

        // Temporary diagnostic: log all opcodes received after GAMEOBJ_USE


        // Optional cooldown checker — set by BackgroundBotWorker after SpellCastingNetworkClientComponent is created
        private Func<uint, bool> _spellCooldownChecker;

        /// <summary>
        /// Set a delegate that checks if a spell ID is off cooldown (returns true if ready).
        /// Wire this to SpellCastingNetworkClientComponent.CanCastSpell().
        /// </summary>


        /// <summary>
        /// Set a delegate that checks if a spell ID is off cooldown (returns true if ready).
        /// Wire this to SpellCastingNetworkClientComponent.CanCastSpell().
        /// </summary>
        public void SetSpellCooldownChecker(Func<uint, bool> checker) => _spellCooldownChecker = checker;

        // Optional agent factory accessor — set by BackgroundBotWorker for LootTargetAsync


        public bool IsSpellReady(string spellName)
        {
            // Resolve spell name to highest-rank ID the player knows
            var knownIds = Spells.Select(s => s.Id);
            var spellId = GameData.Core.Constants.SpellData.GetHighestKnownRank(spellName, knownIds);

            // Spell not known
            if (spellId == 0) return false;

            // Check cooldown via delegate if wired, otherwise assume ready (server validates)
            if (_spellCooldownChecker != null)
                return _spellCooldownChecker(spellId);

            return true;
        }


        public void StopCasting()
        {
            if (_woWClient == null) return;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_CANCEL_CAST, []);
        }


        public void CastSpell(string spellName, int rank = -1, bool castOnSelf = false)
        {
            // Resolve spell name to highest-rank ID the player knows
            var knownIds = Spells.Select(s => s.Id);
            var spellId = GameData.Core.Constants.SpellData.GetHighestKnownRank(spellName, knownIds);

            if (spellId == 0)
            {
                Log.Warning("[CastSpell] Spell '{SpellName}' not found in known spells or SpellData lookup", spellName);
                return;
            }

            CastSpell((int)spellId, rank, castOnSelf);
        }


        // Fishing spell IDs that require TARGET_FLAG_DEST_LOCATION instead of TARGET_FLAG_SELF.
        // Fishing casts a bobber at a location in front of the player — self-targeting causes NOT_FISHABLE.
        private static readonly HashSet<int> _fishingSpellIds = [7620, 7731, 7732, 18248, 33095];
        private const float FishingBobberDistance = 18f; // yards in front of player

        public void CastSpell(int spellId, int rank = -1, bool castOnSelf = false)
        {
            if (_woWClient == null) return;

            // Fishing spells need location-based targeting — calculate bobber position from facing
            if (!castOnSelf && _fishingSpellIds.Contains(spellId) && Player?.Position != null)
            {
                var facing = Player.Facing;
                var pos = Player.Position;
                float targetX = pos.X + (float)(FishingBobberDistance * Math.Cos(facing));
                float targetY = pos.Y + (float)(FishingBobberDistance * Math.Sin(facing));
                float targetZ = pos.Z; // bobber lands at water surface; server adjusts Z
                Log.Information("[CastSpell] Fishing spell {SpellId} — using location target ({X:F1}, {Y:F1}, {Z:F1}) from facing {Facing:F2}",
                    spellId, targetX, targetY, targetZ, facing);
                CastSpellAtLocation(spellId, targetX, targetY, targetZ);
                return;
            }

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((uint)spellId);

            if (castOnSelf || _currentTargetGuid == 0 || _currentTargetGuid == PlayerGuid.FullGuid)
            {
                // TARGET_FLAG_SELF = 0x0000 - server uses caster as target
                // Also use self-targeting when the current target is the player's own GUID
                w.Write((ushort)0x0000);
                Log.Information("[CastSpell] spell={SpellId} targetSelf (guid=0x{Guid:X})", spellId, _currentTargetGuid);
            }
            else
            {
                // TARGET_FLAG_UNIT = 0x0002 - target a specific unit
                w.Write((ushort)0x0002);
                ReaderUtils.WritePackedGuid(w, _currentTargetGuid);
                Log.Information("[CastSpell] spell={SpellId} targetUnit=0x{Guid:X} packetHex={Hex}",
                    spellId, _currentTargetGuid, BitConverter.ToString(ms.ToArray()));
            }

            var payload = ms.ToArray();
            Log.Information("[CastSpell] Sending CMSG_CAST_SPELL ({Len} bytes): {Hex}",
                payload.Length, BitConverter.ToString(payload));
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_CAST_SPELL, payload)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Log.Error(t.Exception, "[CastSpell] SEND FAILED for spell {SpellId}", spellId);
                    else
                        Log.Information("[CastSpell] SEND OK for spell {SpellId}", spellId);
                });
        }

        public void CastSpellAtLocation(int spellId, float x, float y, float z)
        {
            if (_woWClient == null) return;

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
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_CAST_SPELL, payload)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Log.Error(t.Exception, "[CastSpellAtLocation] SEND FAILED for spell {SpellId}", spellId);
                    else
                        Log.Information("[CastSpellAtLocation] SEND OK for spell {SpellId}", spellId);
                });
        }


        public void CastSpellOnGameObject(int spellId, ulong gameObjectGuid)
        {
            if (_woWClient == null) return;

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write((uint)spellId);
            // TARGET_FLAG_OBJECT = 0x0800 — MaNGOS reads packed GO GUID for this flag
            w.Write((ushort)0x0800);
            ReaderUtils.WritePackedGuid(w, gameObjectGuid);

            var payload = ms.ToArray();
            Log.Information("[CastSpellOnGameObject] spell={SpellId} target=0x{Guid:X} ({Len} bytes): {Hex}",
                spellId, gameObjectGuid, payload.Length, BitConverter.ToString(payload));
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_CAST_SPELL, payload);
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


        public void StartWandAttack()
        {
            // BG bot: cast "Shoot" spell (wand auto-attack)
            CastSpell("Shoot");
        }


        public void StopWandAttack()
        {
            // BG bot: stop casting to cancel wand auto-attack
            StopCasting();
        }


        public sbyte GetTalentRank(uint tabIndex, uint talentIndex)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            var tree = factory?.TalentAgent?.GetTalentTreeInfo(tabIndex);
            if (tree?.Talents == null) return -1;
            var talent = tree.Talents.FirstOrDefault(t => t.TalentIndex == talentIndex);
            if (talent == null) return -1;
            return (sbyte)talent.CurrentRank;
        }


        public uint GetManaCost(string spellName)
        {
            // Spell cost data not available from server packets in vanilla 1.12.1
            // Return 0 to indicate "can always attempt" — the server will reject if insufficient mana
            return 0;
        }


        public void CancelAura(uint spellId)
        {
            if (_woWClient == null) return;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_CANCEL_AURA, BitConverter.GetBytes(spellId));
        }


        public void SetTarget(ulong guid)
        {
            if (_woWClient == null) return;
            _currentTargetGuid = guid;
            if (Player is Models.WoWLocalPlayer localPlayer)
            {
                localPlayer.TargetGuid = guid;
                // Also update TargetHighGuid so incoming SMSG_UPDATE_OBJECT (UNIT_FIELD_TARGET)
                // doesn't clobber the locally-set TargetGuid back to the old value.
                localPlayer.TargetHighGuid.LowGuidValue = BitConverter.GetBytes((uint)(guid & 0xFFFFFFFF));
                localPlayer.TargetHighGuid.HighGuidValue = BitConverter.GetBytes((uint)(guid >> 32));
            }
            else if (Player is Models.WoWUnit unit)
            {
                // Fallback: set TargetGuid on the model directly so snapshots reflect
                // the target immediately (before the server echoes via SMSG_UPDATE_OBJECT).
                unit.TargetGuid = guid;
            }
            var payload = BitConverter.GetBytes(guid);
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_SET_SELECTION, payload);
        }


        public void StopAttack()
        {
            if (_woWClient == null) return;
            if (Player is Models.WoWLocalPlayer lp)
                lp.IsAutoAttacking = false;
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_ATTACKSTOP, []);
        }


        public void StartMeleeAttack()
        {
            if (_woWClient == null) return;

            // Ensure the player model's TargetGuid is consistent with _currentTargetGuid
            // so snapshots immediately reflect the attack target.  SetTarget() normally
            // does this, but callers (e.g. BotProfile rotation tasks) may invoke
            // StartMeleeAttack() after SetTarget was already called, and an intervening
            // SMSG_UPDATE_OBJECT could have clobbered TargetGuid back to 0.
            if (_currentTargetGuid != 0)
            {
                if (Player is Models.WoWLocalPlayer localPlayer)
                {
                    localPlayer.TargetGuid = _currentTargetGuid;
                    localPlayer.TargetHighGuid.LowGuidValue = BitConverter.GetBytes((uint)(_currentTargetGuid & 0xFFFFFFFF));
                    localPlayer.TargetHighGuid.HighGuidValue = BitConverter.GetBytes((uint)(_currentTargetGuid >> 32));

                    // MaNGOS requires a recent movement packet to process CMSG_ATTACKSWING.
                    // After teleport + settle, the bot is stationary with MOVEFLAG_NONE and
                    // IsAutoAttacking=false, so MovementController sends no heartbeats.
                    // Force a MSG_MOVE_HEARTBEAT here so the server has fresh movement data,
                    // then set IsAutoAttacking so the controller keeps sending periodic heartbeats.
                    var gameTimeMs = (uint)_worldTimeTracker.NowMS.TotalMilliseconds;
                    var heartbeat = Parsers.MovementPacketHandler.BuildMovementInfoBuffer(localPlayer, gameTimeMs, 0);
                    _ = _woWClient.SendMovementOpcodeAsync(Opcode.MSG_MOVE_HEARTBEAT, heartbeat);
                    localPlayer.IsAutoAttacking = true;
                    Log.Information("[StartMeleeAttack] Sent pre-attack heartbeat at ({X:F1},{Y:F1},{Z:F1}), IsAutoAttacking=true",
                        localPlayer.Position.X, localPlayer.Position.Y, localPlayer.Position.Z);
                }
                else if (Player is Models.WoWUnit unit)
                {
                    unit.TargetGuid = _currentTargetGuid;
                }
            }

            // CMSG_ATTACKSWING requires the target's full 8-byte GUID
            var payload = BitConverter.GetBytes(_currentTargetGuid);
            _ = _woWClient.SendMSGPackedAsync(Opcode.CMSG_ATTACKSWING, payload);
        }


        public void StartRangedAttack()
        {
            // Ranged attack uses the same CMSG_ATTACKSWING opcode as melee
            StartMeleeAttack();
        }


        public enum ObjectUpdateOperation
        {
            Add,
            Update,
            Remove,
        }
    }
}
