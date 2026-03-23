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
    public partial class ObjectManager
    {
        private readonly object _fishingBobberInteractLock = new();
        private DateTime _lastFishingBobberInteractAt = DateTime.MinValue;
        private ulong _lastFishingBobberGuid;
        private ulong _lastNpcInteractionGuid;

        public IWoWEventHandler EventHandler { get; }

        private ulong GetActiveNpcInteractionGuid()
        {
            if (_lastNpcInteractionGuid != 0)
                return _lastNpcInteractionGuid;

            try
            {
                return Player?.TargetGuid ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Wraps Functions.LuaCall in ThreadSynchronizer.RunOnMainThread.
        /// All Lua calls MUST execute on WoW's main thread — calling from a background
        /// thread (e.g., BotRunnerService) silently fails.
        /// </summary>


        /// <summary>
        /// Wraps Functions.LuaCall in ThreadSynchronizer.RunOnMainThread.
        /// All Lua calls MUST execute on WoW's main thread — calling from a background
        /// thread (e.g., BotRunnerService) silently fails.
        /// </summary>


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
        public event EventHandler? OnEnteredWorld;

        // These filter from ObjectsBuffer (which is populated by EnumerateVisibleObjects callback)


        // These filter from ObjectsBuffer (which is populated by EnumerateVisibleObjects callback)



        public void DeleteCursorItem()
        {
            MainThreadLuaCall("DeleteCursorItem()");
        }



        public void SendChatMessage(string chatMessage)
        {
            MainThreadLuaCall($"SendChatMessage(\"{chatMessage}\")");
        }



        public void SetRaidTarget(IWoWUnit target, TargetMarker targetMarker)
        {
            SetTarget(target.Guid);
            MainThreadLuaCall($"SetRaidTarget('target', {targetMarker})");
        }



        public void SetTarget(ulong guid)
        {
            if (guid == 0)
            {
                // Clear the target — prevents stale references to despawned objects
                // (e.g., after mining a node that gets removed).
                ThreadSynchronizer.RunOnMainThread<int>(() =>
                {
                    Functions.SetTarget(0);
                    Log.Debug("[FG] Target cleared (SetTarget(0)).");
                    return 0;
                });
                return;
            }

            ThreadSynchronizer.RunOnMainThread<int>(() =>
            {
                var targetPtr = Functions.GetObjectPtr(guid);
                if (targetPtr == nint.Zero)
                {
                    Log.Warning("[FG] SetTarget skipped; GUID 0x{Guid:X} is not in object manager.", guid);
                    return 0;
                }

                Functions.SetTarget(guid);
                return 0;
            });
        }


        public void AcceptGroupInvite()
        {
            MainThreadLuaCall($"StaticPopup1Button1:Click()");
            MainThreadLuaCall($"AcceptGroup()");
        }



        public void EquipCursorItem()
        {
            MainThreadLuaCall("AutoEquipCursorItem()");
        }



        public void ConfirmItemEquip()
        {
            MainThreadLuaCall($"AutoEquipCursorItem()");
            MainThreadLuaCall($"StaticPopup1Button1:Click()");
        }



        public void JoinBattleGroundQueue()
        {
            string enabled = MainThreadLuaCallWithResult("{0} = BattlefieldFrameGroupJoinButton:IsEnabled()")[0];

            if (enabled == "1")
                MainThreadLuaCall("BattlefieldFrameGroupJoinButton:Click()");
            else
                MainThreadLuaCall("BattlefieldFrameJoinButton:Click()");
        }



        private void OnEvent(object sender, OnEventArgs args)
        {
            // Note: CURSOR_UPDATE previously checked memory offset 0xB4B424, but it returns 0 on this WoW client.
            // For now, we rely on DISCONNECTED_FROM_SERVER to detect logout instead.
            if (args.EventName == "DISCONNECTED_FROM_SERVER")
            {
                _ingame1 = false;
                ClearCachedGuid(); // Clear cached GUID on disconnect
                return;
            }

            // LEARNED_SPELL fires when SMSG_LEARNED_SPELL is received by WoW.
            // In WoW 1.12.1, this event is dispatched through the no-args SignalEvent path
            // (SignalEventNoParamsFunPtr), so args is always empty and spellName is always null.
            // We can't do a name-based lookup here; instead we set _forceSpellRefresh so that
            // RefreshSpells() runs immediately on the next bot-loop tick and:
            //   1. Rescans the static spell book array at 0x00B700F0 (catches most spells)
            //   2. Runs the Lua GetTalentInfo enumeration (catches passive talent spells like
            //      Deflection 16462 which appear in talent data but not the spell book array)
            if (args.EventName == "LEARNED_SPELL" || args.EventName == "UNLEARNED_SPELL")
            {
                bool learned = args.EventName == "LEARNED_SPELL";
                var spellName = args.Parameters.Length > 0 ? args.Parameters[0] as string : null;
                DiagLog($"[SPELLBOOK] {args.EventName} event: '{spellName ?? "(null)"}' params={args.Parameters.Length}");
                Log.Information("[SPELLBOOK] {Event} via WoW hook: {SpellName}", args.EventName, spellName ?? "(null)");

                // Trigger an immediate bypass of the 2-second throttle regardless of whether
                // we have a spell name. If we DO have a name (unexpected, but handle it):
                if (!string.IsNullOrEmpty(spellName))
                {
                    if (_spellNameCacheBuilt && _spellNameToIds.TryGetValue(spellName, out var ids))
                    {
                        ApplyResolvedSpellIds(ids, learned);

                        var publishedIds = new HashSet<uint>(_lastKnownSpellIds);
                        if (learned)
                            publishedIds.UnionWith(ids);
                        else
                            publishedIds.ExceptWith(ids);

                        if (Player is LocalPlayer localPlayer)
                            PublishKnownSpellIds(localPlayer, publishedIds);
                        else
                        {
                            _persistentLearnedIds = publishedIds;
                            _lastKnownSpellIds = publishedIds;
                        }

                        var action = learned ? "added" : "removed";
                        DiagLog($"[SPELLBOOK] '{spellName}' -> {ids.Count} IDs {action} (total={publishedIds.Count})");
                    }
                    else
                    {
                        if (learned)
                            _pendingLearnedSpellNames.Add(spellName);
                        else
                            _pendingUnlearnedSpellNames.Add(spellName);

                        DiagLog(
                            $"[SPELLBOOK] '{spellName}' queued for {(learned ? "learn" : "unlearn")} " +
                            $"({(_spellNameCacheBuilt ? "not in cache" : "cache not ready")})");
                    }
                }

                // Force an immediate RefreshSpells on the next bot-loop tick (bypasses 2s throttle).
                _forceSpellRefresh = true;
                return;
            }

            if (args.EventName != "UNIT_MODEL_CHANGED" &&
                args.EventName != "UPDATE_SELECTED_CHARACTER" &&
                args.EventName != "VARIABLES_LOADED") return;
            _ingame1 = true;
        }

        /// <summary>
        /// SIMPLIFIED: Simple polling loop that only reads static memory addresses for login detection.
        /// Does NOT use EnumerateVisibleObjects callback - only memory reads.
        /// NOTE: This runs on a background thread. For WoW API calls that need the main thread,
        /// we use ThreadSynchronizer. However, for initial detection we just use memory reads
        /// which work from any thread.
        /// </summary>
        // Track consecutive failed login checks to debounce reset (avoid resetting on brief GUID=0 glitches)


        /// <summary>
        /// SIMPLIFIED: Simple polling loop that only reads static memory addresses for login detection.
        /// Does NOT use EnumerateVisibleObjects callback - only memory reads.
        /// NOTE: This runs on a background thread. For WoW API calls that need the main thread,
        /// we use ThreadSynchronizer. However, for initial detection we just use memory reads
        /// which work from any thread.
        /// </summary>
        // Track consecutive failed login checks to debounce reset (avoid resetting on brief GUID=0 glitches)



        public void LeaveGroup()
        {
            MainThreadLuaCall("LeaveParty()");
        }



        public void ResetInstances()
        {
            MainThreadLuaCall("ResetInstances()");
        }



        public void PickupMacro(uint v)
        {
            MainThreadLuaCall($"PickupMacro({v})");
        }



        public void PlaceAction(uint v)
        {
            MainThreadLuaCall($"PlaceAction({v})");
        }



        public void ConvertToRaid()
        {
            MainThreadLuaCall("ConvertToRaid()");
        }

        public void ChangeRaidSubgroup(string playerName, byte subGroup)
        {
            // Lua SetRaidSubgroup(raidIndex, subgroup) — subgroup is 1-based in WoW Lua
            // We need the raid index for the player, so use a Lua snippet to find + move them.
            var luaSubgroup = subGroup + 1; // WoW Lua uses 1-based subgroups
            MainThreadLuaCall(
                $"for i=1,40 do local n,_,sg=GetRaidRosterInfo(i);if n=='{playerName}' then SetRaidSubgroup(i,{luaSubgroup});break;end;end");
        }

        public static void InviteToGroup(string characterName)
        {
            MainThreadLuaCall($"InviteByName('{characterName}')");
        }



        public void DoEmote(Emote emote)
        {
            MainThreadLuaCall($"DoEmote(\"{emote}\")");
        }



        public void DoEmote(TextEmote emote)
        {
            MainThreadLuaCall($"DoEmote(\"{emote}\")");
        }



        public void InteractWithGameObject(ulong gameObjectGuid)
        {
            // FG bot: find the game object and call native CGGameObject_C::OnRightClick.
            var obj = Objects.FirstOrDefault(o => o.Guid == gameObjectGuid);
            if (obj is WoWObject wowObj && wowObj.ObjectType == WoWObjectType.GameObj)
            {
                ThreadSynchronizer.RunOnMainThread(() => wowObj.Interact()); // CGGameObject_C::OnRightClick at 0x5F8660
            }
        }

        internal bool TryAutoInteractFishingBobberFromPacket()
        {
            try
            {
                var player = Player;
                if (player?.Position == null)
                    return false;

                var playerGuid = PlayerGuid.FullGuid;
                var bobber = GameObjects
                    .Where(gameObject =>
                        gameObject.Position != null
                        && (gameObject.DisplayId == 668 || gameObject.TypeId == (uint)GameObjectType.FishingNode)
                        && (gameObject.CreatedBy.FullGuid == playerGuid || gameObject.CreatedBy.FullGuid == 0UL))
                    .OrderBy(gameObject => player.Position.DistanceTo(gameObject.Position!))
                    .FirstOrDefault();

                if (bobber?.Position == null)
                    return false;

                var distance = player.Position.DistanceTo(bobber.Position);
                if (distance > 25f)
                    return false;

                var now = DateTime.UtcNow;
                lock (_fishingBobberInteractLock)
                {
                    if (bobber.Guid == _lastFishingBobberGuid
                        && (now - _lastFishingBobberInteractAt).TotalMilliseconds < 1500)
                    {
                        return false;
                    }

                    _lastFishingBobberGuid = bobber.Guid;
                    _lastFishingBobberInteractAt = now;
                }

                ForceStopImmediate();
                InteractWithGameObject(bobber.Guid);
                Log.Information("[FG-FISH] Auto-interacted with bobber 0x{Guid:X} at {Distance:F1}y (createdBy=0x{CreatedBy:X}).",
                    bobber.Guid,
                    distance,
                    bobber.CreatedBy.FullGuid);
                DiagLog($"[FG-FISH] Auto-interacted with bobber 0x{bobber.Guid:X} distance={distance:F1} createdBy=0x{bobber.CreatedBy.FullGuid:X}");
                return true;
            }
            catch (Exception ex)
            {
                DiagLog($"[FG-FISH] Auto-interact failed: {ex.Message}");
                return false;
            }
        }



        public Task LootTargetAsync(ulong targetGuid, CancellationToken ct = default)
        {
            var unit = Objects.FirstOrDefault(o => o.Guid == targetGuid);
            if (unit is WoWObject obj)
                obj.Interact(); // CGUnit_C::OnRightClick / CGGameObject_C::OnRightClick
            return Task.CompletedTask;
        }

        public async Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, CancellationToken ct = default)
        {
            await InteractWithNpcAsync(vendorGuid, ct);
            if (!await WaitForMerchantWindowAsync(ct))
            {
                Log.Warning("[FG-VENDOR] Merchant window did not open for quick vendor visit 0x{Guid:X}.", vendorGuid);
                return;
            }

            try
            {
                var junkItems = GetContainedItems()
                    .Where(item => VendorInteractionHelper.IsLikelyJunk(item.ItemId, item.Name, item.Quality))
                    .ToList();

                foreach (var item in junkItems)
                {
                    ThreadSynchronizer.RunOnMainThread(() =>
                        Functions.SellItemByGuid(
                            VendorInteractionHelper.NormalizeQuantity(Math.Min(item.Quantity, 255u)),
                            vendorGuid,
                            item.Guid));
                    await Task.Delay(100, ct);
                }

                if (_fgMerchantFrame.CanRepair && _fgMerchantFrame.TotalRepairCost > 0)
                {
                    _fgMerchantFrame.RepairAll();
                    await Task.Delay(150, ct);
                }

                if (itemsToBuy != null)
                {
                    foreach (var kvp in itemsToBuy)
                    {
                        int merchantSlot = ResolveMerchantSlot(kvp.Key);
                        if (merchantSlot <= 0)
                        {
                            Log.Warning("[FG-VENDOR] Item {ItemId} not found during quick vendor visit for vendor 0x{Guid:X}.", kvp.Key, vendorGuid);
                            continue;
                        }

                        MainThreadLuaCall(VendorInteractionHelper.BuildBuyMerchantItemLua(merchantSlot, kvp.Value));
                        await Task.Delay(100, ct);
                    }
                }
            }
            finally
            {
                MainThreadLuaCall(VendorInteractionHelper.CloseMerchantLua);
            }
        }



        public void Logout()
        {
            MainThreadLuaCall("Logout()");
        }



        public void AcceptResurrect()
        {
            MainThreadLuaCall("AcceptResurrect()");
        }



        public void Initialize(IWoWActivitySnapshot parProbe)
        {
            // FG ObjectManager is initialized in constructor; this is a no-op
        }

        sbyte IObjectManager.GetTalentRank(uint tabIndex, uint talentIndex)
        {
            return GetTalentRank((int)tabIndex, (int)talentIndex);
        }

        void IObjectManager.PickupInventoryItem(uint inventorySlot)
        {
            PickupInventoryItem((int)inventorySlot);
        }



        public IWoWUnit GetTarget(IWoWUnit woWUnit)
        {
            if (woWUnit == null) return null;
            var targetGuid = woWUnit.TargetGuid;
            if (targetGuid == 0) return null;
            return Units.FirstOrDefault(u => u.Guid == targetGuid);
        }



        public void InviteToGroup(ulong guid)
        {
            // Find the player name by GUID from enumerated objects
            var player = Players.FirstOrDefault(p => p.Guid == guid);
            if (player != null)
                MainThreadLuaCall($"InviteByName('{player.Name}')");
        }



        public void InviteByName(string characterName)
        {
            MainThreadLuaCall($"InviteByName('{characterName}')");
        }



        public void KickPlayer(ulong guid)
        {
            var player = Players.FirstOrDefault(p => p.Guid == guid);
            if (player != null)
                MainThreadLuaCall($"UninviteByName('{player.Name}')");
        }



        public void DeclineGroupInvite()
        {
            MainThreadLuaCall("DeclineGroup()");
            MainThreadLuaCall("StaticPopup1Button2:Click()");
        }



        public void DisbandGroup()
        {
            // In vanilla, leader leaving disbands the group
            MainThreadLuaCall("LeaveParty()");
        }



        public bool HasPendingGroupInvite()
        {
            var result = MainThreadLuaCallWithResult("{0} = StaticPopup1:IsVisible() and StaticPopup1.which == 'PARTY_INVITE'");
            return result.Length > 0 && result[0] == "1";
        }



        public bool HasLootRollWindow(int itemId)
        {
            // Check if a loot roll frame is visible for this item
            var result = MainThreadLuaCallWithResult(
                $"{{0}} = 0; for i=1,4 do local f = getglobal('GroupLootFrame'..i); if f and f:IsVisible() then {{0}} = 1 end end");
            return result.Length > 0 && result[0] == "1";
        }



        public void LootPass(int itemId)
        {
            // Pass on loot roll (rollID is 1-based, we approximate by clicking pass on visible frame)
            MainThreadLuaCall("for i=1,4 do local f = getglobal('GroupLootFrame'..i); if f and f:IsVisible() then local b = getglobal('GroupLootFrame'..i..'PassButton'); if b then b:Click() end end end");
        }



        public void LootRollGreed(int itemId)
        {
            MainThreadLuaCall("for i=1,4 do local f = getglobal('GroupLootFrame'..i); if f and f:IsVisible() then local b = getglobal('GroupLootFrame'..i..'GreedButton'); if b then b:Click() end end end");
        }



        public void LootRollNeed(int itemId)
        {
            MainThreadLuaCall("for i=1,4 do local f = getglobal('GroupLootFrame'..i); if f and f:IsVisible() then local b = getglobal('GroupLootFrame'..i..'NeedButton'); if b then b:Click() end end end");
        }



        public void AssignLoot(int itemId, ulong playerGuid)
        {
            // Master looter: give loot to specific player
            var player = Players.FirstOrDefault(p => p.Guid == playerGuid);
            if (player != null)
            {
                MainThreadLuaCall($"for i=1,GetNumLootItems() do GiveMasterLoot(i, 1) end");
            }
        }



        public void SetGroupLoot(GroupLootSetting setting)
        {
            // 0=FFA, 1=RoundRobin, 2=MasterLooter, 3=GroupLoot, 4=NeedBeforeGreed
            MainThreadLuaCall($"SetLootMethod('group')");
        }



        public void PromoteLootManager(ulong playerGuid)
        {
            var player = Players.FirstOrDefault(p => p.Guid == playerGuid);
            if (player != null)
                MainThreadLuaCall($"SetLootMethod('master', '{player.Name}')");
        }



        public void PromoteAssistant(ulong playerGuid)
        {
            var player = Players.FirstOrDefault(p => p.Guid == playerGuid);
            if (player != null)
                MainThreadLuaCall($"PromoteToAssistant('{player.Name}')");
        }



        public void PromoteLeader(ulong playerGuid)
        {
            var player = Players.FirstOrDefault(p => p.Guid == playerGuid);
            if (player != null)
                MainThreadLuaCall($"PromoteToLeader('{player.Name}')");
        }



        public static void Stand() => MainThreadLuaCall("DoEmote(\"STAND\")");

        /// <summary>
        /// FG implementation: right-clicks the trainer NPC to open the trainer window,
        /// then uses Lua to enumerate and buy all available trainer services.
        /// Vanilla 1.12.1 GetTrainerServiceInfo(index) returns:
        ///   name, rank, category ("available"/"unavailable"/"used"), expanded.
        /// </summary>
        public async Task<int> LearnAllAvailableSpellsAsync(ulong trainerGuid, CancellationToken ct = default)
        {
            // Step 1: Find and right-click the trainer NPC
            var unit = Objects.FirstOrDefault(o => o.Guid == trainerGuid);
            if (unit is not Objects.WoWObject wowObj)
            {
                Log.Warning("[FG-TRAINER] Trainer GUID 0x{Guid:X} not found in object manager.", trainerGuid);
                return 0;
            }

            Log.Information("[FG-TRAINER] Opening trainer window (right-click on 0x{Guid:X})...", trainerGuid);
            ThreadSynchronizer.RunOnMainThread(() => wowObj.Interact());

            // Step 2: Wait for trainer window to open (poll GetNumTrainerServices).
            // Many trainers open a gossip menu first — detect and click through it.
            bool windowOpened = false;
            bool gossipHandled = false;
            for (int i = 0; i < 30 && !ct.IsCancellationRequested; i++)
            {
                await Task.Delay(150, ct);

                // Check if trainer window is already open
                var countResult = MainThreadLuaCallWithResult("{0} = GetNumTrainerServices()");
                if (countResult.Length > 0 && int.TryParse(countResult[0], out int n) && n > 0)
                {
                    windowOpened = true;
                    break;
                }

                // If gossip frame is visible, click the trainer gossip option to open trainer window.
                // Use DialogFrame (memory-based) to find the trainer-type option, falling back to Lua.
                if (!gossipHandled)
                {
                    var gossipVisible = MainThreadLuaCallWithResult(
                        "if GossipFrame and GossipFrame:IsVisible() then {0} = '1' else {0} = '0' end");
                    if (gossipVisible.Length > 0 && gossipVisible[0] == "1")
                    {
                        Log.Information("[FG-TRAINER] Gossip frame detected — selecting trainer option.");
                        ThreadSynchronizer.RunOnMainThread(() =>
                        {
                            var dialog = new Frames.DialogFrame();
                            if (dialog.DialogOptions.Any(d => d.Type == GameData.Core.Enums.DialogType.trainer))
                            {
                                dialog.SelectFirstGossipOfType(GameData.Core.Enums.DialogType.trainer);
                                Log.Information("[FG-TRAINER] Selected trainer gossip option via DialogFrame ({Count} options).", dialog.DialogOptions.Count);
                            }
                            else
                            {
                                // Fallback: click the first option (some trainers use generic gossip type)
                                Functions.LuaCall("SelectGossipOption(1)");
                                Log.Information("[FG-TRAINER] No trainer-type gossip found ({Count} options), clicked option 1.", dialog.DialogOptions.Count);
                            }
                        });
                        gossipHandled = true;
                    }
                }
            }

            if (!windowOpened)
            {
                Log.Warning("[FG-TRAINER] Trainer window did not open after 4.5s (gossipHandled={GossipHandled}).", gossipHandled);
                return 0;
            }

            // Step 3: Buy all "available" services using a single Lua script.
            // This runs one main-thread call that iterates all services and buys learnable ones.
            // Returns the count of services purchased.
            var result = MainThreadLuaCallWithResult(
                "local n = GetNumTrainerServices(); " +
                "local bought = 0; " +
                "for i = 1, n do " +
                "  local _, _, avail = GetTrainerServiceInfo(i); " +
                "  if avail == 'available' then " +
                "    BuyTrainerService(i); " +
                "    bought = bought + 1; " +
                "  end; " +
                "end; " +
                "{0} = bought");

            int learnedCount = 0;
            if (result.Length > 0)
                int.TryParse(result[0], out learnedCount);

            // Brief delay for server to process all the buy requests
            if (learnedCount > 0)
                await Task.Delay(500, ct);

            // Step 4: Close trainer window
            MainThreadLuaCall("CloseTrainer()");

            Log.Information("[FG-TRAINER] Bought {Count} services from trainer.", learnedCount);
            return learnedCount;
        }

        /// <summary>
        /// FG implementation: right-clicks a mailbox game object to open the mail UI,
        /// then uses Lua to enumerate mail and take all money/items.
        /// Vanilla 1.12.1 Lua API: GetInboxNumItems(), GetInboxHeaderInfo(index),
        /// TakeInboxMoney(index), TakeInboxItem(index), DeleteInboxItem(index).
        /// </summary>
        public async Task CollectAllMailAsync(ulong mailboxGuid, CancellationToken ct = default)
        {
            // Step 1: Find and right-click the mailbox game object
            var obj = Objects.FirstOrDefault(o => o.Guid == mailboxGuid);
            if (obj is not Objects.WoWObject wowObj)
            {
                Log.Warning("[FG-MAIL] Mailbox GUID 0x{Guid:X} not found in object manager.", mailboxGuid);
                return;
            }

            Log.Information("[FG-MAIL] Opening mailbox (right-click on 0x{Guid:X})...", mailboxGuid);
            ThreadSynchronizer.RunOnMainThread(() => wowObj.Interact());

            // Step 2: Wait for mail UI to open (poll GetInboxNumItems)
            bool windowOpened = false;
            for (int i = 0; i < 30 && !ct.IsCancellationRequested; i++)
            {
                await Task.Delay(150, ct);
                var countResult = MainThreadLuaCallWithResult("{0} = GetInboxNumItems()");
                if (countResult.Length > 0 && int.TryParse(countResult[0], out int n) && n >= 0)
                {
                    // GetInboxNumItems returns 0 when open with no mail, nil when closed
                    windowOpened = true;
                    break;
                }
            }

            if (!windowOpened)
            {
                Log.Warning("[FG-MAIL] Mail window did not open after 4.5s.");
                return;
            }

            // Step 3: Take all money and items from each mail
            // Iterate in reverse so index removal doesn't shift remaining indices
            var result = MainThreadLuaCallWithResult(
                "local n = GetInboxNumItems(); " +
                "local collected = 0; " +
                "for i = n, 1, -1 do " +
                "  local _, _, _, _, money, _, _, hasItem = GetInboxHeaderInfo(i); " +
                "  if money and money > 0 then TakeInboxMoney(i); collected = collected + 1; end; " +
                "  if hasItem then TakeInboxItem(i); collected = collected + 1; end; " +
                "end; " +
                "{0} = collected");

            int collectedCount = 0;
            if (result.Length > 0)
                int.TryParse(result[0], out collectedCount);

            if (collectedCount > 0)
                await Task.Delay(500, ct);

            // Step 4: Close mail window
            MainThreadLuaCall("CloseMail()");

            Log.Information("[FG-MAIL] Collected {Count} mail items/money from mailbox.", collectedCount);
        }

        public async Task AcceptQuestFromNpcAsync(ulong npcGuid, uint questId, CancellationToken ct = default)
        {
            await InteractWithNpcAsync(npcGuid, ct);

            for (int i = 0; i < 30 && !ct.IsCancellationRequested; i++)
            {
                await Task.Delay(150, ct);
                if (!_fgQuestFrame.IsOpen)
                    continue;

                _fgQuestFrame.AcceptQuest();

                await Task.Delay(150, ct);
                if (!_fgQuestFrame.IsOpen)
                    return;
            }

            Log.Warning("[FG-QUEST] Accept flow did not complete for quest {QuestId} at NPC 0x{Guid:X}.", questId, npcGuid);
        }

        public async Task TurnInQuestAsync(ulong npcGuid, uint questId, uint rewardIndex = 0, CancellationToken ct = default)
        {
            await InteractWithNpcAsync(npcGuid, ct);

            for (int i = 0; i < 30 && !ct.IsCancellationRequested; i++)
            {
                await Task.Delay(150, ct);
                if (!_fgQuestFrame.IsOpen)
                    continue;

                _fgQuestFrame.CompleteQuest((int)rewardIndex);

                await Task.Delay(150, ct);
                if (!_fgQuestFrame.IsOpen)
                    return;
            }

            Log.Warning("[FG-QUEST] Turn-in flow did not complete for quest {QuestId} at NPC 0x{Guid:X}.", questId, npcGuid);
        }

        public async Task<IReadOnlyList<uint>> DiscoverTaxiNodesAsync(ulong flightMasterGuid, CancellationToken ct = default)
        {
            await InteractWithNpcAsync(flightMasterGuid, ct);
            if (!await WaitForTaxiMapAsync(ct))
            {
                Log.Warning("[FG-TAXI] Taxi map did not open for flight master 0x{Guid:X}.", flightMasterGuid);
                return Array.Empty<uint>();
            }

            return _fgTaxiFrame.Nodes
                .Skip(1)
                .Where(node => _fgTaxiFrame.HasNodeUnlocked(node.NodeNumber))
                .Select(node => (uint)node.NodeNumber)
                .ToList();
        }

        public async Task<bool> ActivateFlightAsync(ulong flightMasterGuid, uint destinationNodeId, CancellationToken ct = default)
        {
            if (!_fgTaxiFrame.IsOpen)
            {
                await InteractWithNpcAsync(flightMasterGuid, ct);
                if (!await WaitForTaxiMapAsync(ct))
                {
                    Log.Warning("[FG-TAXI] Taxi map did not open for activate-flight vendor 0x{Guid:X}.", flightMasterGuid);
                    return false;
                }
            }

            if (!_fgTaxiFrame.HasNodeUnlocked((int)destinationNodeId))
            {
                Log.Warning("[FG-TAXI] Destination node {NodeId} is not unlocked in the current taxi map.", destinationNodeId);
                return false;
            }

            _fgTaxiFrame.SelectNode((int)destinationNodeId);
            await Task.Delay(150, ct);
            return true;
        }

        public async Task InteractWithNpcAsync(ulong npcGuid, CancellationToken ct = default)
        {
            var obj = Objects.FirstOrDefault(o => o.Guid == npcGuid);
            if (obj is not Objects.WoWObject wowObj)
            {
                Log.Warning("[FG-NPC] NPC GUID 0x{Guid:X} not found in object manager.", npcGuid);
                return;
            }

            _lastNpcInteractionGuid = npcGuid;
            ThreadSynchronizer.RunOnMainThread(() =>
            {
                Functions.SetTarget(npcGuid);
                wowObj.Interact();
            });
            await Task.Delay(150, ct);
        }

        public async Task BuyItemFromVendorAsync(ulong vendorGuid, uint itemId, uint quantity = 1, CancellationToken ct = default)
        {
            await InteractWithNpcAsync(vendorGuid, ct);
            if (!await WaitForMerchantWindowAsync(ct))
            {
                Log.Warning("[FG-VENDOR] Merchant window did not open for buy vendor 0x{Guid:X}.", vendorGuid);
                return;
            }

            int merchantSlot = ResolveMerchantSlot(itemId);
            if (merchantSlot <= 0)
            {
                Log.Warning("[FG-VENDOR] Item {ItemId} not found in merchant inventory for vendor 0x{Guid:X}.", itemId, vendorGuid);
                MainThreadLuaCall(VendorInteractionHelper.CloseMerchantLua);
                return;
            }

            MainThreadLuaCall(VendorInteractionHelper.BuildBuyMerchantItemLua(merchantSlot, quantity));
            await Task.Delay(150, ct);
            MainThreadLuaCall(VendorInteractionHelper.CloseMerchantLua);
        }

        public async Task SellItemToVendorAsync(ulong vendorGuid, byte bagId, byte slotId, uint quantity = 1, CancellationToken ct = default)
        {
            await InteractWithNpcAsync(vendorGuid, ct);
            if (!await WaitForMerchantWindowAsync(ct))
            {
                Log.Warning("[FG-VENDOR] Merchant window did not open for sell vendor 0x{Guid:X}.", vendorGuid);
                return;
            }

            ulong itemGuid = VendorInteractionHelper.ResolveSellItemGuid(
                bagId,
                slotId,
                GetContainedItems().Select(item => item.Guid).ToList(),
                (resolvedBagId, resolvedSlotId) => GetContainedItem(resolvedBagId, resolvedSlotId)?.Guid ?? 0);

            if (itemGuid == 0)
            {
                Log.Warning("[FG-VENDOR] Could not resolve item GUID for bag={BagId} slot={SlotId}.", bagId, slotId);
                MainThreadLuaCall(VendorInteractionHelper.CloseMerchantLua);
                return;
            }

            ThreadSynchronizer.RunOnMainThread(() =>
                Functions.SellItemByGuid(
                    VendorInteractionHelper.NormalizeQuantity(quantity),
                    vendorGuid,
                    itemGuid));

            await Task.Delay(150, ct);
            MainThreadLuaCall(VendorInteractionHelper.CloseMerchantLua);
        }

        public async Task RepairAllItemsAsync(ulong vendorGuid, CancellationToken ct = default)
        {
            await InteractWithNpcAsync(vendorGuid, ct);
            if (!await WaitForMerchantWindowAsync(ct))
            {
                Log.Warning("[FG-VENDOR] Merchant window did not open for repair vendor 0x{Guid:X}.", vendorGuid);
                return;
            }

            MainThreadLuaCall(VendorInteractionHelper.RepairAllLua);
            await Task.Delay(150, ct);
            MainThreadLuaCall(VendorInteractionHelper.CloseMerchantLua);
        }

        private async Task<bool> WaitForMerchantWindowAsync(CancellationToken ct)
        {
            for (int i = 0; i < 30 && !ct.IsCancellationRequested; i++)
            {
                await Task.Delay(150, ct);
                var result = MainThreadLuaCallWithResult(VendorInteractionHelper.MerchantOpenProbeLua);
                if (result.Length > 0 && result[0] == "1")
                    return true;
            }

            return false;
        }

        private static int ResolveMerchantSlot(uint itemId)
        {
            var result = MainThreadLuaCallWithResult(VendorInteractionHelper.BuildResolveMerchantSlotLua(itemId));
            if (result.Length == 0)
                return 0;

            return int.TryParse(result[0], out int merchantSlot) ? merchantSlot : 0;
        }

        private async Task<bool> WaitForTaxiMapAsync(CancellationToken ct)
        {
            for (int i = 0; i < 30 && !ct.IsCancellationRequested; i++)
            {
                await Task.Delay(150, ct);
                if (_fgTaxiFrame.IsOpen)
                    return true;
            }

            return false;
        }
    }
}
