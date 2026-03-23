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

        // Temporary diagnostic: log all opcodes received after GAMEOBJ_USE
        internal volatile bool _sniffingGameObjUse = false;

        private DateTime _sniffStartTime;


        // Optional agent factory accessor — set by BackgroundBotWorker for LootTargetAsync
        private Func<IAgentFactory> _agentFactoryAccessor;


        public void SetAgentFactoryAccessor(Func<IAgentFactory> accessor) => _agentFactoryAccessor = accessor;

        private void EventEmitter_OnSpellGo(object? sender, EventArgs e) { }


        private void EventEmitter_OnClientControlUpdate(object? sender, EventArgs e)
        {
            _isInControl = true;
            _isBeingTeleported = false;

            Log.Information("[OnClientControlUpdate] pos=({X:F1},{Y:F1},{Z:F1}) — server confirmed teleport complete",
                Player.Position.X, Player.Position.Y, Player.Position.Z);
        }


        private void EventEmitter_OnSetTimeSpeed(object? sender, OnSetTimeSpeedArgs e)
        {
            _ = _woWClient.QueryTimeAsync();
        }

        /// <summary>
        /// Optional callback invoked each game loop tick after movement/physics.
        /// Use this to drive bot AI logic (pathfinding, combat rotation, etc.).
        /// The float parameter is delta time in seconds.
        /// </summary>


        private void EventEmitter_OnChatMessage(object? sender, ChatMessageArgs e)
        {
            string prefix = e.MsgType switch
            {
                ChatMsg.CHAT_MSG_SAY or ChatMsg.CHAT_MSG_MONSTER_SAY => $"[{e.SenderGuid}]",
                ChatMsg.CHAT_MSG_YELL or ChatMsg.CHAT_MSG_MONSTER_YELL => $"[{e.SenderGuid}]",
                ChatMsg.CHAT_MSG_WHISPER or ChatMsg.CHAT_MSG_MONSTER_WHISPER =>
                    $"[{(Objects.FirstOrDefault(x => x.Guid == e.SenderGuid) as WoWUnit)?.Name ?? ""}]",
                ChatMsg.CHAT_MSG_WHISPER_INFORM => $"To[{e.SenderGuid}]",
                ChatMsg.CHAT_MSG_EMOTE or ChatMsg.CHAT_MSG_TEXT_EMOTE or
                ChatMsg.CHAT_MSG_MONSTER_EMOTE or ChatMsg.CHAT_MSG_RAID_BOSS_EMOTE => $"[{e.SenderGuid}]",
                ChatMsg.CHAT_MSG_SYSTEM => "[System]",
                ChatMsg.CHAT_MSG_PARTY or ChatMsg.CHAT_MSG_RAID or
                ChatMsg.CHAT_MSG_GUILD or ChatMsg.CHAT_MSG_OFFICER => $"[{e.SenderGuid}]",
                ChatMsg.CHAT_MSG_CHANNEL or ChatMsg.CHAT_MSG_CHANNEL_NOTICE => "[Channel]",
                ChatMsg.CHAT_MSG_RAID_WARNING => "[Raid Warning]",
                ChatMsg.CHAT_MSG_LOOT => "[Loot]",
                _ => $"[{e.SenderGuid}][{e.MsgType}]",
            };

            if (e.MsgType == ChatMsg.CHAT_MSG_SYSTEM)
            {
                _systemMessages.Enqueue(e.Text);

                if (e.Text.StartsWith("You are being teleported"))
                {
                    ResetMovementStateForTeleport("chat-teleport-message");
                    _isBeingTeleported = true;
                    _teleportFlagSetTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                    // Safety timeout: if MSG_MOVE_TELEPORT never arrives (server rejected
                    // the teleport after sending the chat message), auto-clear after 2s
                    // to avoid permanently blocking the movement controller.
                    var capturedTicks = _teleportFlagSetTicks;
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        if (_isBeingTeleported && _teleportFlagSetTicks == capturedTicks)
                        {
                            Log.Warning("[TELEPORT] 2s timeout: clearing _isBeingTeleported (MSG_MOVE_TELEPORT never arrived after chat notification)");
                            _isBeingTeleported = false;
                        }
                    });
                }
            }

            Log.Information("[Chat] {MsgType} {Prefix}{Text}", e.MsgType, prefix, e.Text);
        }

        /// <summary>
        /// Called by MovementHandler BEFORE queuing a teleport position update,
        /// so the position write guard in ProcessUpdatesAsync allows it through.
        /// </summary>


        private void EventEmitter_OnCharacterListLoaded(object? sender, EventArgs e)
        {
            _characterSelectScreen.HasReceivedCharacterList = true;
        }


        private void EventEmitter_OnWorldSessionStart(object? sender, EventArgs e)
        {
            _characterSelectScreen.RefreshCharacterListFromServer();
        }


        private void EventEmitter_OnLoginFailure(object? sender, EventArgs e)
        {
            Log.Error("[Login] Login failed");
            _woWClient.Dispose();
        }


        private void EventEmitter_OnWorldSessionEnd(object? sender, EventArgs e)
        {
            ResetWorldSessionState("OnWorldSessionEnd");
        }

        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _systemMessages = new();

        /// <summary>
        /// Returns an object by its full GUID, or null if not found.
        /// Checks the local player first, then the objects list.
        /// </summary>


        /// <summary>
        /// Drains all pending system messages (CHAT_MSG_SYSTEM) received since last call.
        /// </summary>
        public List<string> DrainSystemMessages()
        {
            var messages = new List<string>();
            while (_systemMessages.TryDequeue(out var msg))
                messages.Add(msg);
            return messages;
        }


        private readonly ConcurrentQueue<ObjectStateUpdate> _pendingUpdates = new();


        public void QueueUpdate(ObjectStateUpdate update)
        {
            _pendingUpdates.Enqueue(update);
        }

        internal int PendingUpdateCount => _pendingUpdates.Count;


        public async Task ProcessUpdatesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await _updateSemaphore.WaitAsync(token);
                try
                {
                    while (_pendingUpdates.TryDequeue(out var update))
                    {

                        Log.Verbose("[ProcessUpdates] Op={Op} Type={Type} Guid={Guid:X}",
                            update.Operation, update.ObjectType, update.Guid);

                        try
                        {
                            switch (update.Operation)
                            {
                                case ObjectUpdateOperation.Add:
                                {
                                    var newObject = CreateObjectFromFields(
                                        update.ObjectType,
                                        update.Guid,
                                        update.UpdatedFields
                                    );
                                    lock (_objectsLock)
                                    {
                                        _objects.RemoveAll(existing => existing.Guid == newObject.Guid);
                                        _objects.Add(newObject);
                                    }

                                    if (newObject is WoWItem item)
                                        Log.Information("[ProcessUpdates] ITEM CREATED: Guid={Guid:X} ItemId={ItemId} Fields={FieldCount}",
                                            update.Guid, item.ItemId, update.UpdatedFields.Count);

                                    if (newObject is WoWGameObject go)
                                        Log.Information("[ProcessUpdates] GAMEOBJ CREATED: Guid=0x{Guid:X} DisplayId={DisplayId} TypeId={TypeId} CreatedBy=0x{CreatedBy:X} Pos=({X:F1},{Y:F1},{Z:F1})",
                                            update.Guid, go.DisplayId, go.TypeId, go.CreatedBy.FullGuid, go.Position.X, go.Position.Y, go.Position.Z);

                                    if (update.MovementData != null)
                                    {
                                        if (newObject is WoWUnit unit)
                                        {
                                            ApplyMovementData(unit, update.MovementData);
                                        }
                                        else if (newObject is WoWGameObject gameObj)
                                        {
                                            // Apply position from CREATE_OBJECT movement block to game objects.
                                            // Without this, bobbers/dynamic objects stay at (0,0,0) and never
                                            // appear in NearbyObjects or get matched by SMSG_GAMEOBJECT_CUSTOM_ANIM.
                                            gameObj.Position = new Position(
                                                update.MovementData.X,
                                                update.MovementData.Y,
                                                update.MovementData.Z);
                                        }

                                        Log.Verbose("[Movement-Add] Guid={Guid:X} Pos=({X:F2},{Y:F2},{Z:F2}) Flags=0x{Flags:X8}",
                                            update.Guid, update.MovementData.X, update.MovementData.Y,
                                            update.MovementData.Z, (uint)update.MovementData.MovementFlags);
                                    }

                                    if (newObject is WoWPlayer)
                                    {
                                        _ = _woWClient.SendNameQueryAsync(update.Guid);

                                        if (newObject is WoWLocalPlayer)
                                        {
                                            Log.Information("[LocalPlayer-Add] Taking control");
                                            _ = _woWClient.SendSetActiveMoverAsync(PlayerGuid.FullGuid);
                                            _isInControl = true;
                                            _isBeingTeleported = false;

                                            // Re-create MovementController after cross-map transfer.
                                            // ResetWorldSessionState nulls _movementController, but
                                            // InitializeMovementController is only called from EnterWorld()
                                            // (initial login). Without this, physics never runs after
                                            // teleporting to a different map (e.g. Kalimdor → RFC).
                                            if (_movementController == null)
                                                InitializeMovementController();
                                        }
                                    }

                                    // Pet discovery: promote unit to WoWLocalPet if summoned by player
                                    if (newObject is WoWUnit addedUnit && addedUnit is not WoWLocalPet
                                        && addedUnit.SummonedBy.FullGuid == PlayerGuid.FullGuid
                                        && PlayerGuid.FullGuid != 0)
                                    {
                                        var pet = new WoWLocalPet(addedUnit.HighGuid, addedUnit.ObjectType);
                                        pet.CopyFrom(addedUnit);
                                        lock (_objectsLock)
                                        {
                                            var petIdx = _objects.FindIndex(o => o.Guid == pet.Guid);
                                            if (petIdx >= 0) _objects[petIdx] = pet;
                                        }
                                        _activePet = pet;
                                        Log.Information("[PET] Discovered pet Guid=0x{Guid:X} (summoned by player 0x{PlayerGuid:X})",
                                            pet.Guid, PlayerGuid.FullGuid);
                                    }

                                    break;
                                }

                                case ObjectUpdateOperation.Update:
                                {
                                    WoWObject? obj;
                                    int index;
                                    var isLocalPlayer = update.Guid == PlayerGuid.FullGuid;
                                    lock (_objectsLock)
                                    {
                                        if (isLocalPlayer)
                                        {
                                            index = -1;
                                            obj = Player as WoWObject;
                                        }
                                        else
                                        {
                                            index = _objects.FindIndex(o => o.Guid == update.Guid);
                                            obj = index >= 0 ? _objects[index] : null;
                                        }
                                    }

                                    if (obj == null)
                                    {
                                        Log.Warning("[ProcessUpdates] Update for unknown object {Guid:X}", update.Guid);
                                        break;
                                    }

                                    ApplyFieldDiffs(obj, update.UpdatedFields);

                                    if (update.MovementData != null && obj is WoWUnit)
                                    {
                                        // Only guard position writes for the local player (client-side prediction handles it).
                                        // Other units should always accept server position updates.
                                        var movementData = update.MovementData;

                                        // During teleports, clear queued moving/turn flags for local player updates.
                                        // This prevents stale MOVEFLAG_FORWARD from pre-teleport packets from getting re-applied.
                                        if (isLocalPlayer && _isBeingTeleported && movementData != null)
                                        {
                                            movementData = movementData.Clone();
                                            movementData.MovementFlags &= ~MovementFlags.MOVEFLAG_MASK_MOVING_OR_TURN;
                                        }

                                        bool allowLocalOverwrite = !isLocalPlayer || !(_isInControl && !_isBeingTeleported);
                                        ApplyMovementData((WoWUnit)obj, movementData, allowLocalOverwrite, allowLocalOverwrite);

                                        // Wire spline data to SplineController for server-driven movement
                                        TryActivateSpline(update.Guid, movementData, isLocalPlayer);

                                        Log.Verbose("[Movement-Update] Guid={Guid:X} Pos=({X:F2},{Y:F2},{Z:F2}) Flags=0x{Flags:X8}{Local}",
                                            update.Guid, movementData.X, movementData.Y,
                                            movementData.Z, (uint)movementData.MovementFlags,
                                            obj is WoWLocalPlayer ? " [LOCAL]" : "");
                                    }

                                    // Pet discovery on field update (SummonedBy may arrive in a later update)
                                    if (obj is WoWUnit updatedUnit && updatedUnit is not WoWLocalPet
                                        && updatedUnit.SummonedBy.FullGuid == PlayerGuid.FullGuid
                                        && PlayerGuid.FullGuid != 0)
                                    {
                                        var pet = new WoWLocalPet(updatedUnit.HighGuid, updatedUnit.ObjectType);
                                        pet.CopyFrom(updatedUnit);
                                        if (index >= 0) { lock (_objectsLock) _objects[index] = pet; }
                                        _activePet = pet;
                                        Log.Information("[PET] Promoted unit 0x{Guid:X} to pet on update", pet.Guid);
                                    }
                                    else if (index >= 0)
                                    {
                                        lock (_objectsLock) _objects[index] = obj;
                                    }

                                    // Keep active pet state in sync with field diffs
                                    if (_activePet != null && update.Guid == _activePet.Guid && obj is WoWUnit petUnit)
                                    {
                                        _activePet.CopyFrom(petUnit);
                                    }

                                    break;
                                }

                                case ObjectUpdateOperation.Remove:
                                {
                                    int removed;
                                    lock (_objectsLock) removed = _objects.RemoveAll(x => x.Guid == update.Guid);
                                    Log.Verbose("[Remove] Guid={Guid:X} (removed {Count})", update.Guid, removed);

                                    // Clear pet reference when pet is removed from world
                                    if (_activePet != null && update.Guid == _activePet.Guid)
                                    {
                                        Log.Information("[PET] Pet 0x{Guid:X} removed from world", update.Guid);
                                        _activePet = null;
                                    }
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "[ProcessUpdates] Error processing {Op} for {Guid:X}",
                                update.Operation, update.Guid);
                        }
                    }
                }
                finally
                {
                    _updateSemaphore.Release();
                }

                await Task.Delay(10, token);
            }
        }


        /// <summary>
        /// Send MSG_MOVE_WORLDPORT_ACK to acknowledge a cross-map transfer.
        /// Called when SMSG_TRANSFER_PENDING is received.
        /// </summary>
        public void SendWorldportAck()
        {
            if (_woWClient == null) return;
            Serilog.Log.Information("[WorldportAck] Sending MSG_MOVE_WORLDPORT_ACK");
            _ = _woWClient.SendMSGPackedAsync(Opcode.MSG_MOVE_WORLDPORT_ACK, []);
        }


        public void SendChatMessage(string chatMessage)
        {
            if (_woWClient == null) return;
            // SAY chat requires a faction language, not Universal (server rejects language 0 for chat type 0)
            var language = Player?.Race switch
            {
                Race.Orc or Race.Undead or Race.Tauren or Race.Troll => Language.Orcish,
                _ => Language.Common,
            };
            _ = _woWClient.SendChatMessageAsync(ChatMsg.CHAT_MSG_SAY, language, "", chatMessage);
        }

        /// <summary>
        /// Sends a GM command (e.g. ".go xyz ...") and waits for the server's system message response.
        /// Returns all system messages received within the timeout window.
        /// </summary>


        /// <summary>
        /// Sends a GM command (e.g. ".go xyz ...") and waits for the server's system message response.
        /// Returns all system messages received within the timeout window.
        /// </summary>
        public async Task<List<string>> SendGmCommandAsync(string command, int timeoutMs = 2000)
        {
            // Drain any stale messages
            DrainSystemMessages();

            SendChatMessage(command);

            // Wait for server response
            await Task.Delay(timeoutMs);

            return DrainSystemMessages();
        }


        public async Task<int> LearnAllAvailableSpellsAsync(ulong trainerGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return 0;

            var gossip = factory.GossipAgent;
            var trainer = factory.TrainerAgent;
            await gossip.GreetNpcAsync(trainerGuid, ct);
            await Task.Delay(200, ct);

            try
            {
                await gossip.NavigateToServiceAsync(
                    WoWSharpClient.Networking.ClientComponents.Models.GossipServiceType.Trainer,
                    ct);
                await Task.Delay(200, ct);
            }
            catch
            {
                // Some trainers open directly on greet while others expose trainer through gossip.
                // Always follow up with an explicit trainer-list request.
            }

            await trainer.RequestTrainerServicesAsync(trainerGuid, ct);

            TrainerServiceData[] available = [];
            for (int attempt = 0; attempt < 10; attempt++)
            {
                available = trainer.GetAvailableServices();
                if (available.Length > 0)
                    break;

                await Task.Delay(100, ct);
            }

            if (available == null || available.Length == 0)
            {
                await trainer.CloseTrainerAsync(ct);
                return 0;
            }

            // Sort by cost (cheapest first), filter by player coinage
            var coinage = Player?.Coinage ?? uint.MaxValue;
            var affordable = available
                .Where(s => s.Cost <= coinage)
                .OrderBy(s => s.Cost)
                .ToList();

            int learned = 0;
            foreach (var spell in affordable)
            {
                try
                {
                    await trainer.LearnSpellAsync(trainerGuid, spell.SpellId, ct);
                    coinage -= spell.Cost;
                    learned++;
                    await Task.Delay(200, ct);
                }
                catch { break; }
            }

            await trainer.CloseTrainerAsync(ct);
            return learned;
        }


        public async Task<IReadOnlyList<uint>> DiscoverTaxiNodesAsync(ulong flightMasterGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return Array.Empty<uint>();

            var fm = factory.FlightMasterAgent;
            await fm.HelloFlightMasterAsync(flightMasterGuid, ct);

            for (int i = 0; i < 20; i++)
            {
                if (fm.IsTaxiMapOpen) break;
                await Task.Delay(100, ct);
            }

            var nodes = fm.AvailableTaxiNodes;
            try { await fm.CloseTaxiMapAsync(ct); } catch { }
            return nodes;
        }


        public async Task<bool> ActivateFlightAsync(ulong flightMasterGuid, uint destinationNodeId, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return false;

            var fm = factory.FlightMasterAgent;

            if (!fm.IsTaxiMapOpen)
            {
                await fm.HelloFlightMasterAsync(flightMasterGuid, ct);
                for (int i = 0; i < 20; i++)
                {
                    if (fm.IsTaxiMapOpen) break;
                    await Task.Delay(100, ct);
                }
                if (!fm.IsTaxiMapOpen) return false;
            }

            var sourceNodeId = fm.CurrentNodeId;
            if (!sourceNodeId.HasValue || !fm.IsNodeAvailable(destinationNodeId))
            {
                try { await fm.CloseTaxiMapAsync(ct); } catch { }
                return false;
            }

            try
            {
                await fm.ActivateFlightAsync(flightMasterGuid, sourceNodeId.Value, destinationNodeId, ct);
                return true;
            }
            catch
            {
                try { await fm.CloseTaxiMapAsync(ct); } catch { }
                return false;
            }
        }


        public async Task AcceptQuestFromNpcAsync(ulong npcGuid, uint questId, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;

            var quest = factory.QuestAgent;
            await quest.HelloQuestGiverAsync(npcGuid, ct);
            await Task.Delay(500, ct);
            await quest.AcceptQuestAsync(npcGuid, questId, ct);
        }


        public async Task TurnInQuestAsync(ulong npcGuid, uint questId, uint rewardIndex = 0, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;

            var quest = factory.QuestAgent;
            await quest.HelloQuestGiverAsync(npcGuid, ct);
            await Task.Delay(500, ct);
            await quest.CompleteQuestAsync(npcGuid, questId, ct);
            await Task.Delay(300, ct);
            await quest.ChooseQuestRewardAsync(npcGuid, questId, rewardIndex, ct);
        }


        public async Task InteractWithNpcAsync(ulong npcGuid, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null) return;

            var gossip = factory.GossipAgent;
            await gossip.GreetNpcAsync(npcGuid, ct);
        }

        /// <summary>
        /// Creates a Spline from SMSG_MONSTER_MOVE data and feeds it to the SplineController.
        /// For the local player, this also suppresses physics (spline lockout) until the spline completes.
        /// </summary>
        private void TryActivateSpline(ulong guid, MovementInfoUpdate moveData, bool isLocalPlayer)
        {
            var block = moveData.MovementBlockUpdate;
            if (block == null) return;

            // SplineType.Stop means the server cancelled the spline
            if (block.SplineType == SplineType.Stop)
            {
                Splines.Instance.Remove(guid);
                if (isLocalPlayer && !_isInControl)
                {
                    _isInControl = true;
                    Log.Information("[SplineLockout] Spline stopped for local player — restoring control");
                }
                return;
            }

            // Need spline points and duration to create a meaningful spline
            if (block.SplinePoints == null || block.SplinePoints.Count == 0)
                return;
            if (block.SplineTimestamp == 0)
                return;

            // Build point list: start position (from monster move header) + waypoints
            var points = new List<Position>(block.SplinePoints.Count + 1)
            {
                new(moveData.X, moveData.Y, moveData.Z)
            };
            points.AddRange(block.SplinePoints);
            var worldTimeTracker = _worldTimeTracker ??= new WorldTimeTracker();
            uint currentWorldTimeMs = (uint)worldTimeTracker.NowMS.TotalMilliseconds;

            var spline = new Spline(
                owner: guid,
                id: block.SplineId ?? 0,
                t0: block.SplineStartTime != 0
                    ? block.SplineStartTime
                    : currentWorldTimeMs,
                flags: block.SplineFlags ?? SplineFlags.None,
                pts: points,
                durationMs: block.SplineTimestamp
            );

            Splines.Instance.AddOrUpdate(spline, currentWorldTimeMs);

            // For local player: suppress physics while server controls movement
            if (isLocalPlayer)
            {
                _isInControl = false;
                Log.Information("[SplineLockout] Local player spline active — suppressing physics ({Points} pts, {Duration}ms)",
                    points.Count, block.SplineTimestamp);
            }
        }
    }
}
