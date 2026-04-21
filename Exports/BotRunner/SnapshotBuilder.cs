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
        private static readonly bool EnableSnapshotPositionDiagnostics =
            string.Equals(
                Environment.GetEnvironmentVariable("WWOW_ENABLE_SNAP_POS_DIAG"),
                "1",
                StringComparison.Ordinal);
        private static readonly bool EnableSnapshotEnvironmentDiagnostics =
            string.Equals(
                Environment.GetEnvironmentVariable("WWOW_ENABLE_ENV_DIAG"),
                "1",
                StringComparison.Ordinal);

        private void PopulateSnapshotFromObjectManager()
        {
            _activitySnapshot.Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Clear any action from the previous response to prevent echo-back.
            // Without this, the old CurrentAction stays in the snapshot, gets sent
            // back to StateManager, and is returned again — causing infinite re-execution.
            _activitySnapshot.CurrentAction = null;

            // Detect screen state
            var playerWorldReady = _objectManager.HasEnteredWorld
                && WorldEntryHydration.IsReadyForWorldInteraction(_objectManager.Player);
            if (playerWorldReady && _objectManager.Player != null)
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

            // Connection state — deterministic, derived from existing IObjectManager properties.
            // Gives StateManager and tests a machine-readable lifecycle signal without Task.Delay guessing.
            var inMapTransition = _objectManager.IsInMapTransition;
            _activitySnapshot.IsMapTransition = inMapTransition;

            if (!_objectManager.HasEnteredWorld)
            {
                if (_objectManager.CharacterSelectScreen?.IsOpen == true)
                    _activitySnapshot.ConnectionState = BotConnectionState.BotCharSelect;
                else if (_objectManager.LoginScreen?.IsLoggedIn == true)
                    _activitySnapshot.ConnectionState = BotConnectionState.BotAuthenticating;
                else
                    _activitySnapshot.ConnectionState = BotConnectionState.BotDisconnected;
            }
            else if (inMapTransition)
            {
                _activitySnapshot.ConnectionState = BotConnectionState.BotTransferring;
            }
            else if (playerWorldReady && _objectManager.Player != null)
            {
                _activitySnapshot.ConnectionState = BotConnectionState.BotInWorld;
            }
            else
            {
                _activitySnapshot.ConnectionState = BotConnectionState.BotEnteringWorld;
            }

            // ObjectManager is valid only when fully in world, not transitioning, and player exists.
            _activitySnapshot.IsObjectManagerValid =
                _activitySnapshot.ConnectionState == BotConnectionState.BotInWorld
                && _objectManager.Player != null
                && !inMapTransition;
            _activitySnapshot.IsIndoors = false;

            // Top-level MapId for reliable BG transfer detection.
            // This bypasses the deep nesting (Player.Unit.GameObject.Base.MapId)
            // that may be unreliable during protobuf serialization with nested sub-messages.
            if (_objectManager.Player is GameData.Core.Interfaces.IWoWPlayer mapPlayer)
                _activitySnapshot.CurrentMapId = (uint)mapPlayer.MapId;

            // Always flush message buffers (even during login — captures GM command errors)
            TrySubscribeToBattlegroundMessageEvents();
            FlushMessageBuffers();

            // Only populate game data when in world and not in a map transition.
            // During cross-map teleports, object pointers become invalid — reading them
            // causes ACCESS_VIOLATION that .NET 8 cannot catch (process termination).
            if (_activitySnapshot.ScreenState != "InWorld" || _objectManager.Player == null
                || inMapTransition)
                return;

            var player = _objectManager.Player;
            _activitySnapshot.IsIndoors = _objectManager.PhysicsIsIndoors;

            if (EnableSnapshotEnvironmentDiagnostics)
            {
                var envFlags = _objectManager.PhysicsEnvironmentFlags;
                var envPos = player.Position;
                DiagLog(
                    $"[SNAP_ENV] map={(player as IWoWPlayer)?.MapId ?? 0} pos=({envPos?.X:F1},{envPos?.Y:F1},{envPos?.Z:F1}) " +
                    $"indoors={_activitySnapshot.IsIndoors} flags=0x{(uint)envFlags:X} objMgrValid={_activitySnapshot.IsObjectManagerValid} transition={inMapTransition}");
            }

            // Track the last known alive position to recover corpse navigation when corpse coordinates
            // are not populated immediately after release on some client/server combinations.
            UpdateLastKnownAlivePosition(player);

            // Movement data
            try
            {
                var pos = player.Position;
                // Optional high-rate diagnostics for snapshot-vs-heartbeat movement comparisons.
                if (EnableSnapshotPositionDiagnostics && _tickCount % 50 == 1 && (uint)player.MovementFlags != 0)
                {
                    Log.Debug("[SNAP_POS] tick={Tick} live=({X:F2},{Y:F2},{Z:F2}) flags=0x{Flags:X}",
                        _tickCount, pos?.X ?? -999, pos?.Y ?? -999, pos?.Z ?? -999,
                        (uint)player.MovementFlags);
                }
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
                    // Prefer the stored LeaderGuid from SMSG_GROUP_LIST (most reliable)
                    var storedLeader = factory.PartyAgent.LeaderGuid;
                    if (storedLeader != 0)
                    {
                        _activitySnapshot.PartyLeaderGuid = storedLeader;
                    }
                    else
                    {
                        // Fallback: check group member IsLeader flags or self-leader state
                        var members = factory.PartyAgent.GetGroupMembers();
                        var leader = members.FirstOrDefault(m => m.IsLeader);
                        _activitySnapshot.PartyLeaderGuid = leader?.Guid ?? (factory.PartyAgent.IsGroupLeader && factory.PartyAgent.GroupSize > 0 ? player.Guid : 0);
                    }
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
                        Log.Debug("[BOT RUNNER] Equipment: {Count} slots occupied (Inventory[].Length={Len})", nonZeroCount, wp.Inventory.Length);
                        foreach (var kvp in _activitySnapshot.Player.Inventory)
                            Log.Debug("[BOT RUNNER] Equipment slot {Slot}: GUID=0x{Guid:X}", kvp.Key, kvp.Value);
                        Log.Debug("[BOT RUNNER] Protobuf Inventory map count={Count}", _activitySnapshot.Player.Inventory.Count);
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

            // Nearby units (within 40y). Re-check the transition guard *inside* the
            // enumeration — a cross-map transfer (battleground entry, teleport) that
            // starts between the top-of-populate guard and this loop invalidates unit
            // pointers mid-iteration. Spline/unit-reaction reads in BuildUnitSnapshot /
            // BuildUnitProtobuf dereference WoW memory directly; an ACCESS_VIOLATION
            // from a stale pointer during transfer teardown cannot be caught by
            // managed catch blocks in .NET 8 and terminates the host WoW.exe.
            try
            {
                _activitySnapshot.NearbyUnits.Clear();
                _activitySnapshot.MovementData?.NearbyUnits.Clear();
                var playerPos = player.Position;
                if (playerPos != null && !_objectManager.IsInMapTransition)
                {
                    foreach (var unit in _objectManager.Units
                        .Where(u => u.Guid != player.Guid && u.Position != null && u.Position.DistanceTo(playerPos) < 40f))
                    {
                        if (_objectManager.IsInMapTransition)
                            break;

                        var protoUnit = BuildUnitProtobuf(unit);
                        _activitySnapshot.NearbyUnits.Add(protoUnit);
                        _activitySnapshot.MovementData?.NearbyUnits.Add(
                            BuildUnitSnapshot(unit, protoUnit, playerPos.DistanceTo(unit.Position)));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating nearby units: {ex.Message}");
            }

            // Nearby game objects (within 40y). Same transition-race concern as the
            // nearby-units loop above — BuildGameObjectProtobuf reads game-object
            // fields directly from WoW memory, and a transition starting mid-loop
            // can hand us a freed pointer.
            try
            {
                _activitySnapshot.NearbyObjects.Clear();
                _activitySnapshot.MovementData?.NearbyGameObjects.Clear();
                var playerPos = player.Position;
                if (playerPos != null && !_objectManager.IsInMapTransition)
                {
                    foreach (var go in _objectManager.GameObjects
                        .Where(g => g.Position != null && g.Position.DistanceTo(playerPos) < 40f))
                    {
                        if (_objectManager.IsInMapTransition)
                            break;

                        var protoGameObject = BuildGameObjectProtobuf(go);
                        _activitySnapshot.NearbyObjects.Add(protoGameObject);
                        _activitySnapshot.MovementData?.NearbyGameObjects.Add(new Game.GameObjectSnapshot
                        {
                            Guid = protoGameObject.Base.Guid,
                            Entry = protoGameObject.Entry,
                            DisplayId = protoGameObject.DisplayId,
                            GameObjectType = protoGameObject.GameObjectType,
                            Flags = protoGameObject.Flags,
                            GoState = protoGameObject.GoState,
                            Position = protoGameObject.Base.Position == null
                                ? null
                                : new Game.Position
                                {
                                    X = protoGameObject.Base.Position.X,
                                    Y = protoGameObject.Base.Position.Y,
                                    Z = protoGameObject.Base.Position.Z,
                                },
                            Facing = protoGameObject.Base.Facing,
                            Name = protoGameObject.Name ?? string.Empty,
                            Scale = protoGameObject.Base.ScaleX,
                            AnimProgress = protoGameObject.AnimProgress,
                            DistanceToPlayer = playerPos.DistanceTo(go.Position),
                        });
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
                TryPopulate(() => player.Coinage = lp.Copper, "Coinage");

                // Skip CorpseRecoveryDelaySeconds during ghost form. The FG implementation
                // calls Lua (GetCorpseRecoveryDelay) via ThreadSynchronizer every tick, which
                // races with MoveToward's direct SendMovementUpdate calls and eventually
                // corrupts a packet — causing a server disconnect ~8s into ghost navigation.
                // RetrieveCorpseTask checks the delay itself when it needs it (line 612).
                var isGhostSnapshot = (((uint)lp.PlayerFlags) & 0x10) != 0; // PLAYER_FLAGS_GHOST
                if (!isGhostSnapshot)
                {
                    TryPopulate(() => player.CorpseRecoveryDelaySeconds = (uint)Math.Max(0, lp.CorpseRecoveryDelaySeconds), "CorpseRecoveryDelay");
                }
            }

            if (unit is IWoWPlayer wp)
            {
                TryPopulate(() => player.Unit.GameObject.Base.MapId = wp.MapId, "MapId");
                TryPopulate(() => player.PlayerFlags = (uint)wp.PlayerFlags, "PlayerFlags");

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
                catch (Exception ex) { Log.Debug("[Snapshot] QuestLog unavailable: {Type}", ex.GetType().Name); }

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
                                Log.Debug("[SkillSnapshot] Fishing slot {Slot}: Int1=0x{Int1:X8} Int2=0x{Int2:X8} Int3=0x{Int3:X8} -> id={Id} val={Val} max={Max}",
                                    i, skill.SkillInt1, skill.SkillInt2, skill.SkillInt3,
                                    skillId, skill.SkillInt2 & 0xFFFF, (skill.SkillInt2 >> 16) & 0xFFFF);
                            if (skillId == 186) // Mining
                                Log.Debug("[SkillSnapshot] Mining slot {Slot}: Int1=0x{Int1:X8} Int2=0x{Int2:X8} Int3=0x{Int3:X8} -> id={Id} val={Val} max={Max}",
                                    i, skill.SkillInt1, skill.SkillInt2, skill.SkillInt3,
                                    skillId, skill.SkillInt2 & 0xFFFF, (skill.SkillInt2 >> 16) & 0xFFFF);
                        }
                    }
                    if (nonZeroSkills > 0)
                        Log.Debug("[SkillSnapshot] {Count} skills populated", nonZeroSkills);
                }
                catch (Exception ex) { Log.Debug("[Snapshot] SkillInfo unavailable: {Type}", ex.GetType().Name); }
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
            TryPopulate(() => protoUnit.GameObject.Base.Facing = unit.Facing, "Facing");
            TryPopulate(() => protoUnit.GameObject.Base.ScaleX = unit.ScaleX, "ScaleX");
            TryPopulate(() => protoUnit.GameObject.Entry = unit.Entry, "Entry");
            TryPopulate(() => protoUnit.GameObject.Name = unit.Name ?? string.Empty, "Name");
            TryPopulate(() => protoUnit.GameObject.FactionTemplate = unit.FactionTemplate, "FactionTemplate");
            TryPopulate(() => protoUnit.TargetGuid = unit.TargetGuid, "TargetGuid");
            TryPopulate(() => protoUnit.UnitFlags = (uint)unit.UnitFlags, "UnitFlags");
            TryPopulate(() => protoUnit.DynamicFlags = (uint)unit.DynamicFlags, "DynamicFlags");
            TryPopulate(() => protoUnit.MovementFlags = (uint)unit.MovementFlags, "MovementFlags");
            TryPopulate(() => protoUnit.MountDisplayId = unit.MountDisplayId, "MountDisplayId");
            TryPopulate(() => protoUnit.ChannelSpellId = unit.ChannelingId, "ChannelSpellId");
            TryPopulate(() => protoUnit.SummonedBy = unit.SummonedByGuid, "SummonedBy");
            TryPopulate(() => protoUnit.NpcFlags = (uint)unit.NpcFlags, "NpcFlags");
            TryPopulate(() => protoUnit.BoundingRadius = unit.BoundingRadius, "BoundingRadius");
            TryPopulate(() => protoUnit.CombatReach = unit.CombatReach, "CombatReach");
            TryPopulate(() => protoUnit.UnitReaction = (uint)unit.UnitReaction, "UnitReaction");
            TryPopulate(() => { if (unit.Bytes0 != null && unit.Bytes0.Length > 0) protoUnit.Bytes0 = unit.Bytes0[0]; }, "Bytes0");
            TryPopulate(() => { if (unit.Bytes1 != null && unit.Bytes1.Length > 0) protoUnit.Bytes1 = unit.Bytes1[0]; }, "Bytes1");
            TryPopulate(() => { if (unit.Bytes2 != null && unit.Bytes2.Length > 0) protoUnit.Bytes2 = unit.Bytes2[0]; }, "Bytes2");

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
            catch (Exception ex) { Log.Debug("[Snapshot] Powers unavailable: {Type}", ex.GetType().Name); }

            // Auras (from AuraFields - raw spell IDs)
            try
            {
                if (unit.AuraFields != null)
                {
                    foreach (var auraSpellId in unit.AuraFields.Where(a => a != 0))
                        protoUnit.Auras.Add(auraSpellId);
                }
            }
            catch (Exception ex) { Log.Debug("[Snapshot] Auras unavailable: {Type}", ex.GetType().Name); }

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
                    ScaleX = go.ScaleX,
                },
                DisplayId = go.DisplayId,
                GoState = (uint)go.GoState,
                GameObjectType = go.TypeId,
                Flags = go.Flags,
                DynamicFlags = (uint)go.DynamicFlags,
                Level = go.Level,
                FactionTemplate = go.FactionTemplate,
                Entry = go.Entry,
                ArtKit = go.ArtKit,
                AnimProgress = go.AnimProgress,
                Name = go.Name ?? string.Empty,
            };

            if (pos != null)
            {
                protoGo.Base.Position = new Game.Position { X = pos.X, Y = pos.Y, Z = pos.Z };
            }

            return protoGo;
        }

        private static Game.UnitSnapshot BuildUnitSnapshot(IWoWUnit unit, Game.WoWUnit protoUnit, float distanceToPlayer)
        {
            var snapshot = new Game.UnitSnapshot
            {
                Guid = protoUnit.GameObject.Base.Guid,
                Entry = protoUnit.GameObject.Entry,
                Name = protoUnit.GameObject.Name ?? string.Empty,
                Facing = protoUnit.GameObject.Base.Facing,
                MovementFlags = (uint)protoUnit.MovementFlags,
                Health = protoUnit.Health,
                MaxHealth = protoUnit.MaxHealth,
                Level = protoUnit.GameObject.Level,
                UnitFlags = protoUnit.UnitFlags,
                DistanceToPlayer = distanceToPlayer,
                BoundingRadius = protoUnit.BoundingRadius,
                CombatReach = protoUnit.CombatReach,
                NpcFlags = (uint)protoUnit.NpcFlags,
                TargetGuid = protoUnit.TargetGuid,
                IsPlayer = protoUnit.GameObject.Base.ObjectType == (uint)WoWObjectType.Player,
            };

            if (protoUnit.GameObject.Base.Position != null)
            {
                snapshot.Position = new Game.Position
                {
                    X = protoUnit.GameObject.Base.Position.X,
                    Y = protoUnit.GameObject.Base.Position.Y,
                    Z = protoUnit.GameObject.Base.Position.Z,
                };
            }

            TryPopulate(() => snapshot.HasSpline = unit.SplineFlags != SplineFlags.None || (uint)(unit.SplineNodes?.Count ?? 0) > 0, "NearbyUnit.HasSpline");
            TryPopulate(() => snapshot.SplineFlags = (uint)unit.SplineFlags, "NearbyUnit.SplineFlags");
            TryPopulate(() => snapshot.SplineTimePassed = unit.SplineTimePassed, "NearbyUnit.SplineTimePassed");
            TryPopulate(() => snapshot.SplineDuration = unit.SplineDuration, "NearbyUnit.SplineDuration");
            TryPopulate(() => snapshot.SplineNodeCount = (uint)(unit.SplineNodes?.Count ?? 0), "NearbyUnit.SplineNodeCount");
            TryPopulate(() =>
            {
                var finalDestination = unit.SplineFinalDestination;
                if (finalDestination != null)
                {
                    snapshot.SplineFinalDestination = new Game.Position
                    {
                        X = finalDestination.X,
                        Y = finalDestination.Y,
                        Z = finalDestination.Z,
                    };
                }
            }, "NearbyUnit.SplineFinalDestination");

            return snapshot;
        }

        /// <summary>
        /// Wraps a snapshot field setter with debug-level logging on failure.
        /// FG objects throw NotImplementedException for unmapped memory fields;
        /// this makes gaps traceable without masking unexpected errors.
        /// </summary>
        private static void TryPopulate(Action setter, string fieldName)
        {
            try { setter(); }
            catch (Exception ex) { Log.Debug("[Snapshot] {Field} unavailable: {Type}", fieldName, ex.GetType().Name); }
        }
    }
}
