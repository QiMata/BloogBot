using Database;
using Google.Protobuf.WellKnownTypes;
using MySql.Data.MySqlClient;
using MySql.Data.Types;
using System.Data;
using Serilog;
using System.Collections.Generic;
using System;

namespace DecisionEngineService.Repository
{
    public partial class MangosRepository
    {
        public static List<ForbiddenItem> GetForbiddenItems()
        {
            List<ForbiddenItem> forbiddenItems = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM forbidden_items";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var forbiddenItem = new ForbiddenItem
                        {
                            Entry = reader.GetUInt32("entry"),
                            Patch = reader.GetByte("patch"),
                            AfterOrBefore = reader.GetByte("AfterOrBefore")
                        };

                        forbiddenItems.Add(forbiddenItem);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return forbiddenItems;
        }

        public static List<ItemDisplayInfo> GetItemDisplayInfo()
        {
            List<ItemDisplayInfo> itemDisplayInfos = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM item_display_info";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var itemDisplayInfo = new ItemDisplayInfo
                        {
                            Field0 = reader.GetInt32("field0"),
                            Field5 = reader.IsDBNull(reader.GetOrdinal("field5")) ? string.Empty : reader.GetString("field5")
                        };

                        itemDisplayInfos.Add(itemDisplayInfo);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return itemDisplayInfos;
        }

