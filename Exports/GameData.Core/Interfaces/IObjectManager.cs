using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GameData.Core.Interfaces
{
    public interface IObjectManager
    {
        IWoWEventHandler EventHandler { get; }
        ILoginScreen LoginScreen { get; }
        IRealmSelectScreen RealmSelectScreen { get; }
        ICharacterSelectScreen CharacterSelectScreen { get; }
        HighGuid PlayerGuid { get; }
        string ZoneText { get; }
        string MinimapZoneText { get; }
        string ServerName { get; }
        IGossipFrame GossipFrame { get; }
        ILootFrame LootFrame { get; }
        IMerchantFrame MerchantFrame { get; }
        ICraftFrame CraftFrame { get; }
        IQuestFrame QuestFrame { get; }
        IQuestGreetingFrame QuestGreetingFrame { get; }
        ITaxiFrame TaxiFrame { get; }
        ITradeFrame TradeFrame { get; }
        ITrainerFrame TrainerFrame { get; }
        ITalentFrame TalentFrame { get; }
        IWoWLocalPlayer Player { get; }
        IWoWLocalPet Pet { get; }
        IEnumerable<IWoWObject> Objects { get; }
        IWoWPlayer PartyLeader { get; }
        ulong PartyLeaderGuid { get; }
        ulong Party1Guid { get; }
        ulong Party2Guid { get; }
        ulong Party3Guid { get; }
        ulong Party4Guid { get; }
        ulong StarTargetGuid { get; }
        ulong CircleTargetGuid { get; }
        ulong DiamondTargetGuid { get; }
        ulong TriangleTargetGuid { get; }
        ulong MoonTargetGuid { get; }
        ulong SquareTargetGuid { get; }
        ulong CrossTargetGuid { get; }
        ulong SkullTargetGuid { get; }
        void AntiAfk();
        string GlueDialogText { get; }
        LoginStates LoginState { get; }
        bool HasEnteredWorld { get; }
#if NET8_0_OR_GREATER
        public void Face(Position pos)
        {
            if (pos == null) return;
            if (Player.Facing < 0)
            {
                SetFacing((float)(Math.PI * 2) + Player.Facing);
                return;
            }
            if (!Player.IsFacing(pos))
                SetFacing(Player.GetFacingForPosition(pos));
        }
        public void MoveToward(Position pos)
        {
            Face(pos);
            StartMovement(ControlBits.Front);
        }
        public void Turn180()
        {
            var newFacing = Player.Facing + Math.PI;
            if (newFacing > Math.PI * 2)
                newFacing -= Math.PI * 2;
            SetFacing((float)newFacing);
        }
        public void StopAllMovement()
        {
            var bits = ControlBits.Front | ControlBits.Back | ControlBits.Left | ControlBits.Right | ControlBits.StrafeLeft | ControlBits.StrafeRight;
            StopMovement(bits);
        }
        /// <summary>
        /// Clears all movement flags AND immediately sends MSG_MOVE_STOP to the server.
        /// Use before actions that require the player to be stationary (e.g., CMSG_GAMEOBJ_USE).
        /// Default implementation just calls StopAllMovement(); WoWSharpClient overrides to also send the packet.
        /// </summary>
        public void ForceStopImmediate() => StopAllMovement();
        public IEnumerable<IWoWGameObject> GameObjects => Objects.OfType<IWoWGameObject>();
        public IEnumerable<IWoWUnit> Units => Objects.OfType<IWoWUnit>();
        public IEnumerable<IWoWPlayer> Players => Objects.OfType<IWoWPlayer>();
        public IEnumerable<IWoWItem> Items => Objects.OfType<IWoWItem>();
        public IEnumerable<IWoWContainer> Containers => Objects.OfType<IWoWContainer>();
        public IEnumerable<IWoWUnit> CasterAggressors => Objects.OfType<IWoWUnit>();
        public IEnumerable<IWoWUnit> MeleeAggressors => Objects.OfType<IWoWUnit>();
        public IEnumerable<IWoWUnit> Aggressors => Objects.OfType<IWoWUnit>();
        public IEnumerable<IWoWUnit> Hostiles => Objects.OfType<IWoWUnit>();
        public IEnumerable<IWoWPlayer> PartyMembers
        {
            get
            {
                var partyMembers = new List<IWoWPlayer>() { Player };
                var partyMember1 = (IWoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party1Guid);
                if (partyMember1 != null) partyMembers.Add(partyMember1);
                var partyMember2 = (IWoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party2Guid);
                if (partyMember2 != null) partyMembers.Add(partyMember2);
                var partyMember3 = (IWoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party3Guid);
                if (partyMember3 != null) partyMembers.Add(partyMember3);
                var partyMember4 = (IWoWPlayer)Objects.FirstOrDefault(p => p.Guid == Party4Guid);
                if (partyMember4 != null) partyMembers.Add(partyMember4);
                return partyMembers;
            }
        }
#else
        void Face(Position pos);
        void MoveToward(Position pos);
        void Turn180();
        void StopAllMovement();
        IEnumerable<IWoWGameObject> GameObjects { get; }
        IEnumerable<IWoWUnit> Units { get; }
        IEnumerable<IWoWPlayer> Players { get; }
        IEnumerable<IWoWItem> Items { get; }
        IEnumerable<IWoWContainer> Containers { get; }
        IEnumerable<IWoWUnit> CasterAggressors { get; }
        IEnumerable<IWoWUnit> MeleeAggressors { get; }
        IEnumerable<IWoWUnit> Aggressors { get; }
        IEnumerable<IWoWUnit> Hostiles { get; }
        IEnumerable<IWoWPlayer> PartyMembers { get; }
#endif
        IWoWUnit GetTarget(IWoWUnit woWUnit);
        sbyte GetTalentRank(uint tabIndex, uint talentIndex);
        void PickupInventoryItem(uint inventorySlot);
        void DeleteCursorItem();
        void EquipCursorItem();
        void ConfirmItemEquip();
        void SendChatMessage(string chatMessage);
        void SetRaidTarget(IWoWUnit target, TargetMarker v);
        void JoinBattleGroundQueue();
        void ResetInstances();
        void PickupMacro(uint v);
        void PlaceAction(uint v);
        void InviteToGroup(ulong guid);
        void InviteByName(string characterName);
        void KickPlayer(ulong guid);
        void AcceptGroupInvite();
        void DeclineGroupInvite();
        void LeaveGroup();
        void DisbandGroup();
        void ConvertToRaid();
        bool HasPendingGroupInvite();
        bool HasLootRollWindow(int itemId);
        void LootPass(int itemId);
        void LootRollGreed(int itemId);
        void LootRollNeed(int itemId);
        void AssignLoot(int itemId, ulong playerGuid);
        void SetGroupLoot(GroupLootSetting setting);
        void PromoteLootManager(ulong playerGuid);
        void PromoteAssistant(ulong playerGuid);
        void PromoteLeader(ulong playerGuid);
        void DoEmote(Emote emote);
        void DoEmote(TextEmote emote);
        uint GetManaCost(string healingTouch);
        void MoveToward(Position position, float facing);
        void SetNavigationPath(Position[] path) { } // default no-op for foreground
        void RefreshSkills();
        void RefreshSpells();
        void ReleaseSpirit();
        void RetrieveCorpse();
        void SetTarget(ulong guid);
        void StopAttack();
        void StartMeleeAttack();
        void StartRangedAttack();
        void SetFacing(float facing);
        void StartMovement(ControlBits bits);
        void StopMovement(ControlBits bits);
        bool IsSpellReady(string spellName);
        void StopCasting();
        void CastSpell(string spellName, int rank = -1, bool castOnSelf = false);
        void CastSpell(int spellId, int rank = -1, bool castOnSelf = false);
        void CastSpellOnGameObject(int spellId, ulong gameObjectGuid);
        void InteractWithGameObject(ulong gameObjectGuid);
        bool CanCastSpell(int spellId, ulong targetGuid);
        IReadOnlyCollection<uint> KnownSpellIds { get; }
        void UseItem(int bagId, int slotId, ulong targetGuid = 0);
        ulong GetBackpackItemGuid(int parSlot);
        ulong GetEquippedItemGuid(EquipSlot slot);
        IWoWItem GetEquippedItem(EquipSlot ranged);
        IWoWItem GetContainedItem(int bagSlot, int slotId);
        IEnumerable<IWoWItem> GetEquippedItems();
        IEnumerable<IWoWItem> GetContainedItems();
        uint GetBagGuid(EquipSlot equipSlot);
        void PickupContainedItem(int bagSlot, int slotId, int quantity);
        void PlaceItemInContainer(int bagSlot, int slotId);
        void DestroyItemInContainer(int bagSlot, int slotId, int quantity = -1);
        void Logout();
        void SplitStack(int bag, int slot, int quantity, int destinationBag, int destinationSlot);
        void EquipItem(int bagSlot, int slotId, EquipSlot? equipSlot = null);
        void UnequipItem(EquipSlot slot);
        void AcceptResurrect();
        void EnterWorld(ulong characterGuid);

        // Inventory helpers
        int CountFreeSlots(bool countSpecialSlots = false);
        uint GetItemCount(uint itemId);
#if NET8_0_OR_GREATER
        IWoWItem GetItem(int bag, int slot) => GetContainedItem(bag, slot);
        void UseContainerItem(int bag, int slot) => UseItem(bag, slot);
#else
        IWoWItem GetItem(int bag, int slot);
        void UseContainerItem(int bag, int slot);
#endif

        // Wand helpers
        void StartWandAttack();
        void StopWandAttack();

        // Looting — FG: right-click interact, BG: CMSG_LOOT packet via AgentFactory
#if NET8_0_OR_GREATER
        public Task LootTargetAsync(ulong targetGuid, CancellationToken ct = default) => Task.CompletedTask;
#else
        Task LootTargetAsync(ulong targetGuid, CancellationToken ct = default);
#endif

        // Vendor — FG: Lua NPC interaction, BG: packet-based via AgentFactory
        // Opens vendor, sells junk, repairs, optionally buys items, closes.
#if NET8_0_OR_GREATER
        public Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, CancellationToken ct = default) => Task.CompletedTask;
#else
        Task QuickVendorVisitAsync(ulong vendorGuid, Dictionary<uint, uint>? itemsToBuy = null, CancellationToken ct = default);
#endif

        // Mail collection — FG: Lua mailbox interaction, BG: packet-based via AgentFactory
#if NET8_0_OR_GREATER
        public Task CollectAllMailAsync(ulong mailboxGuid, CancellationToken ct = default) => Task.CompletedTask;
#else
        Task CollectAllMailAsync(ulong mailboxGuid, CancellationToken ct = default);
#endif

        // Trainer — FG: Lua trainer interaction, BG: packet-based via AgentFactory
        // Opens trainer, learns all affordable spells, closes.
#if NET8_0_OR_GREATER
        public Task<int> LearnAllAvailableSpellsAsync(ulong trainerGuid, CancellationToken ct = default) => Task.FromResult(0);
#else
        Task<int> LearnAllAvailableSpellsAsync(ulong trainerGuid, CancellationToken ct = default);
#endif

        // Flight master — FG: Lua taxi interaction, BG: packet-based via AgentFactory
#if NET8_0_OR_GREATER
        public Task<IReadOnlyList<uint>> DiscoverTaxiNodesAsync(ulong flightMasterGuid, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<uint>>(Array.Empty<uint>());
        public Task<bool> ActivateFlightAsync(ulong flightMasterGuid, uint destinationNodeId, CancellationToken ct = default) => Task.FromResult(false);
#else
        Task<IReadOnlyList<uint>> DiscoverTaxiNodesAsync(ulong flightMasterGuid, CancellationToken ct = default);
        Task<bool> ActivateFlightAsync(ulong flightMasterGuid, uint destinationNodeId, CancellationToken ct = default);
#endif

        // Banking — FG: Lua banker interaction, BG: packet-based via AgentFactory
#if NET8_0_OR_GREATER
        public Task DepositExcessItemsAsync(ulong bankerGuid, CancellationToken ct = default) => Task.CompletedTask;
#else
        Task DepositExcessItemsAsync(ulong bankerGuid, CancellationToken ct = default);
#endif

        // Auction house — FG: Lua AH interaction, BG: packet-based via AgentFactory
#if NET8_0_OR_GREATER
        public Task PostAuctionItemsAsync(ulong auctioneerGuid, CancellationToken ct = default) => Task.CompletedTask;
#else
        Task PostAuctionItemsAsync(ulong auctioneerGuid, CancellationToken ct = default);
#endif

        // Crafting — FG: Lua craft window, BG: spell casting via AgentFactory
#if NET8_0_OR_GREATER
        public Task CraftAvailableRecipesAsync(CancellationToken ct = default) => Task.CompletedTask;
#else
        Task CraftAvailableRecipesAsync(CancellationToken ct = default);
#endif
    }
}
