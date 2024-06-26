﻿using RaidMemberBot.Game;
using RaidMemberBot.Game.Statics;
using RaidMemberBot.Objects;
using System.Collections.Generic;
using System.Linq;
using static RaidMemberBot.Constants.Enums;

namespace RaidMemberBot.AI.SharedStates
{
    public class EquipArmorTask : BotTask, IBotTask
    {
        static readonly IDictionary<Class, ItemClass> desiredArmorTypes = new Dictionary<Class, ItemClass>
        {
            { Class.Druid, ItemClass.Leather },
            { Class.Hunter, ItemClass.Mail },
            { Class.Mage, ItemClass.Cloth },
            { Class.Paladin, ItemClass.Mail },
            { Class.Priest, ItemClass.Cloth },
            { Class.Rogue, ItemClass.Leather },
            { Class.Shaman, ItemClass.Leather },
            { Class.Warlock, ItemClass.Cloth },
            { Class.Warrior, ItemClass.Mail }
        };

        readonly IList<EquipSlot> slotsToCheck = new List<EquipSlot>
        {
            EquipSlot.Back,
            EquipSlot.Chest,
            EquipSlot.Feet,
            EquipSlot.Hands,
            EquipSlot.Head,
            EquipSlot.Legs,
            EquipSlot.Shoulders,
            EquipSlot.Waist,
            EquipSlot.Wrist
        };

        readonly LocalPlayer player;

        EquipSlot? emptySlot;
        WoWItem itemToEquip;

        public EquipArmorTask(IClassContainer container, Stack<IBotTask> botTasks) : base(container, botTasks, TaskType.Ordinary) { }

        public void Update()
        {
            if (ObjectManager.Player.IsInCombat)
            {
                BotTasks.Pop();
                return;
            }

            if (itemToEquip == null)
            {
                foreach (EquipSlot slot in slotsToCheck)
                {
                    WoWItem equippedItem = ObjectManager.Items.First(x => x.Guid == ObjectManager.Player.GetEquippedItemGuid(slot));
                    if (equippedItem == null)
                    {
                        emptySlot = slot;
                        break;
                    }
                }

                if (emptySlot != null)
                {
                    slotsToCheck.Remove(emptySlot.Value);

                    itemToEquip = ObjectManager.Items
                        .FirstOrDefault(i =>
                            //(i.Info.ItemSubclass == desiredArmorTypes[ObjectManager.Player.Class] || i.Info.ItemClass == ItemClass.Cloth && i.Info.EquipSlot == EquipSlot.Back) &&
                            //i.Info.EquipSlot.ToString() == emptySlot.ToString() &&
                            i.Info.RequiredLevel <= ObjectManager.Player.Level
                        );

                    if (itemToEquip == null)
                        emptySlot = null;
                }
                else
                    slotsToCheck.Clear();
            }

            if (itemToEquip == null && slotsToCheck.Count == 0)
            {
                BotTasks.Pop();
                return;
            }

            if (itemToEquip != null && Wait.For("EquipItemDelay", 500))
            {
                //var bagId = Inventory.GetBagId(itemToEquip.Guid);
                //var slotId = Inventory.GetSlotId(itemToEquip.Guid);

                //Functions.LuaCall($"UseContainerItem({bagId}, {slotId})");
                //if ((int)itemToEquip.Quality > 1)
                //    Functions.LuaCall("EquipPendingItem(0)");
                //emptySlot = null;
                //itemToEquip = null;
            }
        }
    }
}
