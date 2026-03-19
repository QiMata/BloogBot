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

        private const int OBJECT_TYPE_OFFSET = 0x14;

        // Vanilla 1.12.1 callback signature: int __thiscall callback(int filter, ulong guid)
        // ThisCall convention: filter comes first, guid second (opposite of non-Vanilla clients)
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int EnumerateVisibleObjectsCallbackVanilla(int filter, ulong guid);

        private readonly EnumerateVisibleObjectsCallbackVanilla CallbackDelegate;

        private readonly nint callbackPtr;


        public IEnumerable<IWoWObject> Objects
        {
            get
            {
                lock (_objectsLock)
                {
                    return [.. ObjectsBuffer.Cast<IWoWObject>()]; // safe snapshot
                }
            }
        }


        internal IList<WoWObject> ObjectsBuffer = [];



        private readonly object _objectsLock = new();


        // These filter from ObjectsBuffer (which is populated by EnumerateVisibleObjects callback)


        // These filter from ObjectsBuffer (which is populated by EnumerateVisibleObjects callback)
        public IEnumerable<IWoWGameObject> GameObjects => Objects.OfType<IWoWGameObject>();


        public IEnumerable<IWoWUnit> Units => Objects.OfType<IWoWUnit>();


        public IEnumerable<IWoWPlayer> Players => Objects.OfType<IWoWPlayer>();


        public IEnumerable<IWoWItem> Items => Objects.OfType<IWoWItem>();


        public IEnumerable<IWoWContainer> Containers => Objects.OfType<IWoWContainer>();

        /// <summary>
        /// Walk the WoW object manager linked list in memory to find an object by GUID.
        /// Does NOT require ThreadSynchronizer — pure memory reads, safe from any thread.
        /// Used as a fallback when Functions.GetObjectPtr() via ThreadSynchronizer fails
        /// (e.g., during world entry when WoW's message loop isn't processing WM_USER yet).
        /// </summary>

        /// <summary>
        /// Walk the WoW object manager linked list in memory to find an object by GUID.
        /// Does NOT require ThreadSynchronizer — pure memory reads, safe from any thread.
        /// Used as a fallback when Functions.GetObjectPtr() via ThreadSynchronizer fails
        /// (e.g., during world entry when WoW's message loop isn't processing WM_USER yet).
        /// </summary>
        private nint GetObjectPtrFromMemory(ulong targetGuid)
        {
            try
            {
                var managerPtr = MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase);
                if (managerPtr == nint.Zero) return nint.Zero;

                var currentObj = MemoryManager.ReadIntPtr(nint.Add(managerPtr, (int)Offsets.ObjectManager.FirstObj));
                int maxIterations = 5000; // Safety guard against infinite loop

                while (currentObj != nint.Zero && maxIterations-- > 0)
                {
                    // Check if this looks like a valid pointer (> 0x10000, even aligned)
                    if ((long)currentObj < 0x10000 || ((long)currentObj & 1) != 0)
                        break;

                    ulong objGuid = MemoryManager.ReadUlong(nint.Add(currentObj, (int)Offsets.ObjectManager.CurObjGuid));
                    if (objGuid == targetGuid)
                        return currentObj;

                    currentObj = MemoryManager.ReadIntPtr(nint.Add(currentObj, (int)Offsets.ObjectManager.NextObj));
                }

                return nint.Zero;
            }
            catch (Exception ex)
            {
                DiagLog($"GetObjectPtrFromMemory EXCEPTION: {ex.Message}");
                return nint.Zero;
            }
        }

        // Counter for IsLoggedIn calls to limit logging


        // Counter for IsLoggedIn calls to limit logging



        private void EnumerateVisibleObjects()
        {
            ThreadSynchronizer.RunOnMainThread(() =>
            {
                try
                {
                    // Double-check safety: if a cross-map transfer started between the
                    // SimplePolling guard check and this delegate executing on the main thread,
                    // bail out immediately to avoid ACCESS_VIOLATION during object teardown.
                    if (PauseDuringTeleport || IsContinentTransition)
                    {
                        return;
                    }

                    if (!IsLoggedIn)
                    {
                        return;
                    }
                    // Use memory read instead of Functions.GetPlayerGuid() which returns an index
                    ulong playerGuid = GetPlayerGuidFromMemory();
                    byte[] playerGuidParts = BitConverter.GetBytes(playerGuid);
                    PlayerGuid = new HighGuid(playerGuidParts[0..4], playerGuidParts[4..8]);

                    if (PlayerGuid.FullGuid == 0)
                    {
                        Player = null;
                        return;
                    }
                    var playerObject = Functions.GetObjectPtr(PlayerGuid.FullGuid);
                    if (playerObject == nint.Zero)
                    {
                        // Memory fallback: walk the object manager linked list directly
                        playerObject = GetObjectPtrFromMemory(PlayerGuid.FullGuid);
                    }
                    if (playerObject == nint.Zero)
                    {
                        // Truly can't find player — null it out
                        Player = null;
                        return;
                    }

                    lock (_objectsLock)
                    {
                        ObjectsBuffer.Clear();
                        if (!Functions.EnumerateVisibleObjects(callbackPtr, 0))
                        {
                            // SEH caught an ACCESS_VIOLATION — zone boundary cache reset in progress.
                            // Discard partial results to avoid stale pointers.
                            ObjectsBuffer.Clear();
                            Player = null;
                            Log.Warning("[OBJECT MANAGER] EnumerateVisibleObjects aborted (zone boundary cache reset) — skipping this frame");
                            return;
                        }
                    }

                    if (Player != null)
                    {
                        try
                        {
                            var petFound = false;

                            foreach (var unit in Units)
                            {
                                if (unit.SummonedByGuid == Player?.Guid)
                                {
                                    Pet = new LocalPet(((WoWObject)unit).Pointer, unit.HighGuid, unit.ObjectType);
                                    petFound = true;
                                }
                            }

                            if (!petFound)
                                Pet = null;

                            // Skip Lua-heavy spell/skill refresh during ghost form.
                            // RefreshSpells makes multiple Lua calls (GetSpellTabInfo, GetTalentInfo)
                            // that can crash WoW.exe when internal state is transitional after death.
                            // Ghost form only needs object enumeration for corpse-run navigation —
                            // spell/skill data is irrelevant until resurrection.
                            var isGhost = MemoryManager.ReadInt(Offsets.Player.IsGhost) != 0;
                            if (!isGhost)
                            {
                                RefreshSpells();
                                RefreshSkills();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[OBJECT MANAGER] Post-enum error: {ex.Message}");
                        }
                    }

                    UpdateProbe();
                }
                catch (Exception ex)
                {
                    Log.Error($"[OBJECT MANAGER] EnumerateVisibleObjects error: {ex.Message}");
                }
            });
        }

        // EnumerateVisibleObjects callback for Vanilla 1.12.1: ThisCall with (filter, guid)
        // Parameter order is swapped compared to non-Vanilla clients

        // EnumerateVisibleObjects callback for Vanilla 1.12.1: ThisCall with (filter, guid)
        // Parameter order is swapped compared to non-Vanilla clients
        private int CallbackVanilla(int filter, ulong guid)
        {
            return CallbackInternal(guid, filter);  // Swap back to (guid, filter) for internal use
        }



        private int CallbackInternal(ulong guid, int filter)
        {
            try
            {
                if (guid == 0)
                {
                    return 0;
                }

                var pointer = Functions.GetObjectPtr(guid);

                if (pointer == nint.Zero)
                {
                    return 1; // Continue enumeration
                }

                var objectType = (WoWObjectType)MemoryManager.ReadInt(nint.Add(pointer, OBJECT_TYPE_OFFSET));

                byte[] guidParts = BitConverter.GetBytes(guid);
                // Note: On private servers, low GUIDs like 5 are perfectly valid
                HighGuid highGuid = new(guidParts[0..4], guidParts[4..8]);  // Fixed: was [0..3], now [0..4] for 4 bytes

                switch (objectType)
                {
                    case WoWObjectType.Container:
                        ObjectsBuffer.Add(new WoWContainer(pointer, highGuid, objectType));
                        break;
                    case WoWObjectType.Item:
                        ObjectsBuffer.Add(new WoWItem(pointer, highGuid, objectType));
                        break;
                    case WoWObjectType.Player:
                        // Compare by GUID (more reliable than pointer comparison when GetObjectPtr fails)
                        var isLocalPlayer = (guid == PlayerGuid.FullGuid);
                        if (isLocalPlayer)
                        {
                            var player = new LocalPlayer(pointer, highGuid, objectType);
                            Player = player;
                            ObjectsBuffer.Add(player);
                        }
                        else
                        {
                            ObjectsBuffer.Add(new WoWPlayer(pointer, highGuid, objectType));
                        }
                        break;
                    case WoWObjectType.GameObj:
                        ObjectsBuffer.Add(new WoWGameObject(pointer, highGuid, objectType));
                        break;
                    case WoWObjectType.Unit:
                        ObjectsBuffer.Add(new WoWUnit(pointer, highGuid, objectType));
                        break;
                }

                return 1;
            }
            catch (Exception e)
            {
                Log.Error($"OBJECT MANAGER: CallbackInternal => {e.Message} {e.StackTrace}");
                return 1; // Continue enumeration even on error
            }
        }



        private void UpdateProbe()
        {
            try
            {
                if (IsLoggedIn && Player != null)
                {
                    // Track if this is our first time entering the world (to fire event)
                    bool justEnteredWorld = !HasEnteredWorld;

                    // Mark that we've successfully entered the world
                    if (justEnteredWorld)
                    {
                        HasEnteredWorld = true;
                    }

                    // Update snapshot with character info for StateManager
                    var playerName = Player.Name ?? "";

                    // Lua fallback: Name cache may not be populated on first login
                    if (string.IsNullOrEmpty(playerName))
                    {
                        try
                        {
                            var result = MainThreadLuaCallWithResult("{0} = UnitName('player')");
                            if (result != null && result.Length > 0 && !string.IsNullOrEmpty(result[0]))
                            {
                                playerName = result[0];
                            }
                        }
                        catch
                        {
                            // Lua name fallback failed, continue with empty name
                        }
                    }

                    _characterState.CharacterName = playerName;
                    _characterState.Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    // Fire event when first entering world (AFTER setting character name in snapshot)
                    // This allows subscribers to send the snapshot immediately while we're definitely in-world
                    if (justEnteredWorld)
                    {
                        try
                        {
                            OnEnteredWorld?.Invoke(this, EventArgs.Empty);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[OBJECT MANAGER] OnEnteredWorld event handler error: {ex.Message}");
                        }
                    }

                    //_characterState.Guid = playerGuid;
                    //_characterState.Zone = MinimapZoneText;
                    //_characterState.InParty = int.Parse(MainThreadLuaCallWithResult("{0} = GetNumPartyMembers()")[0]) > 0;
                    //_characterState.InRaid = int.Parse(MainThreadLuaCallWithResult("{0} = GetNumRaidMembers()")[0]) > 0;
                    //_characterState.MapId = (int)MapId;
                    //_characterState.Race = Enum.GetValues(typeof(Race)).Cast<Race>().Where(x => x.GetDescription() == Player.Race).First();
                    //_characterState.Facing = Player.Facing;
                    //_characterState.Position = new Vector3(Player.Position.X, Player.Position.Y, Player.Position.Z);

                    IWoWItem headItem = GetEquippedItem(EquipSlot.Head);
                    IWoWItem neckItem = GetEquippedItem(EquipSlot.Neck);
                    IWoWItem shoulderItem = GetEquippedItem(EquipSlot.Shoulders);
                    IWoWItem backItem = GetEquippedItem(EquipSlot.Back);
                    IWoWItem chestItem = GetEquippedItem(EquipSlot.Chest);
                    IWoWItem shirtItem = GetEquippedItem(EquipSlot.Shirt);
                    IWoWItem tabardItem = GetEquippedItem(EquipSlot.Tabard);
                    IWoWItem wristItem = GetEquippedItem(EquipSlot.Wrist);
                    IWoWItem handsItem = GetEquippedItem(EquipSlot.Hands);
                    IWoWItem waistItem = GetEquippedItem(EquipSlot.Waist);
                    IWoWItem legsItem = GetEquippedItem(EquipSlot.Legs);
                    IWoWItem feetItem = GetEquippedItem(EquipSlot.Feet);
                    IWoWItem finger1Item = GetEquippedItem(EquipSlot.Finger1);
                    IWoWItem finger2Item = GetEquippedItem(EquipSlot.Finger2);
                    IWoWItem trinket1Item = GetEquippedItem(EquipSlot.Trinket1);
                    IWoWItem trinket2Item = GetEquippedItem(EquipSlot.Trinket2);
                    IWoWItem mainHandItem = GetEquippedItem(EquipSlot.MainHand);
                    IWoWItem offHandItem = GetEquippedItem(EquipSlot.OffHand);
                    IWoWItem rangedItem = GetEquippedItem(EquipSlot.Ranged);

                    //if (headItem != null)
                    //{
                    //    _characterState.HeadItem = headItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.HeadItem = 0;
                    //}
                    //if (neckItem != null)
                    //{
                    //    _characterState.NeckItem = neckItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.NeckItem = 0;
                    //}
                    //if (shoulderItem != null)
                    //{
                    //    _characterState.ShoulderItem = shoulderItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.ShoulderItem = 0;
                    //}
                    //if (backItem != null)
                    //{
                    //    _characterState.BackItem = backItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.BackItem = 0;
                    //}
                    //if (chestItem != null)
                    //{
                    //    _characterState.ChestItem = chestItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.ChestItem = 0;
                    //}
                    //if (shirtItem != null)
                    //{
                    //    _characterState.ShirtItem = shirtItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.ShirtItem = 0;
                    //}
                    //if (tabardItem != null)
                    //{
                    //    _characterState.TabardItem = tabardItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.TabardItem = 0;
                    //}
                    //if (wristItem != null)
                    //{
                    //    _characterState.WristsItem = wristItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.WristsItem = 0;
                    //}
                    //if (handsItem != null)
                    //{
                    //    _characterState.HandsItem = handsItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.HandsItem = 0;
                    //}
                    //if (waistItem != null)
                    //{
                    //    _characterState.WaistItem = waistItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.WaistItem = 0;
                    //}
                    //if (legsItem != null)
                    //{
                    //    _characterState.LegsItem = legsItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.LegsItem = 0;
                    //}
                    //if (feetItem != null)
                    //{
                    //    _characterState.FeetItem = feetItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.FeetItem = 0;
                    //}
                    //if (finger1Item != null)
                    //{
                    //    _characterState.Finger1Item = finger1Item.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.Finger1Item = 0;
                    //}
                    //if (finger2Item != null)
                    //{
                    //    _characterState.Finger2Item = finger2Item.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.Finger2Item = 0;
                    //}
                    //if (trinket1Item != null)
                    //{
                    //    _characterState.Trinket1Item = trinket1Item.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.Trinket1Item = 0;
                    //}
                    //if (trinket2Item != null)
                    //{
                    //    _characterState.Trinket2Item = trinket2Item.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.Trinket2Item = 0;
                    //}
                    //if (mainHandItem != null)
                    //{
                    //    _characterState.MainHandItem = mainHandItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.MainHandItem = 0;
                    //}
                    //if (offHandItem != null)
                    //{
                    //    _characterState.OffHandItem = offHandItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.OffHandItem = 0;
                    //}
                    //if (rangedItem != null)
                    //{
                    //    _characterState.RangedItem = rangedItem.ItemId;
                    //}
                    //else
                    //{
                    //    _characterState.RangedItem = 0;
                    //}
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[OBJECT MANAGER]{ex.Message} {ex.StackTrace}");
            }
        }
    }
}
