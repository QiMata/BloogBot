using Communication;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Serilog;
using System;
using System.Linq;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        private void PopulateSnapshotFromObjectManager()
        {
            _activitySnapshot.Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Clear any action from the previous response to prevent echo-back.
            // Without this, the old CurrentAction stays in the snapshot, gets sent
            // back to StateManager, and is returned again — causing infinite re-execution.
            _activitySnapshot.CurrentAction = null;

            // Detect screen state
            if (_objectManager.HasEnteredWorld && _objectManager.Player != null)
            {
                _activitySnapshot.ScreenState = "InWorld";
                _activitySnapshot.CharacterName = _objectManager.Player.Name ?? string.Empty;
            }
            else if (_objectManager.CharacterSelectScreen?.IsOpen == true)
            {
                _activitySnapshot.ScreenState = "CharacterSelect";
            }
            else if (_objectManager.LoginScreen?.IsLoggedIn == true)
            {
                _activitySnapshot.ScreenState = "RealmSelect";
            }
            else
            {
                _activitySnapshot.ScreenState = "LoginScreen";
            }

            // Always flush message buffers (even during login — captures GM command errors)
            FlushMessageBuffers();

            // Only populate game data when in world
            if (_activitySnapshot.ScreenState != "InWorld" || _objectManager.Player == null)
                return;

            var player = _objectManager.Player;

            // Track the last known alive position to recover corpse navigation when corpse coordinates
            // are not populated immediately after release on some client/server combinations.
            UpdateLastKnownAlivePosition(player);

            // Movement data
            try
            {
                var pos = player.Position;
                _activitySnapshot.MovementData = new Game.MovementData
                {
                    MovementFlags = (uint)player.MovementFlags,
                    FallTime = player.FallTime,
                    JumpVerticalSpeed = player.JumpVerticalSpeed,
                    JumpSinAngle = player.JumpSinAngle,
                    JumpCosAngle = player.JumpCosAngle,
                    JumpHorizontalSpeed = player.JumpHorizontalSpeed,
                    SwimPitch = player.SwimPitch,
                    WalkSpeed = player.WalkSpeed,
                    RunSpeed = player.RunSpeed,
                    RunBackSpeed = player.RunBackSpeed,
                    SwimSpeed = player.SwimSpeed,
                    SwimBackSpeed = player.SwimBackSpeed,
                    TurnRate = player.TurnRate,
                    Facing = player.Facing,
                    TransportGuid = player.TransportGuid,
                    TransportOrientation = player.TransportOrientation,
                };
                if (pos != null)
                {
                    _activitySnapshot.MovementData.Position = new Game.Position
                    {
                        X = pos.X,
                        Y = pos.Y,
                        Z = pos.Z,
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating movement data: {ex.Message}");
            }

            // Party leader GUID (0 if not in a group)
            // Use agent factory (headless) if available, otherwise fall back to IObjectManager (foreground)
            try
            {
                var factory = _agentFactoryAccessor?.Invoke();
                if (factory != null)
                {
                    var members = factory.PartyAgent.GetGroupMembers();
                    var leader = members.FirstOrDefault(m => m.IsLeader);
                    _activitySnapshot.PartyLeaderGuid = leader?.Guid ?? (factory.PartyAgent.IsGroupLeader && factory.PartyAgent.GroupSize > 0 ? player.Guid : 0);
                }
                else
                {
                    _activitySnapshot.PartyLeaderGuid = _objectManager.PartyLeaderGuid;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating party leader GUID: {ex.Message}");
            }

            // Player protobuf
            try
            {
                _activitySnapshot.Player = BuildPlayerProtobuf(player);
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating player: {ex.Message}");
            }

            // Spell list (known spell IDs for combat coordination)
            try
            {
                if (_activitySnapshot.Player != null)
                {
                    _activitySnapshot.Player.SpellList.Clear();
                    foreach (var spellId in _objectManager.KnownSpellIds)
                        _activitySnapshot.Player.SpellList.Add(spellId);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating spell list: {ex.Message}");
            }

            // Equipment slots (inventory map: slot 0-18 → 64-bit GUID)
            // WoWPlayer.Inventory stores GUID pairs: [slot*2]=LOW, [slot*2+1]=HIGH
            try
            {
                if (_activitySnapshot.Player != null && player is GameData.Core.Interfaces.IWoWPlayer wp)
                {
                    _activitySnapshot.Player.Inventory.Clear();
                    int nonZeroCount = 0;
                    for (uint slot = 0; slot < 19; slot++)
                    {
                        uint lowIdx = slot * 2;
                        uint highIdx = slot * 2 + 1;
                        if (highIdx < (uint)wp.Inventory.Length)
                        {
                            ulong guid = ((ulong)wp.Inventory[highIdx] << 32) | wp.Inventory[lowIdx];
                            if (guid != 0)
                            {
                                _activitySnapshot.Player.Inventory[slot] = guid;
                                nonZeroCount++;
                            }
                        }
                    }
                    if (nonZeroCount > 0)
                    {
                        Log.Information("[BOT RUNNER] Equipment: {Count} slots occupied (Inventory[].Length={Len})", nonZeroCount, wp.Inventory.Length);
                        // DIAG: log exact slots and verify protobuf map contains them
                        foreach (var kvp in _activitySnapshot.Player.Inventory)
                            Log.Information("[BOT RUNNER] Equipment slot {Slot}: GUID=0x{Guid:X}", kvp.Key, kvp.Value);
                        Log.Information("[BOT RUNNER] Protobuf Inventory map count={Count}", _activitySnapshot.Player.Inventory.Count);
                    }
                }
                else
                {
                    Log.Warning("[BOT RUNNER] Equipment skipped: Player={HasPlayer}, IsIWoWPlayer={IsType}",
                        _activitySnapshot.Player != null, player is GameData.Core.Interfaces.IWoWPlayer);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating equipment inventory: {ex.Message}");
            }

            // Inventory items (bagContents map: sequential index → itemId)
            try
            {
                if (_activitySnapshot.Player != null)
                {
                    _activitySnapshot.Player.BagContents.Clear();
                    uint slotIndex = 0;
                    foreach (var item in _objectManager.GetContainedItems())
                    {
                        _activitySnapshot.Player.BagContents[slotIndex++] = item.ItemId;
                    }

                    // Diagnostic: log item counts when they change
                    var itemObjectCount = _objectManager.Objects.Count(o => o.ObjectType == GameData.Core.Enums.WoWObjectType.Item);
                    if (slotIndex != _lastLoggedContainedItems || itemObjectCount != _lastLoggedItemObjects)
                    {
                        _lastLoggedContainedItems = (int)slotIndex;
                        _lastLoggedItemObjects = itemObjectCount;
                        Log.Information("[BOT RUNNER] Inventory changed: {ContainedItems} contained items, {ItemObjects} item objects in OM",
                            slotIndex, itemObjectCount);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating inventory: {ex.Message}");
            }

            // Nearby units (within 40y)
            try
            {
                _activitySnapshot.NearbyUnits.Clear();
                var playerPos = player.Position;
                if (playerPos != null)
                {
                    foreach (var unit in _objectManager.Units
                        .Where(u => u.Guid != player.Guid && u.Position != null && u.Position.DistanceTo(playerPos) < 40f))
                    {
                        _activitySnapshot.NearbyUnits.Add(BuildUnitProtobuf(unit));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating nearby units: {ex.Message}");
            }

            // Nearby game objects (within 40y)
            try
            {
                _activitySnapshot.NearbyObjects.Clear();
                var playerPos = player.Position;
                if (playerPos != null)
                {
                    foreach (var go in _objectManager.GameObjects
                        .Where(g => g.Position != null && g.Position.DistanceTo(playerPos) < 40f))
                    {
                        _activitySnapshot.NearbyObjects.Add(BuildGameObjectProtobuf(go));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating nearby objects: {ex.Message}");
            }

        }

        private static Game.WoWPlayer BuildPlayerProtobuf(IWoWUnit unit)
        {
            var player = new Game.WoWPlayer
            {
                Unit = BuildUnitProtobuf(unit),
            };

            if (unit is IWoWLocalPlayer lp)
            {
                try { player.Coinage = lp.Copper; } catch { }
                try { player.CorpseRecoveryDelaySeconds = (uint)Math.Max(0, lp.CorpseRecoveryDelaySeconds); } catch { }
            }

            if (unit is IWoWPlayer wp)
            {
                try { player.PlayerFlags = (uint)wp.PlayerFlags; } catch { }

                try
                {
                    player.QuestLogEntries.Clear();
                    foreach (var slot in wp.QuestLog)
                    {
                        if (slot == null || slot.QuestId == 0)
                            continue;

                        uint packedCounters = 0;
                        var counters = slot.QuestCounters;
                        if (counters != null)
                        {
                            var count = Math.Min(4, counters.Length);
                            for (var i = 0; i < count; i++)
                                packedCounters |= (uint)counters[i] << (i * 8);
                        }

                        player.QuestLogEntries.Add(new Game.QuestLogEntry
                        {
                            QuestLog1 = slot.QuestId,
                            QuestLog2 = packedCounters,
                            QuestLog3 = slot.QuestState,
                        });
                    }
                }
                catch { }

                try
                {
                    int nonZeroSkills = 0;
                    for (int i = 0; i < wp.SkillInfo.Length; i++)
                    {
                        var skill = wp.SkillInfo[i];
                        uint skillId = skill.SkillInt1 & 0xFFFF;
                        if (skillId > 0)
                        {
                            player.SkillInfo[skillId] = skill.SkillInt2 & 0xFFFF;
                            nonZeroSkills++;
                            if (skillId == 356) // Fishing
                                Log.Information("[SkillSnapshot] Fishing slot {Slot}: Int1=0x{Int1:X8} Int2=0x{Int2:X8} Int3=0x{Int3:X8} → id={Id} val={Val} max={Max}",
                                    i, skill.SkillInt1, skill.SkillInt2, skill.SkillInt3,
                                    skillId, skill.SkillInt2 & 0xFFFF, (skill.SkillInt2 >> 16) & 0xFFFF);
                            if (skillId == 186) // Mining
                                Log.Information("[SkillSnapshot] Mining slot {Slot}: Int1=0x{Int1:X8} Int2=0x{Int2:X8} Int3=0x{Int3:X8} → id={Id} val={Val} max={Max}",
                                    i, skill.SkillInt1, skill.SkillInt2, skill.SkillInt3,
                                    skillId, skill.SkillInt2 & 0xFFFF, (skill.SkillInt2 >> 16) & 0xFFFF);
                        }
                    }
                    if (nonZeroSkills > 0)
                        Log.Information("[SkillSnapshot] {Count} skills populated", nonZeroSkills);
                }
                catch { }
            }

            return player;
        }

        private static Game.WoWUnit BuildUnitProtobuf(IWoWUnit unit)
        {
            // Core fields (Guid, Position, Health) — these work for both FG and BG.
            // Extended fields (FactionTemplate, Powers, Auras, etc.) may throw
            // NotImplementedException on FG's memory-based objects, so wrap them individually.
            var pos = unit.Position;
            var protoUnit = new Game.WoWUnit
            {
                GameObject = new Game.WoWGameObject
                {
                    Base = new Game.WoWObject
                    {
                        Guid = unit.Guid,
                        ObjectType = (uint)unit.ObjectType,
                    },
                    Level = unit.Level,
                },
                Health = unit.Health,
                MaxHealth = unit.MaxHealth,
            };

            if (pos != null)
                protoUnit.GameObject.Base.Position = new Game.Position { X = pos.X, Y = pos.Y, Z = pos.Z };

            // Extended fields — individually guarded for FG compatibility
            try { protoUnit.GameObject.Base.Facing = unit.Facing; } catch { }
            try { protoUnit.GameObject.Base.ScaleX = unit.ScaleX; } catch { }
            try { protoUnit.GameObject.Entry = unit.Entry; } catch { }
            try { protoUnit.GameObject.Name = unit.Name ?? string.Empty; } catch { }
            try { protoUnit.GameObject.FactionTemplate = unit.FactionTemplate; } catch { }
            try { protoUnit.TargetGuid = unit.TargetGuid; } catch { }
            try { protoUnit.UnitFlags = (uint)unit.UnitFlags; } catch { }
            try { protoUnit.DynamicFlags = (uint)unit.DynamicFlags; } catch { }
            try { protoUnit.MovementFlags = (uint)unit.MovementFlags; } catch { }
            try { protoUnit.MountDisplayId = unit.MountDisplayId; } catch { }
            try { protoUnit.ChannelSpellId = unit.ChannelingId; } catch { }
            try { protoUnit.SummonedBy = unit.SummonedByGuid; } catch { }
            try { protoUnit.NpcFlags = (uint)unit.NpcFlags; } catch { }
            try { if (unit.Bytes0 != null && unit.Bytes0.Length > 0) protoUnit.Bytes0 = unit.Bytes0[0]; } catch { }
            try { if (unit.Bytes1 != null && unit.Bytes1.Length > 0) protoUnit.Bytes1 = unit.Bytes1[0]; } catch { }
            try { if (unit.Bytes2 != null && unit.Bytes2.Length > 0) protoUnit.Bytes2 = unit.Bytes2[0]; } catch { }

            // Power map: Mana, Rage, Energy
            try
            {
                if (unit.Powers.TryGetValue(Powers.MANA, out uint mana)) protoUnit.Power[0] = mana;
                if (unit.MaxPowers.TryGetValue(Powers.MANA, out uint maxMana)) protoUnit.MaxPower[0] = maxMana;
                if (unit.Powers.TryGetValue(Powers.RAGE, out uint rage)) protoUnit.Power[1] = rage;
                if (unit.MaxPowers.TryGetValue(Powers.RAGE, out uint maxRage)) protoUnit.MaxPower[1] = maxRage;
                if (unit.Powers.TryGetValue(Powers.ENERGY, out uint energy)) protoUnit.Power[3] = energy;
                if (unit.MaxPowers.TryGetValue(Powers.ENERGY, out uint maxEnergy)) protoUnit.MaxPower[3] = maxEnergy;
            }
            catch { }

            // Auras (from AuraFields - raw spell IDs)
            try
            {
                if (unit.AuraFields != null)
                {
                    foreach (var auraSpellId in unit.AuraFields.Where(a => a != 0))
                        protoUnit.Auras.Add(auraSpellId);
                }
            }
            catch { }

            return protoUnit;
        }

        private static Game.WoWGameObject BuildGameObjectProtobuf(IWoWGameObject go)
        {
            var pos = go.Position;
            var protoGo = new Game.WoWGameObject
            {
                Base = new Game.WoWObject
                {
                    Guid = go.Guid,
                    ObjectType = (uint)go.ObjectType,
                    Facing = go.Facing,
                },
                DisplayId = go.DisplayId,
                GoState = (uint)go.GoState,
                GameObjectType = go.TypeId,
                Flags = go.Flags,
                Level = go.Level,
                Entry = go.Entry,
            };

            if (pos != null)
            {
                protoGo.Base.Position = new Game.Position { X = pos.X, Y = pos.Y, Z = pos.Z };
            }

            return protoGo;
        }
    }
}
