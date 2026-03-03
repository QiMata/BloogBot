using BotRunner.Movement;
using GameData.Core.Constants;
using GameData.Core.Models;
using Serilog;
using System;
using System.Linq;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        private static IBehaviourTreeNode BuildWaitSequence(float duration) => new BehaviourTreeBuilder()
                .Sequence("Wait Sequence")
                    .Do("Wait", time => BehaviourTreeStatus.Success)
                .End()
                .Build();
        /// <summary>
        /// Sequence to move the bot to a specific location using given coordinates (x, y, z) and a range (f).
        /// </summary>
        /// <param name="x">The x-coordinate of the destination.</param>
        /// <param name="y">The y-coordinate of the destination.</param>
        /// <param name="z">The z-coordinate of the destination.</param>
        /// <param name="tolerance">How close to get before stopping (0 = default 3 yards).</param>
        /// <returns>IBehaviourTreeNode that manages moving the bot to the specified location.</returns>
        private IBehaviourTreeNode BuildGoToSequence(float x, float y, float z, float tolerance)
        {
            NavigationPath? navPath = null;
            DateTime? noPathSinceUtc = null;
            DateTime lastNoPathLogUtc = DateTime.MinValue;

            return new BehaviourTreeBuilder()
            .Sequence("GoTo Sequence")
                .Do("Move to Location", time =>
                {
                    if (_objectManager.Player?.Position == null)
                        return BehaviourTreeStatus.Running;

                    if (navPath == null)
                    {
                        var (radius, height) = RaceDimensions.GetCapsuleForRace(
                            _objectManager.Player.Race, _objectManager.Player.Gender);
                        navPath = new NavigationPath(_container.PathfindingClient,
                            capsuleRadius: radius, capsuleHeight: height);
                    }

                    var target = new Position(x, y, z);
                    var dist = _objectManager.Player.Position.DistanceTo(target);
                    var arrivalDist = tolerance > 0 ? tolerance : 3f;

                    if (dist < arrivalDist)
                    {
                        _objectManager.StopAllMovement();
                        navPath.Clear();
                        return BehaviourTreeStatus.Success;
                    }

                    // Phase 7: keep acceptance radii tuned to actual movement speed.
                    if (_objectManager.Player.RunSpeed > 0)
                        navPath.UpdateCharacterSpeed(_objectManager.Player.RunSpeed);

                    // Keep GoTo pathfinding-driven so movement mirrors corpse-run behavior and avoids
                    // long stuck-forward loops when direct steering has no valid route.
                    // Phase 2: pass physics wall contact hint so NavigationPath can suppress false stall
                    // detection when the bot is genuinely blocked by geometry.
                    bool hitWall = _objectManager is WoWSharpClient.WoWSharpObjectManager wsOm && wsOm.PhysicsHitWall;
                    var waypoint = navPath.GetNextWaypoint(_objectManager.Player.Position, target, _objectManager.Player.MapId, allowDirectFallback: false, physicsHitWall: hitWall);
                    if (waypoint == null)
                    {
                        _objectManager.StopAllMovement();
                        noPathSinceUtc ??= DateTime.UtcNow;

                        if (DateTime.UtcNow - lastNoPathLogUtc > TimeSpan.FromSeconds(5))
                        {
                            var noPathSeconds = (int)(DateTime.UtcNow - noPathSinceUtc.Value).TotalSeconds;
                            Log.Warning("[GOTO] No route to ({X:F1},{Y:F1},{Z:F1}) for {Seconds}s; waiting for path.",
                                target.X, target.Y, target.Z, noPathSeconds);
                            lastNoPathLogUtc = DateTime.UtcNow;
                        }

                        return BehaviourTreeStatus.Running;
                    }

                    noPathSinceUtc = null;

                    // Calculate facing toward next waypoint
                    var dx = waypoint.X - _objectManager.Player.Position.X;
                    var dy = waypoint.Y - _objectManager.Player.Position.Y;
                    var facing = MathF.Atan2(dy, dx);

                    _objectManager.MoveToward(waypoint, facing);
                    return BehaviourTreeStatus.Running;
                })
            .End()
            .Build();
        }
        /// <summary>
        /// Sequence to interact with a specific target based on its GUID.
        /// </summary>
        /// <param name="guid">The GUID of the target to interact with.</param>
        /// <returns>IBehaviourTreeNode that manages interacting with the specified target.</returns>
        private IBehaviourTreeNode BuildInteractWithSequence(ulong guid) => new BehaviourTreeBuilder()
            .Sequence("Interact With Sequence")
                .Splice(CheckForTarget(guid))
                // Ensure the target is valid for interaction
                .Condition("Has Valid Target", time => _objectManager.Player.TargetGuid == guid)

                // Perform the interaction
                .Do("Interact with Target", time =>
                {
                    _objectManager.GameObjects.First(x => x.Guid == guid).Interact();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to gather a resource node (herb/ore) by GUID.
        /// WoW 1.12.1 mining/herbalism flow:
        ///   1. CMSG_GAMEOBJ_USE (selects the node, triggers scripts)
        ///   2. CMSG_CAST_SPELL with gathering spell (SPELL_EFFECT_OPEN_LOCK → skill check → loot)
        /// The native WoW client does both automatically via CGGameObject_C::OnRightClick.
        /// The headless client must replicate both steps explicitly.
        /// </summary>
        /// <param name="guid">Game object GUID of the node.</param>
        /// <param name="gatherSpellId">Gathering spell to cast (e.g. 2656 for Mining, 2366 for Herbalism).
        /// If 0, falls back to CMSG_GAMEOBJ_USE only (legacy behavior).</param>
        private IBehaviourTreeNode BuildGatherNodeSequence(ulong guid, int gatherSpellId = 0)
        {
            DateTime? interactedAt = null;
            bool looted = false;
            DateTime? targetClearedAt = null;
            const double GatherChannelSeconds = 5.0;
            const double PostGatherCooldownSeconds = 3.0;

            return new BehaviourTreeBuilder()
                .Sequence("Gather Node Sequence")
                    .Condition("Player Exists", time => _objectManager.Player != null)
                    .Do("Gather Channel", time =>
                    {
                        if (interactedAt == null)
                        {
                            // Stop movement ONCE before interacting — do NOT repeat on subsequent ticks
                            // or the MSG_MOVE_STOP flood will cancel the server-side spell channel.
                            _objectManager.ForceStopImmediate();

                            var node = _objectManager.GameObjects.FirstOrDefault(x => x.Guid == guid);
                            var playerPos = _objectManager.Player.Position;
                            if (node != null)
                            {
                                var dist = playerPos.DistanceTo(node.Position);
                                Log.Information("[GATHER] Node 0x{Guid:X} (Entry={Entry}) found, dist={Dist:F1}y playerPos=({PX:F1},{PY:F1},{PZ:F1})",
                                    guid, node.Entry, dist, playerPos.X, playerPos.Y, playerPos.Z);
                            }
                            else
                            {
                                // Node not in object manager (common for freshly spawned GOs via .gobject add).
                                // Proceed anyway — the server knows the GO exists and will process the spell.
                                Log.Warning("[GATHER] Node 0x{Guid:X} not in GameObjects (count={Count}), casting by GUID anyway. playerPos=({PX:F1},{PY:F1},{PZ:F1})",
                                    guid, _objectManager.GameObjects.Count(), playerPos.X, playerPos.Y, playerPos.Z);
                            }

                            // Send CMSG_GAMEOBJ_USE first, then CMSG_CAST_SPELL.
                            // MaNGOS mining flow: GAMEOBJ_USE on CHEST type runs scripts/triggers,
                            // then CAST_SPELL with the gathering spell processes EffectOpenLock → SendLoot.
                            // Both packets together match what the real WoW client sends.
                            Log.Information("[GATHER] Sending GAMEOBJ_USE + CAST_SPELL for node 0x{Guid:X}, spell={SpellId}", guid, gatherSpellId);
                            _objectManager.InteractWithGameObject(guid);

                            if (gatherSpellId > 0)
                            {
                                _objectManager.CastSpellOnGameObject(gatherSpellId, guid);
                            }

                            // Clear target immediately after the interaction/cast has been sent.
                            // For FG: CGGameObject_C::OnRightClick registers a game object interaction
                            // state machine that holds a pointer to the node. When the node despawns
                            // (~3s after gather), the callback dereferences that pointer → ERROR #132
                            // (ACCESS_VIOLATION at 0x006F876A reading 0x0000001E).
                            // The gather spell is already in flight; clearing target does not cancel it.
                            _objectManager.SetTarget(0);
                            Log.Information("[GATHER] Target pre-cleared (crash prevention) for node 0x{Guid:X}", guid);

                            interactedAt = DateTime.UtcNow;
                        }

                        // Hold tree in Running state while the server-side channel completes.
                        // Stay silent — no movement packets during channel.
                        if ((DateTime.UtcNow - interactedAt.Value).TotalSeconds < GatherChannelSeconds)
                            return BehaviourTreeStatus.Running;

                        Log.Information("[GATHER] Channel complete for node 0x{Guid:X}", guid);
                        return BehaviourTreeStatus.Success;
                    })
                    .Do("Auto-Loot Gather", time =>
                    {
                        if (looted) return BehaviourTreeStatus.Success;

                        // After the channel, the server sends SMSG_LOOT_RESPONSE which opens the loot frame.
                        // Auto-loot all items then close, matching what WoW.exe does natively for right-click gather.
                        if (_objectManager.LootFrame?.IsOpen == true)
                        {
                            Log.Information("[GATHER] Loot frame open with {Count} items — auto-looting", _objectManager.LootFrame.LootCount);
                            _objectManager.LootFrame.LootAll();
                            _objectManager.LootFrame.Close();
                        }
                        else
                        {
                            Log.Information("[GATHER] No loot frame open after channel (node may have been empty or server rejected)");
                        }

                        looted = true;
                        return BehaviourTreeStatus.Success;
                    })
                    .Do("Post-Gather Cooldown", time =>
                    {
                        // Clear target on the first tick, then hold Running for PostGatherCooldownSeconds.
                        // WoW.exe (FG) crashes with ACCESS_VIOLATION (ERROR #132, 0x006F876A) when the
                        // game object interaction state machine still holds a reference to a despawned node.
                        // SetTarget(0) + 3s wait gives the engine time to release the object reference.
                        if (targetClearedAt == null)
                        {
                            _objectManager.SetTarget(0);
                            Log.Information("[GATHER] Target cleared; holding {Secs}s post-gather cooldown (node 0x{Guid:X})",
                                PostGatherCooldownSeconds, guid);
                            targetClearedAt = DateTime.UtcNow;
                            return BehaviourTreeStatus.Running;
                        }

                        if ((DateTime.UtcNow - targetClearedAt.Value).TotalSeconds < PostGatherCooldownSeconds)
                            return BehaviourTreeStatus.Running;

                        Log.Information("[GATHER] Post-gather cooldown complete (node 0x{Guid:X})", guid);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        }

        /// <summary>
        /// Property to check if the player has a target, and if not, sets the target to the specified GUID.
        /// </summary>
        /// <param name="guid">The GUID of the target to set.</param>
        /// <returns>IBehaviourTreeNode that checks for and sets a target if needed.</returns>
        private IBehaviourTreeNode CheckForTarget(ulong guid) => new BehaviourTreeBuilder()
            .Sequence("Check for Target")
                .Condition("Player Exists", time => _objectManager.Player != null)
                .Do("Set Target", time =>
                {
                    if (guid == 0)
                        return BehaviourTreeStatus.Success;

                    if (_objectManager.Player.TargetGuid != guid)
                    {
                        _objectManager.SetTarget(guid);
                    }
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
    }
}