        public static List<ItemEnchantmentTemplate> GetItemEnchantmentTemplates()
        {
            List<ItemEnchantmentTemplate> enchantmentTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM item_enchantment_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var enchantmentTemplate = new ItemEnchantmentTemplate
                        {
                            Entry = reader.GetUInt32("entry"),
                            Ench = reader.GetUInt32("ench"),
                            Chance = reader.GetFloat("chance")
                        };

                        enchantmentTemplates.Add(enchantmentTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return enchantmentTemplates;
        }

        public static List<ItemLootTemplate> GetItemLootTemplates()
        {
            List<ItemLootTemplate> lootTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM item_loot_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var lootTemplate = new ItemLootTemplate
                        {
                            Entry = reader.GetUInt32("entry"),
                            Item = reader.GetUInt32("item"),
                            ChanceOrQuestChance = reader.GetFloat("ChanceOrQuestChance"),
                            Groupid = reader.GetByte("groupid"),
                            MincountOrRef = reader.GetInt32("mincountOrRef"),
                            Maxcount = reader.GetByte("maxcount"),
                            ConditionId = reader.GetUInt32("condition_id"),
                            PatchMin = reader.GetByte("patch_min"),
                            PatchMax = reader.GetByte("patch_max")
                        };

                        lootTemplates.Add(lootTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return lootTemplates;
        }

        public static List<ItemRequiredTarget> GetItemRequiredTargets()
        {
            List<ItemRequiredTarget> requiredTargets = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM item_required_target";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var requiredTarget = new ItemRequiredTarget
                        {
                            Entry = reader.GetUInt32("entry"),
                            Type = reader.GetByte("type"),
                            TargetEntry = reader.GetUInt32("targetEntry")
                        };

                        requiredTargets.Add(requiredTarget);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return requiredTargets;
        }

        public static List<ItemTemplate> GetItemTemplates()
        {
            List<ItemTemplate> itemTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM item_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var itemTemplate = new ItemTemplate
                        {
                            Entry = reader.GetUInt32("entry"),
                            Patch = reader.GetByte("patch"),
                            Class = reader.GetByte("class"),
                            Subclass = reader.GetByte("subclass"),
                            Name = reader.GetString("name"),
                            Displayid = reader.GetUInt32("displayid"),
                            Quality = reader.GetByte("Quality"),
                            Flags = reader.GetUInt32("Flags"),
                            BuyCount = reader.GetByte("BuyCount"),
                            BuyPrice = reader.GetUInt32("BuyPrice"),
                            SellPrice = reader.GetUInt32("SellPrice"),
                            InventoryType = reader.GetByte("InventoryType"),
                            AllowableClass = reader.GetInt32("AllowableClass"),
                            AllowableRace = reader.GetInt32("AllowableRace"),
                            ItemLevel = reader.GetByte("ItemLevel"),
                            RequiredLevel = reader.GetByte("RequiredLevel"),
                            RequiredSkill = reader.GetUInt16("RequiredSkill"),
                            RequiredSkillRank = reader.GetUInt16("RequiredSkillRank"),
                            RequiredSpell = reader.GetUInt32("requiredspell"),
                            RequiredHonorRank = reader.GetUInt32("requiredhonorrank"),
                            RequiredCityRank = reader.GetUInt32("RequiredCityRank"),
                            RequiredReputationFaction = reader.GetUInt16("RequiredReputationFaction"),
                            RequiredReputationRank = reader.GetUInt16("RequiredReputationRank"),
                            MaxCount = reader.GetUInt16("maxcount"),
                            Stackable = reader.GetUInt16("stackable"),
                            ContainerSlots = reader.GetByte("ContainerSlots"),
                            Armor = reader.GetUInt16("armor"),
                            Delay = reader.GetUInt16("delay"),
                            AmmoType = reader.GetByte("ammo_type"),
                            RangedModRange = reader.GetUInt32("RangedModRange"),
                            Bonding = reader.GetByte("bonding"),
                            Description = reader.GetString("description"),
                            PageText = reader.GetUInt32("PageText"),
                            LanguageID = reader.GetByte("LanguageID"),
                            PageMaterial = reader.GetByte("PageMaterial"),
                            StartQuest = reader.GetUInt32("startquest"),
                            LockID = reader.GetUInt32("lockid"),
                            Material = reader.GetSByte("Material"),  // Note: Material is signed, so GetSByte is used here
                            Sheath = reader.GetByte("sheath"),
                            RandomProperty = reader.GetUInt32("RandomProperty"),
                            Block = reader.GetUInt32("block"),
                            ItemSet = reader.GetUInt32("itemset"),
                            MaxDurability = reader.GetUInt16("MaxDurability"),
                            Area = reader.GetUInt32("area"),
                            Map = reader.GetUInt16("Map"),
                            BagFamily = reader.GetUInt32("BagFamily"),
                            ScriptName = reader.GetString("ScriptName"),
                            DisenchantID = reader.GetUInt32("DisenchantID"),
                            FoodType = reader.GetByte("FoodType"),
                            MinMoneyLoot = reader.GetUInt32("minMoneyLoot"),
                            MaxMoneyLoot = reader.GetUInt32("maxMoneyLoot"),
                            Duration = reader.GetUInt32("Duration"),
                            ExtraFlags = reader.GetByte("ExtraFlags"),
                            OtherTeamEntry = reader.GetUInt32("OtherTeamEntry")

                        };

                        itemTemplate.Stats.Add(new Stat() { Type = reader.GetUInt32("stat_type1"), Value = reader.GetInt32("stat_value1") });
                        itemTemplate.Stats.Add(new Stat() { Type = reader.GetUInt32("stat_type2"), Value = reader.GetInt32("stat_value2") });
                        itemTemplate.Stats.Add(new Stat() { Type = reader.GetUInt32("stat_type3"), Value = reader.GetInt32("stat_value3") });
                        itemTemplate.Stats.Add(new Stat() { Type = reader.GetUInt32("stat_type4"), Value = reader.GetInt32("stat_value4") });
                        itemTemplate.Stats.Add(new Stat() { Type = reader.GetUInt32("stat_type5"), Value = reader.GetInt32("stat_value5") });
                        itemTemplate.Stats.Add(new Stat() { Type = reader.GetUInt32("stat_type6"), Value = reader.GetInt32("stat_value6") });
                        itemTemplate.Stats.Add(new Stat() { Type = reader.GetUInt32("stat_type7"), Value = reader.GetInt32("stat_value7") });
                        itemTemplate.Stats.Add(new Stat() { Type = reader.GetUInt32("stat_type8"), Value = reader.GetInt32("stat_value8") });
                        itemTemplate.Stats.Add(new Stat() { Type = reader.GetUInt32("stat_type9"), Value = reader.GetInt32("stat_value9") });
                        itemTemplate.Stats.Add(new Stat() { Type = reader.GetUInt32("stat_type10"), Value = reader.GetInt32("stat_value10") });

                        itemTemplate.Damages.Add(new Damage() { Min = reader.GetFloat("dmg_min1"), Max = reader.GetFloat("dmg_max1"), Type = reader.GetByte("dmg_type1") });
                        itemTemplate.Damages.Add(new Damage() { Min = reader.GetFloat("dmg_min2"), Max = reader.GetFloat("dmg_max2"), Type = reader.GetByte("dmg_type2") });
                        itemTemplate.Damages.Add(new Damage() { Min = reader.GetFloat("dmg_min3"), Max = reader.GetFloat("dmg_max3"), Type = reader.GetByte("dmg_type3") });
                        itemTemplate.Damages.Add(new Damage() { Min = reader.GetFloat("dmg_min4"), Max = reader.GetFloat("dmg_max4"), Type = reader.GetByte("dmg_type4") });
                        itemTemplate.Damages.Add(new Damage() { Min = reader.GetFloat("dmg_min5"), Max = reader.GetFloat("dmg_max5"), Type = reader.GetByte("dmg_type5") });

                        itemTemplate.Resistances = new Resistance
                        {
                            Holy = reader.GetUInt16("holy_res"),
                            Fire = reader.GetUInt16("fire_res"),
                            Nature = reader.GetUInt16("nature_res"),
                            Frost = reader.GetUInt16("frost_res"),
                            Shadow = reader.GetUInt16("shadow_res"),
                            Arcane = reader.GetUInt16("arcane_res")
                        };

                        itemTemplate.Spells.Add(new Spell()
                        {
                            SpellID = reader.GetUInt32("spellid_1"),
                            Trigger = reader.GetByte("spelltrigger_1"),
                            Charges = reader.GetInt16("spellcharges_1"),
                            PpmRate = reader.GetFloat("spellppmRate_1"),
                            Cooldown = reader.GetInt32("spellcooldown_1"),
                            Category = reader.GetUInt16("spellcategory_1"),
                            CategoryCooldown = reader.GetInt32("spellcategorycooldown_1")
                        });
                        itemTemplate.Spells.Add(new Spell()
                        {
                            SpellID = reader.GetUInt32("spellid_2"),
                            Trigger = reader.GetByte("spelltrigger_2"),
                            Charges = reader.GetInt16("spellcharges_2"),
                            PpmRate = reader.GetFloat("spellppmRate_2"),
                            Cooldown = reader.GetInt32("spellcooldown_2"),
                            Category = reader.GetUInt16("spellcategory_2"),
                            CategoryCooldown = reader.GetInt32("spellcategorycooldown_2")
                        });
                        itemTemplate.Spells.Add(new Spell()
                        {
                            SpellID = reader.GetUInt32("spellid_3"),
                            Trigger = reader.GetByte("spelltrigger_3"),
                            Charges = reader.GetInt16("spellcharges_3"),
                            PpmRate = reader.GetFloat("spellppmRate_3"),
                            Cooldown = reader.GetInt32("spellcooldown_3"),
                            Category = reader.GetUInt16("spellcategory_3"),
                            CategoryCooldown = reader.GetInt32("spellcategorycooldown_3")
                        });
                        itemTemplate.Spells.Add(new Spell()
                        {
                            SpellID = reader.GetUInt32("spellid_4"),
                            Trigger = reader.GetByte("spelltrigger_4"),
                            Charges = reader.GetInt16("spellcharges_4"),
                            PpmRate = reader.GetFloat("spellppmRate_4"),
                            Cooldown = reader.GetInt32("spellcooldown_4"),
                            Category = reader.GetUInt16("spellcategory_4"),
                            CategoryCooldown = reader.GetInt32("spellcategorycooldown_4")
                        });
                        itemTemplate.Spells.Add(new Spell()
                        {
                            SpellID = reader.GetUInt32("spellid_5"),
                            Trigger = reader.GetByte("spelltrigger_5"),
                            Charges = reader.GetInt16("spellcharges_5"),
                            PpmRate = reader.GetFloat("spellppmRate_5"),
                            Cooldown = reader.GetInt32("spellcooldown_5"),
                            Category = reader.GetUInt16("spellcategory_5"),
                            CategoryCooldown = reader.GetInt32("spellcategorycooldown_5")
                        });

                        itemTemplates.Add(itemTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return itemTemplates;
        }
    }
}
