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
    /// <summary>
    /// Network event handling: ProcessUpdatesAsync loop, chat messages, teleport/death events,
    /// character select, GM commands, NPC interaction, flight, quest operations, spline activation.
    /// KEPT AS PARTIAL: ProcessUpdatesAsync is the core update loop that touches _objects, _objectsLock,
    /// _activePet, _isInControl, _isBeingTeleported, _movementController, PlayerGuid, and Player.
    /// Event handlers manage teleport state transitions that are interleaved with Movement.cs state.
    /// ~686 lines; extraction would require splitting the update loop's state machine across classes.
    /// </summary>
    public partial class WoWSharpObjectManager
    {
        private const float TeleportStaleLocalUpdateRejectDistance = 10.0f;


        // Temporary diagnostic: log all opcodes received after GAMEOBJ_USE
        internal volatile bool _sniffingGameObjUse = false;

        private DateTime _sniffStartTime;


        // Optional agent factory accessor — set by BackgroundBotWorker for LootTargetAsync
        private Func<IAgentFactory> _agentFactoryAccessor;


        public void SetAgentFactoryAccessor(Func<IAgentFactory> accessor) => _agentFactoryAccessor = accessor;

        private void EventEmitter_OnSpellGo(object? sender, EventArgs e) { }


        private void EventEmitter_OnClientControlUpdate(object? sender, ClientControlUpdateArgs e)
        {
            var localGuid = Player?.Guid ?? PlayerGuid.FullGuid;
            if (localGuid == 0 || localGuid != e.Guid)
            {
                Log.Debug(
                    "[OnClientControlUpdate] ignoring guid=0x{Guid:X} canControl={CanControl}; localGuid=0x{LocalGuid:X}",
                    e.Guid,
                    e.CanControl,
                    localGuid);
                return;
            }

            _hasExplicitClientControlLockout = !e.CanControl;
            _isInControl = e.CanControl;

            if (e.CanControl)
            {
                _isBeingTeleported = false;
            }

            Log.Information(
                "[OnClientControlUpdate] guid=0x{Guid:X} canControl={CanControl} pos=({X:F1},{Y:F1},{Z:F1})",
                e.Guid,
                e.CanControl,
                Player.Position.X,
                Player.Position.Y,
                Player.Position.Z);
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
                    }, TaskScheduler.Default);
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
            _characterSelectScreen.MarkCharacterListLoaded();
        }

        private void EventEmitter_OnCharacterCreateResponse(object? sender, CharCreateResponse e)
        {
            if (e.Result == CreateCharacterResult.Success)
            {
                Log.Information("[CharacterSelect] Character creation succeeded. Refreshing character list.");
            }
            else if (e.Result == CreateCharacterResult.InProgress)
            {
                Log.Information("[CharacterSelect] Character creation still in progress. Refreshing character list.");
            }
            else
            {
                Log.Warning("[CharacterSelect] Character creation failed: {Result}", e.Result);
            }

            _characterSelectScreen.HandleCharacterCreateResponse(e.Result);
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

        public bool SupportsDirectGmCommandCapture => true;


        private readonly ConcurrentQueue<ObjectStateUpdate> _pendingUpdates = new();


        public void QueueUpdate(ObjectStateUpdate update)
        {
            _pendingUpdates.Enqueue(update);
        }

        internal int PendingUpdateCount => _pendingUpdates.Count;

        private void ReportTestMutation(
            ulong guid,
            ObjectUpdateOperation operation,
            TestMutationStage stage,
            WoWObjectType objectType,
            string context)
        {
            TestMutationObserver?.Invoke(new TestMutationTrace(guid, operation, stage, objectType, context));
        }


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
                                    WoWObject? existingObject;
                                    int existingIndex;
                                    var isLocalPlayer = update.Guid == PlayerGuid.FullGuid;
                                    lock (_objectsLock)
                                    {
                                        if (isLocalPlayer)
                                        {
                                            existingIndex = -1;
                                            existingObject = Player as WoWObject;
                                        }
                                        else
                                        {
                                            existingIndex = _objects.FindIndex(o => o.Guid == update.Guid);
                                            existingObject = existingIndex >= 0 ? _objects[existingIndex] : null;
                                        }
                                    }

                                    var effectiveUpdateType = ResolveEffectiveCreateObjectType(update.ObjectType, update.Guid);
                                    if (existingObject != null
                                        && effectiveUpdateType != WoWObjectType.None
                                        && existingObject.ObjectType == effectiveUpdateType)
                                    {
                                        // WoW.exe `0x4660A0` looks up the object first (`0x464530`) and,
                                        // on a cache hit, routes the block to `0x466350` instead of the
                                        // new-object path (`0x466E00`/`0x466C70`). That cached-object
                                        // branch runs its movement prepass (`0x5FF070`) before the
                                        // descriptor walker (`0x466590`), so duplicate CREATE blocks
                                        // mutate the existing object in place instead of replacing it.
                                        ApplyMovementUpdateToExistingObject(
                                            existingObject,
                                            update.Guid,
                                            update.MovementData,
                                            isLocalPlayer,
                                            "[Movement-CreateExisting]");
                                        if (update.MovementData != null)
                                        {
                                            ReportTestMutation(
                                                update.Guid,
                                                update.Operation,
                                                TestMutationStage.MovementApplied,
                                                existingObject.ObjectType,
                                                "cached-create");
                                        }
                                        ApplyFieldDiffs(existingObject, update.UpdatedFields);
                                        if (update.UpdatedFields.Count > 0)
                                        {
                                            ReportTestMutation(
                                                update.Guid,
                                                update.Operation,
                                                TestMutationStage.FieldsApplied,
                                                existingObject.ObjectType,
                                                "cached-create");
                                        }
                                        FinalizeUpdatedObject(existingObject, existingIndex, update.Guid);
                                        break;
                                    }

                                    var newObject = CreateObjectFromType(
                                        update.ObjectType,
                                        update.Guid
                                    );
                                    ApplyFieldDiffs(newObject, update.UpdatedFields);
                                    if (update.UpdatedFields.Count > 0)
                                    {
                                        ReportTestMutation(
                                            update.Guid,
                                            update.Operation,
                                            TestMutationStage.FieldsApplied,
                                            newObject.ObjectType,
                                            "create");
                                    }
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

                                    ApplyMovementUpdateToExistingObject(
                                        newObject,
                                        update.Guid,
                                        update.MovementData,
                                        newObject is WoWLocalPlayer,
                                        "[Movement-Add]");
                                    if (update.MovementData != null)
                                    {
                                        ReportTestMutation(
                                            update.Guid,
                                            update.Operation,
                                            TestMutationStage.MovementApplied,
                                            newObject.ObjectType,
                                            "create");
                                    }

                                    if (newObject is WoWPlayer)
                                    {
                                        _ = _woWClient.SendNameQueryAsync(update.Guid);

                                        if (newObject is WoWLocalPlayer)
                                        {
                                            Log.Information("[LocalPlayer-Add] Taking control");
                                            _ = _woWClient.SendSetActiveMoverAsync(PlayerGuid.FullGuid);
                                            _isInControl = true;
                                            _hasExplicitClientControlLockout = false;
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
                                        pet.ObjectManager = this;
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
                                    if (update.UpdatedFields.Count > 0)
                                    {
                                        ReportTestMutation(
                                            update.Guid,
                                            update.Operation,
                                            TestMutationStage.FieldsApplied,
                                            obj.ObjectType,
                                            "update");
                                    }

                                    ApplyMovementUpdateToExistingObject(
                                        obj,
                                        update.Guid,
                                        update.MovementData,
                                        isLocalPlayer,
                                        "[Movement-Update]");
                                    if (update.MovementData != null)
                                    {
                                        ReportTestMutation(
                                            update.Guid,
                                            update.Operation,
                                            TestMutationStage.MovementApplied,
                                            obj.ObjectType,
                                            "update");
                                    }
                                    FinalizeUpdatedObject(obj, index, update.Guid);

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

        private static bool IsStaleLocalTeleportMovementUpdate(WoWUnit unit, MovementInfoUpdate movementData)
        {
            var currentPosition = unit.Position;
            if (currentPosition == null)
                return false;

            var dx = movementData.X - currentPosition.X;
            var dy = movementData.Y - currentPosition.Y;
            var dz = movementData.Z - currentPosition.Z;
            var distance = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            return distance > TeleportStaleLocalUpdateRejectDistance;
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
            if (!await EnsureTaxiMapReadyAsync(fm, flightMasterGuid, ct))
            {
                _logger.LogWarning(
                    "DiscoverTaxiNodesAsync failed: taxi map never opened for FM {FlightMasterGuid:X}",
                    flightMasterGuid);
                try { await fm.CloseTaxiMapAsync(ct); } catch { }
                return Array.Empty<uint>();
            }

            var nodes = fm.AvailableTaxiNodes.ToArray();
            try { await fm.CloseTaxiMapAsync(ct); } catch { }
            return nodes;
        }

        private void ApplyMovementUpdateToExistingObject(
            WoWObject obj,
            ulong guid,
            MovementInfoUpdate? movementData,
            bool isLocalPlayer,
            string logPrefix)
        {
            if (movementData == null)
                return;

            if (obj is WoWUnit unit)
            {
                // Only guard position writes for the local player (client-side prediction handles it).
                // Other units should always accept server position updates.
                var effectiveMovementData = movementData;

                // During teleports, clear queued moving/turn flags for local player updates.
                // This prevents stale MOVEFLAG_FORWARD from pre-teleport packets from getting re-applied.
                var rejectStaleTeleportMovement = false;
                if (isLocalPlayer && _isBeingTeleported)
                {
                    rejectStaleTeleportMovement = IsStaleLocalTeleportMovementUpdate(unit, effectiveMovementData);
                    if (rejectStaleTeleportMovement)
                    {
                        Log.Warning(
                            "[TeleportGuard] Ignoring stale local movement update during teleport: " +
                            "current=({CurX:F1},{CurY:F1},{CurZ:F1}) incoming=({NewX:F1},{NewY:F1},{NewZ:F1})",
                            obj.Position.X,
                            obj.Position.Y,
                            obj.Position.Z,
                            effectiveMovementData.X,
                            effectiveMovementData.Y,
                            effectiveMovementData.Z);
                    }
                    else
                    {
                        effectiveMovementData = effectiveMovementData.Clone();
                        effectiveMovementData.MovementFlags &= ~MovementFlags.MOVEFLAG_MASK_MOVING_OR_TURN;
                    }
                }

                bool allowLocalOverwrite = !isLocalPlayer || !(_isInControl && !_isBeingTeleported);
                if (rejectStaleTeleportMovement)
                    allowLocalOverwrite = false;

                var allowMovementFlagWrite = allowLocalOverwrite && !rejectStaleTeleportMovement;
                ApplyMovementData(unit, effectiveMovementData, allowLocalOverwrite, allowMovementFlagWrite);

                // Wire spline data to SplineController for server-driven movement.
                TryActivateSpline(guid, effectiveMovementData, isLocalPlayer);

                Log.Verbose("{LogPrefix} Guid={Guid:X} Pos=({X:F2},{Y:F2},{Z:F2}) Flags=0x{Flags:X8}{Local}",
                    logPrefix, guid, effectiveMovementData.X, effectiveMovementData.Y,
                    effectiveMovementData.Z, (uint)effectiveMovementData.MovementFlags,
                    obj is WoWLocalPlayer ? " [LOCAL]" : "");
            }
            else if (obj is WoWGameObject gameObj)
            {
                ApplyMovementData(gameObj, movementData);
                TryActivateSpline(guid, movementData, false);

                Log.Verbose("{LogPrefix}-GO Guid={Guid:X} Pos=({X:F2},{Y:F2},{Z:F2}) Flags=0x{Flags:X8}",
                    logPrefix, guid, movementData.X, movementData.Y,
                    movementData.Z, (uint)movementData.MovementFlags);
            }
        }

        private void FinalizeUpdatedObject(WoWObject obj, int index, ulong guid)
        {
            // Pet discovery on field update (SummonedBy may arrive in a later update).
            if (obj is WoWUnit updatedUnit && updatedUnit is not WoWLocalPet
                && updatedUnit.SummonedBy.FullGuid == PlayerGuid.FullGuid
                && PlayerGuid.FullGuid != 0)
            {
                var pet = new WoWLocalPet(updatedUnit.HighGuid, updatedUnit.ObjectType);
                pet.ObjectManager = this;
                pet.CopyFrom(updatedUnit);
                if (index >= 0)
                {
                    lock (_objectsLock)
                    {
                        _objects[index] = pet;
                    }
                }

                _activePet = pet;
                Log.Information("[PET] Promoted unit 0x{Guid:X} to pet on update", pet.Guid);
            }
            else if (index >= 0)
            {
                lock (_objectsLock)
                {
                    _objects[index] = obj;
                }
            }

            // Keep active pet state in sync with field diffs.
            if (_activePet != null && guid == _activePet.Guid && obj is WoWUnit petUnit)
            {
                _activePet.CopyFrom(petUnit);
            }
        }


        public async Task<bool> ActivateFlightAsync(ulong flightMasterGuid, uint destinationNodeId, CancellationToken ct = default)
        {
            var factory = _agentFactoryAccessor?.Invoke();
            if (factory == null)
            {
                _logger.LogWarning("ActivateFlightAsync failed: AgentFactory unavailable for FM {FlightMasterGuid:X} -> node {DestinationNodeId}", flightMasterGuid, destinationNodeId);
                return false;
            }

            var fm = factory.FlightMasterAgent;

            if (!await EnsureTaxiMapReadyAsync(fm, flightMasterGuid, ct))
            {
                _logger.LogWarning("ActivateFlightAsync failed: taxi map never opened for FM {FlightMasterGuid:X} -> node {DestinationNodeId}", flightMasterGuid, destinationNodeId);
                return false;
            }

            var sourceNodeId = fm.CurrentNodeId;
            if (!sourceNodeId.HasValue || !fm.IsNodeAvailable(destinationNodeId))
            {
                try { await fm.CloseTaxiMapAsync(ct); } catch { }
                if (await EnsureTaxiMapReadyAsync(fm, flightMasterGuid, ct))
                    sourceNodeId = fm.CurrentNodeId;
            }

            if (!sourceNodeId.HasValue || !fm.IsNodeAvailable(destinationNodeId))
            {
                _logger.LogWarning(
                    "ActivateFlightAsync failed: source node unavailable or destination not known for FM {FlightMasterGuid:X}. CurrentNode={CurrentNodeId}, destination={DestinationNodeId}, availableCount={AvailableCount}",
                    flightMasterGuid,
                    sourceNodeId,
                    destinationNodeId,
                    fm.AvailableTaxiNodes.Count);
                try { await fm.CloseTaxiMapAsync(ct); } catch { }
                return false;
            }

            try
            {
                await fm.ActivateFlightAsync(flightMasterGuid, sourceNodeId.Value, destinationNodeId, ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ActivateFlightAsync failed while activating FM {FlightMasterGuid:X} from node {SourceNodeId} to {DestinationNodeId}", flightMasterGuid, sourceNodeId.Value, destinationNodeId);
                try { await fm.CloseTaxiMapAsync(ct); } catch { }
                return false;
            }
        }

        private async Task<bool> EnsureTaxiMapReadyAsync(
            IFlightMasterNetworkClientComponent flightMasterAgent,
            ulong flightMasterGuid,
            CancellationToken ct)
        {
            if (HasTaxiMapData(flightMasterAgent))
                return true;

            await flightMasterAgent.HelloFlightMasterAsync(flightMasterGuid, ct);
            if (await WaitForTaxiMapAsync(flightMasterAgent, ct))
                return true;

            await flightMasterAgent.QueryAvailableNodesAsync(flightMasterGuid, ct);
            return await WaitForTaxiMapAsync(flightMasterAgent, ct);
        }

        private static bool HasTaxiMapData(IFlightMasterNetworkClientComponent flightMasterAgent)
        {
            return flightMasterAgent.IsTaxiMapOpen
                && flightMasterAgent.CurrentNodeId.HasValue
                && flightMasterAgent.AvailableTaxiNodes.Count > 0;
        }

        private static async Task<bool> WaitForTaxiMapAsync(
            IFlightMasterNetworkClientComponent flightMasterAgent,
            CancellationToken ct)
        {
            for (int i = 0; i < 20; i++)
            {
                if (HasTaxiMapData(flightMasterAgent))
                    return true;

                await Task.Delay(100, ct);
            }

            return HasTaxiMapData(flightMasterAgent);
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
                SplineCtrl.Remove(guid);
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

            SplineCtrl.AddOrUpdate(spline, currentWorldTimeMs);

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
