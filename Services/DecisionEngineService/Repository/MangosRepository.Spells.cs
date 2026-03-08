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
        public static List<SkillDiscoveryTemplate> GetSkillDiscoveryTemplates()
        {
            List<SkillDiscoveryTemplate> skillDiscoveries = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM skill_discovery_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SkillDiscoveryTemplate discovery = new()
                        {
                            SpellId = reader.GetUInt32("spellId"),
                            ReqSpell = reader.GetUInt32("reqSpell"),
                            Chance = reader.GetFloat("chance")
                        };

                        skillDiscoveries.Add(discovery);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return skillDiscoveries;
        }

        public static List<SkillExtraItemTemplate> GetSkillExtraItemTemplates()
        {
            List<SkillExtraItemTemplate> skillExtraItems = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM skill_extra_item_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SkillExtraItemTemplate extraItem = new()
                        {
                            SpellId = reader.GetUInt32("spellId"),
                            RequiredSpecialization = reader.GetUInt32("requiredSpecialization"),
                            AdditionalCreateChance = reader.GetFloat("additionalCreateChance"),
                            AdditionalMaxNum = reader.GetByte("additionalMaxNum")
                        };

                        skillExtraItems.Add(extraItem);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return skillExtraItems;
        }

        public static List<SkillFishingBaseLevel> GetSkillFishingBaseLevels()
        {
            List<SkillFishingBaseLevel> fishingBaseLevels = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM skill_fishing_base_level";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SkillFishingBaseLevel fishingBaseLevel = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Skill = reader.GetInt32("skill")
                        };
                        fishingBaseLevels.Add(fishingBaseLevel);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return fishingBaseLevels;
        }

        public static List<SpellAffect> GetSpellAffect()
        {
            List<SpellAffect> spellAffects = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_affect";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellAffect affect = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            EffectId = reader.GetByte("effectId"),
                            SpellFamilyMask = reader.GetUInt64("SpellFamilyMask")
                        };

                        spellAffects.Add(affect);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellAffects;
        }

        public static List<SpellArea> GetSpellAreas()
        {
            List<SpellArea> spellAreas = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_area";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellArea spellArea = new()
                        {
                            Spell = reader.GetUInt32("spell"),
                            Area = reader.GetUInt32("area"),
                            QuestStart = reader.GetUInt32("quest_start"),
                            QuestStartActive = reader.GetByte("quest_start_active"),
                            QuestEnd = reader.GetUInt32("quest_end"),
                            AuraSpell = reader.GetUInt32("aura_spell"),
                            Racemask = reader.GetUInt32("racemask"),
                            Gender = reader.GetByte("gender"),
                            Autocast = reader.GetByte("autocast")
                        };

                        spellAreas.Add(spellArea);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellAreas;
        }

        public static List<SpellBonusData> GetSpellBonusData()
        {
            List<SpellBonusData> spellBonusDataList = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_bonus_data";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellBonusData spellBonusData = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            DirectBonus = reader.GetFloat("direct_bonus"),
                            DotBonus = reader.GetFloat("dot_bonus"),
                            ApBonus = reader.GetFloat("ap_bonus"),
                            ApDotBonus = reader.GetFloat("ap_dot_bonus"),
                            Comments = reader.IsDBNull("comments") ? string.Empty : reader.GetString("comments")
                        };

                        spellBonusDataList.Add(spellBonusData);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellBonusDataList;
        }

        public static List<SpellChain> GetSpellChain()
        {
            List<SpellChain> spellChainList = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_chain";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellChain spellChain = new()
                        {
                            SpellId = reader.GetUInt32("spell_id"),
                            PrevSpell = reader.GetUInt32("prev_spell"),
                            FirstSpell = reader.GetUInt32("first_spell"),
                            Rank = reader.GetByte("rank"),
                            ReqSpell = reader.GetUInt32("req_spell")
                        };

                        spellChainList.Add(spellChain);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellChainList;
        }

        public static List<SpellCheck> GetSpellCheck()
        {
            List<SpellCheck> spellCheckList = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_check";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellCheck spellCheck = new()
                        {
                            Spellid = reader.GetUInt32("spellid"),
                            SpellFamilyName = reader.GetInt32("SpellFamilyName"),
                            SpellFamilyMask = reader.GetInt64("SpellFamilyMask"),
                            SpellIcon = reader.GetInt32("SpellIcon"),
                            SpellVisual = reader.GetInt32("SpellVisual"),
                            SpellCategory = reader.GetInt32("SpellCategory"),
                            EffectType = reader.GetInt32("EffectType"),
                            EffectAura = reader.GetInt32("EffectAura"),
                            EffectIdx = reader.GetInt32("EffectIdx"),
                            Name = reader.GetString("Name"),
                            Code = reader.GetString("Code")
                        };

                        spellCheckList.Add(spellCheck);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellCheckList;
        }

        public static List<uint> GetDisabledSpells()
        {
            List<uint> disabledSpells = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_disabled";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        disabledSpells.Add(reader.GetUInt32("entry"));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return disabledSpells;
        }

        public static List<SpellEffectMod> GetSpellEffectMods()
        {
            List<SpellEffectMod> spellEffectMods = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_effect_mod";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellEffectMod effectMod = new()
                        {
                            Id = reader.GetUInt32("Id"),
                            EffectIndex = reader.GetUInt32("EffectIndex"),
                            Effect = reader.GetInt32("Effect"),
                            EffectDieSides = reader.GetInt32("EffectDieSides"),
                            EffectBaseDice = reader.GetInt32("EffectBaseDice"),
                            EffectDicePerLevel = reader.GetFloat("EffectDicePerLevel"),
                            EffectRealPointsPerLevel = reader.GetFloat("EffectRealPointsPerLevel"),
                            EffectBasePoints = reader.GetInt32("EffectBasePoints"),
                            EffectAmplitude = reader.GetInt32("EffectAmplitude"),
                            EffectPointsPerComboPoint = reader.GetFloat("EffectPointsPerComboPoint"),
                            EffectChainTarget = reader.GetInt32("EffectChainTarget"),
                            EffectMultipleValue = reader.GetFloat("EffectMultipleValue"),
                            EffectMechanic = reader.GetInt32("EffectMechanic"),
                            EffectImplicitTargetA = reader.GetInt32("EffectImplicitTargetA"),
                            EffectImplicitTargetB = reader.GetInt32("EffectImplicitTargetB"),
                            EffectRadiusIndex = reader.GetInt32("EffectRadiusIndex"),
                            EffectApplyAuraName = reader.GetInt32("EffectApplyAuraName"),
                            EffectItemType = reader.GetInt32("EffectItemType"),
                            EffectMiscValue = reader.GetInt32("EffectMiscValue"),
                            EffectTriggerSpell = reader.GetInt32("EffectTriggerSpell"),
                            Comment = reader.IsDBNull("Comment") ? string.Empty : reader.GetString("Comment")
                        };

                        spellEffectMods.Add(effectMod);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellEffectMods;
        }

        public static List<SpellElixir> GetSpellElixirs()
        {
            List<SpellElixir> spellElixirs = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_elixir";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellElixir elixir = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Mask = reader.GetByte("mask")
                        };

                        spellElixirs.Add(elixir);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellElixirs;
        }

        public static List<SpellFacing> GetSpellFacings()
        {
            List<SpellFacing> spellFacings = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_facing";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellFacing facing = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Facingcasterflag = reader.GetByte("facingcasterflag")
                        };

                        spellFacings.Add(facing);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellFacings;
        }

        public static List<SpellGroup> GetSpellGroups()
        {
            List<SpellGroup> spellGroups = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_group";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellGroup group = new()
                        {
                            GroupId = reader.GetUInt32("group_id"),
                            GroupSpellId = reader.GetUInt32("group_spell_id"),
                            SpellId = reader.GetUInt32("spell_id")
                        };

                        spellGroups.Add(group);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellGroups;
        }

        public static List<SpellGroupStackRules> GetSpellGroupStackRules()
        {
            List<SpellGroupStackRules> spellGroupStackRules = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_group_stack_rules";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellGroupStackRules rule = new()
                        {
                            GroupId = reader.GetUInt32("group_id"),
                            StackRule = reader.GetByte("stack_rule")
                        };

                        spellGroupStackRules.Add(rule);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellGroupStackRules;
        }

        public static List<SpellLearnSpell> GetSpellLearnSpells()
        {
            List<SpellLearnSpell> spellLearnSpells = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_learn_spell";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellLearnSpell learnSpell = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            SpellId = reader.GetUInt32("SpellID"),
                            Active = reader.GetByte("Active")
                        };

                        spellLearnSpells.Add(learnSpell);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellLearnSpells;
        }

        public static List<SpellMod> GetSpellMods()
        {
            List<SpellMod> spellMods = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_mod";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellMod spellMod = new()
                        {
                            Id = reader.GetUInt32("Id"),
                            ProcChance = reader.IsDBNull("procChance") ? -1 : reader.GetInt32("procChance"),
                            ProcFlags = reader.IsDBNull("procFlags") ? -1 : reader.GetInt32("procFlags"),
                            ProcCharges = reader.IsDBNull("procCharges") ? -1 : reader.GetInt32("procCharges"),
                            DurationIndex = reader.IsDBNull("DurationIndex") ? -1 : reader.GetInt32("DurationIndex"),
                            Category = reader.IsDBNull("Category") ? -1 : reader.GetInt32("Category"),
                            CastingTimeIndex = reader.IsDBNull("CastingTimeIndex") ? -1 : reader.GetInt32("CastingTimeIndex"),
                            StackAmount = reader.IsDBNull("StackAmount") ? -1 : reader.GetInt32("StackAmount"),
                            SpellIconId = reader.IsDBNull("SpellIconID") ? -1 : reader.GetInt32("SpellIconID"),
                            ActiveIconId = reader.IsDBNull("activeIconID") ? -1 : reader.GetInt32("activeIconID"),
                            ManaCost = reader.IsDBNull("manaCost") ? -1 : reader.GetInt32("manaCost"),
                            Attributes = reader.IsDBNull("Attributes") ? -1 : reader.GetInt32("Attributes"),
                            AttributesEx = reader.IsDBNull("AttributesEx") ? -1 : reader.GetInt32("AttributesEx"),
                            AttributesEx2 = reader.IsDBNull("AttributesEx2") ? -1 : reader.GetInt32("AttributesEx2"),
                            AttributesEx3 = reader.IsDBNull("AttributesEx3") ? -1 : reader.GetInt32("AttributesEx3"),
                            AttributesEx4 = reader.IsDBNull("AttributesEx4") ? -1 : reader.GetInt32("AttributesEx4"),
                            Custom = reader.GetInt32("Custom"),
                            InterruptFlags = reader.IsDBNull("InterruptFlags") ? -1 : reader.GetInt32("InterruptFlags"),
                            AuraInterruptFlags = reader.IsDBNull("AuraInterruptFlags") ? -1 : reader.GetInt32("AuraInterruptFlags"),
                            ChannelInterruptFlags = reader.IsDBNull("ChannelInterruptFlags") ? -1 : reader.GetInt32("ChannelInterruptFlags"),
                            Dispel = reader.GetInt32("Dispel"),
                            Stances = reader.IsDBNull("Stances") ? -1 : reader.GetInt32("Stances"),
                            StancesNot = reader.IsDBNull("StancesNot") ? -1 : reader.GetInt32("StancesNot"),
                            SpellVisual = reader.IsDBNull("SpellVisual") ? -1 : reader.GetInt32("SpellVisual"),
                            ManaCostPercentage = reader.IsDBNull("ManaCostPercentage") ? -1 : reader.GetInt32("ManaCostPercentage"),
                            StartRecoveryCategory = reader.IsDBNull("StartRecoveryCategory") ? -1 : reader.GetInt32("StartRecoveryCategory"),
                            StartRecoveryTime = reader.IsDBNull("StartRecoveryTime") ? -1 : reader.GetInt32("StartRecoveryTime"),
                            MaxAffectedTargets = reader.IsDBNull("MaxAffectedTargets") ? -1 : reader.GetInt32("MaxAffectedTargets"),
                            MaxTargetLevel = reader.IsDBNull("MaxTargetLevel") ? -1 : reader.GetInt32("MaxTargetLevel"),
                            DmgClass = reader.IsDBNull("DmgClass") ? -1 : reader.GetInt32("DmgClass"),
                            RangeIndex = reader.IsDBNull("rangeIndex") ? -1 : reader.GetInt32("rangeIndex"),
                            RecoveryTime = reader.GetInt32("RecoveryTime"),
                            CategoryRecoveryTime = reader.GetInt32("CategoryRecoveryTime"),
                            SpellFamilyName = reader.GetInt32("SpellFamilyName"),
                            SpellFamilyFlags = reader.GetUInt64("SpellFamilyFlags"),
                            Mechanic = reader.IsDBNull("Mechanic") ? -1 : reader.GetInt32("Mechanic"),
                            EquippedItemClass = reader.IsDBNull("EquippedItemClass") ? -1 : reader.GetInt32("EquippedItemClass"),
                            Comment = reader.IsDBNull("Comment") ? string.Empty : reader.GetString("Comment")
                        };

                        spellMods.Add(spellMod);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellMods;
        }

        public static List<SpellPetAura> GetSpellPetAuras()
        {
            List<SpellPetAura> spellPetAuras = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_pet_auras";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellPetAura spellPetAura = new()
                        {
                            Spell = reader.GetUInt32("spell"),
                            Pet = reader.GetUInt32("pet"),
                            Aura = reader.GetUInt32("aura")
                        };

                        spellPetAuras.Add(spellPetAura);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellPetAuras;
        }

        public static List<SpellProcEvent> GetSpellProcEvents()
        {
            List<SpellProcEvent> spellProcEvents = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_proc_event";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellProcEvent spellProcEvent = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            SchoolMask = reader.GetByte("SchoolMask"),
                            SpellFamilyName = reader.GetUInt16("SpellFamilyName"),
                            SpellFamilyMask0 = reader.GetUInt64("SpellFamilyMask0"),
                            SpellFamilyMask1 = reader.GetUInt64("SpellFamilyMask1"),
                            SpellFamilyMask2 = reader.GetUInt64("SpellFamilyMask2"),
                            ProcFlags = reader.GetUInt32("procFlags"),
                            ProcEx = reader.GetUInt32("procEx"),
                            PpmRate = reader.GetFloat("ppmRate"),
                            CustomChance = reader.GetFloat("CustomChance"),
                            Cooldown = reader.GetUInt32("Cooldown")
                        };

                        spellProcEvents.Add(spellProcEvent);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellProcEvents;
        }

        public static List<SpellProcItemEnchant> GetSpellProcItemEnchants()
        {
            List<SpellProcItemEnchant> spellProcItemEnchants = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_proc_item_enchant";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellProcItemEnchant itemEnchant = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            PpmRate = reader.GetFloat("ppmRate")
                        };

                        spellProcItemEnchants.Add(itemEnchant);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellProcItemEnchants;
        }

        public static List<SpellScript> GetSpellScripts()
        {
            List<SpellScript> spellScripts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_scripts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellScript spellScript = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Delay = reader.GetUInt32("delay"),
                            Command = reader.GetUInt32("command"),
                            Datalong = reader.GetUInt32("datalong"),
                            Datalong2 = reader.GetUInt32("datalong2"),
                            Datalong3 = reader.GetUInt32("datalong3"),
                            Datalong4 = reader.GetUInt32("datalong4"),
                            TargetParam1 = reader.GetUInt32("target_param1"),
                            TargetParam2 = reader.GetUInt32("target_param2"),
                            TargetType = reader.GetByte("target_type"),
                            DataFlags = reader.GetByte("data_flags"),
                            Dataint = reader.GetInt32("dataint"),
                            Dataint2 = reader.GetInt32("dataint2"),
                            Dataint3 = reader.GetInt32("dataint3"),
                            Dataint4 = reader.GetInt32("dataint4"),
                            X = reader.GetFloat("x"),
                            Y = reader.GetFloat("y"),
                            Z = reader.GetFloat("z"),
                            O = reader.GetFloat("o"),
                            ConditionId = reader.GetUInt32("condition_id"),
                            Comments = reader.GetString("comments")
                        };

                        spellScripts.Add(spellScript);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellScripts;
        }

        public static List<SpellScriptTarget> GetSpellScriptTargets()
        {
            List<SpellScriptTarget> spellScriptTargets = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_script_target";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellScriptTarget target = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Type = reader.GetByte("type"),
                            TargetEntry = reader.GetUInt32("targetEntry")
                        };

                        spellScriptTargets.Add(target);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellScriptTargets;
        }

        public static List<SpellTargetPosition> GetSpellTargetPositions()
        {
            List<SpellTargetPosition> spellTargetPositions = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_target_position";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellTargetPosition targetPosition = new()
                        {
                            Id = reader.GetUInt32("id"),
                            TargetMap = reader.GetUInt16("target_map"),
                            TargetPositionX = reader.GetFloat("target_position_x"),
                            TargetPositionY = reader.GetFloat("target_position_y"),
                            TargetPositionZ = reader.GetFloat("target_position_z"),
                            TargetOrientation = reader.GetFloat("target_orientation")
                        };

                        spellTargetPositions.Add(targetPosition);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellTargetPositions;
        }

        public static List<SpellTemplate> GetSpellTemplates()
        {
            List<SpellTemplate> spellTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellTemplate spellTemplate = new()
                        {
                            Id = reader.GetUInt32("ID"),
                            School = reader.GetUInt32("school"),
                            Category = reader.GetUInt32("category"),
                            CastUi = reader.GetUInt32("castUI"),
                            Dispel = reader.GetUInt32("dispel"),
                            Mechanic = reader.GetUInt32("mechanic"),
                            Attributes = reader.GetUInt64("attributes"),
                            AttributesEx = reader.GetUInt32("attributesEx"),
                            AttributesEx2 = reader.GetUInt32("attributesEx2"),
                            AttributesEx3 = reader.GetUInt32("attributesEx3"),
                            AttributesEx4 = reader.GetUInt32("attributesEx4"),
                            Stances = reader.GetUInt32("stances"),
                            StancesNot = reader.GetUInt32("stancesNot"),
                            Targets = reader.GetUInt32("targets"),
                            TargetCreatureType = reader.GetUInt32("targetCreatureType"),
                            RequiresSpellFocus = reader.GetUInt32("requiresSpellFocus"),
                            CasterAuraState = reader.GetUInt32("casterAuraState"),
                            TargetAuraState = reader.GetUInt32("targetAuraState"),
                            CastingTimeIndex = reader.GetUInt32("castingTimeIndex"),
                            RecoveryTime = reader.GetUInt32("recoveryTime"),
                            CategoryRecoveryTime = reader.GetUInt32("categoryRecoveryTime"),
                            InterruptFlags = reader.GetUInt32("interruptFlags"),
                            AuraInterruptFlags = reader.GetUInt32("auraInterruptFlags"),
                            ChannelInterruptFlags = reader.GetUInt32("channelInterruptFlags"),
                            ProcFlags = reader.GetUInt32("procFlags"),
                            ProcChance = reader.GetUInt32("procChance"),
                            ProcCharges = reader.GetUInt32("procCharges"),
                            MaxLevel = reader.GetUInt32("maxLevel"),
                            BaseLevel = reader.GetUInt32("baseLevel"),
                            SpellLevel = reader.GetUInt32("spellLevel"),
                            DurationIndex = reader.GetUInt32("durationIndex"),
                            PowerType = reader.GetUInt32("powerType"),
                            ManaCost = reader.GetUInt32("manaCost"),
                            ManaCostPerLevel = reader.GetUInt32("manCostPerLevel"),
                            ManaPerSecond = reader.GetUInt32("manaPerSecond"),
                            ManaPerSecondPerLevel = reader.GetUInt32("manaPerSecondPerLevel"),
                            RangeIndex = reader.GetUInt32("rangeIndex"),
                            Speed = reader.GetFloat("speed"),
                            ModelNextSpell = reader.GetUInt32("modelNextSpell"),
                            StackAmount = reader.GetUInt32("stackAmount"),
                            Totem1 = reader.GetUInt32("totem1"),
                            Totem2 = reader.GetUInt32("totem2"),
                            Reagent1 = reader.GetUInt32("reagent1"),
                            Reagent2 = reader.GetUInt32("reagent2"),
                            Reagent3 = reader.GetUInt32("reagent3"),
                            Reagent4 = reader.GetUInt32("reagent4"),
                            Reagent5 = reader.GetUInt32("reagent5"),
                            Reagent6 = reader.GetUInt32("reagent6"),
                            Reagent7 = reader.GetUInt32("reagent7"),
                            Reagent8 = reader.GetUInt32("reagent8"),
                            ReagentCount1 = reader.GetUInt32("reagentCount1"),
                            ReagentCount2 = reader.GetUInt32("reagentCount2"),
                            ReagentCount3 = reader.GetUInt32("reagentCount3"),
                            ReagentCount4 = reader.GetUInt32("reagentCount4"),
                            ReagentCount5 = reader.GetUInt32("reagentCount5"),
                            ReagentCount6 = reader.GetUInt32("reagentCount6"),
                            ReagentCount7 = reader.GetUInt32("reagentCount7"),
                            ReagentCount8 = reader.GetUInt32("reagentCount8"),
                            EquippedItemClass = reader.GetInt32("equippedItemClass"),
                            EquippedItemSubClassMask = reader.GetInt32("equippedItemSubClassMask"),
                            EquippedItemInventoryTypeMask = reader.GetInt32("equippedItemInventoryTypeMask"),
                            Effect1 = reader.GetUInt32("effect1"),
                            Effect2 = reader.GetUInt32("effect2"),
                            Effect3 = reader.GetUInt32("effect3"),
                            EffectDieSides1 = reader.GetInt32("effectDieSides1"),
                            EffectDieSides2 = reader.GetInt32("effectDieSides2"),
                            EffectDieSides3 = reader.GetInt32("effectDieSides3"),
                            EffectBaseDice1 = reader.GetInt32("effectBaseDice1"),
                            EffectBaseDice2 = reader.GetInt32("effectBaseDice2"),
                            EffectBaseDice3 = reader.GetInt32("effectBaseDice3"),
                            EffectDicePerLevel1 = reader.GetFloat("effectDicePerLevel1"),
                            EffectDicePerLevel2 = reader.GetFloat("effectDicePerLevel2"),
                            EffectDicePerLevel3 = reader.GetFloat("effectDicePerLevel3"),
                            EffectRealPointsPerLevel1 = reader.GetFloat("effectRealPointsPerLevel1"),
                            EffectRealPointsPerLevel2 = reader.GetFloat("effectRealPointsPerLevel2"),
                            EffectRealPointsPerLevel3 = reader.GetFloat("effectRealPointsPerLevel3"),
                            EffectBasePoints1 = reader.GetInt32("effectBasePoints1"),
                            EffectBasePoints2 = reader.GetInt32("effectBasePoints2"),
                            EffectBasePoints3 = reader.GetInt32("effectBasePoints3"),
                            EffectMechanic1 = reader.GetUInt32("effectMechanic1"),
                            EffectMechanic2 = reader.GetUInt32("effectMechanic2"),
                            EffectMechanic3 = reader.GetUInt32("effectMechanic3"),
                            EffectImplicitTargetA1 = reader.GetUInt32("effectImplicitTargetA1"),
                            EffectImplicitTargetA2 = reader.GetUInt32("effectImplicitTargetA2"),
                            EffectImplicitTargetA3 = reader.GetUInt32("effectImplicitTargetA3"),
                            EffectImplicitTargetB1 = reader.GetUInt32("effectImplicitTargetB1"),
                            EffectImplicitTargetB2 = reader.GetUInt32("effectImplicitTargetB2"),
                            EffectImplicitTargetB3 = reader.GetUInt32("effectImplicitTargetB3"),
                            EffectRadiusIndex1 = reader.GetUInt32("effectRadiusIndex1"),
                            EffectRadiusIndex2 = reader.GetUInt32("effectRadiusIndex2"),
                            EffectRadiusIndex3 = reader.GetUInt32("effectRadiusIndex3"),
                            EffectApplyAuraName1 = reader.GetUInt32("effectApplyAuraName1"),
                            EffectApplyAuraName2 = reader.GetUInt32("effectApplyAuraName2"),
                            EffectApplyAuraName3 = reader.GetUInt32("effectApplyAuraName3"),
                            EffectAmplitude1 = reader.GetUInt32("effectAmplitude1"),
                            EffectAmplitude2 = reader.GetUInt32("effectAmplitude2"),
                            EffectAmplitude3 = reader.GetUInt32("effectAmplitude3"),
                            EffectMultipleValue1 = reader.GetFloat("effectMultipleValue1"),
                            EffectMultipleValue2 = reader.GetFloat("effectMultipleValue2"),
                            EffectMultipleValue3 = reader.GetFloat("effectMultipleValue3"),
                            EffectChainTarget1 = reader.GetUInt32("effectChainTarget1"),
                            EffectChainTarget2 = reader.GetUInt32("effectChainTarget2"),
                            EffectChainTarget3 = reader.GetUInt32("effectChainTarget3"),
                            EffectItemType1 = reader.GetUInt32("effectItemType1"),
                            EffectItemType2 = reader.GetUInt32("effectItemType2"),
                            EffectItemType3 = reader.GetUInt32("effectItemType3"),
                            EffectMiscValue1 = reader.GetInt32("effectMiscValue1"),
                            EffectMiscValue2 = reader.GetInt32("effectMiscValue2"),
                            EffectMiscValue3 = reader.GetInt32("effectMiscValue3"),
                            EffectTriggerSpell1 = reader.GetUInt32("effectTriggerSpell1"),
                            EffectTriggerSpell2 = reader.GetUInt32("effectTriggerSpell2"),
                            EffectTriggerSpell3 = reader.GetUInt32("effectTriggerSpell3"),
                            EffectPointsPerComboPoint1 = reader.GetFloat("effectPointsPerComboPoint1"),
                            EffectPointsPerComboPoint2 = reader.GetFloat("effectPointsPerComboPoint2"),
                            EffectPointsPerComboPoint3 = reader.GetFloat("effectPointsPerComboPoint3"),
                            SpellVisual1 = reader.GetUInt32("spellVisual1"),
                            SpellVisual2 = reader.GetUInt32("spellVisual2"),
                            SpellIconId = reader.GetUInt32("spellIconId"),
                            ActiveIconId = reader.GetUInt32("activeIconId"),
                            SpellPriority = reader.GetUInt32("spellPriority"),
                            Name1 = reader.GetString("name1"),
                            Name2 = reader.GetString("name2"),
                            Name3 = reader.GetString("name3"),
                            Name4 = reader.GetString("name4"),
                            Name5 = reader.GetString("name5"),
                            Name6 = reader.GetString("name6"),
                            Name7 = reader.GetString("name7"),
                            Name8 = reader.GetString("name8"),
                            NameFlags = reader.GetUInt32("nameFlags"),
                            NameSubtext1 = reader.GetString("nameSubtext1"),
                            NameSubtext2 = reader.GetString("nameSubtext2"),
                            NameSubtext3 = reader.GetString("nameSubtext3"),
                            NameSubtext4 = reader.GetString("nameSubtext4"),
                            NameSubtext5 = reader.GetString("nameSubtext5"),
                            NameSubtext6 = reader.GetString("nameSubtext6"),
                            NameSubtext7 = reader.GetString("nameSubtext7"),
                            NameSubtext8 = reader.GetString("nameSubtext8"),
                            NameSubtextFlags = reader.GetUInt32("nameSubtextFlags"),
                            Description1 = reader.GetString("description1"),
                            Description2 = reader.GetString("description2"),
                            Description3 = reader.GetString("description3"),
                            Description4 = reader.GetString("description4"),
                            Description5 = reader.GetString("description5"),
                            Description6 = reader.GetString("description6"),
                            Description7 = reader.GetString("description7"),
                            Description8 = reader.GetString("description8"),
                            DescriptionFlags = reader.GetUInt32("descriptionFlags"),
                            AuraDescription1 = reader.GetString("auraDescription1"),
                            AuraDescription2 = reader.GetString("auraDescription2"),
                            AuraDescription3 = reader.GetString("auraDescription3"),
                            AuraDescription4 = reader.GetString("auraDescription4"),
                            AuraDescription5 = reader.GetString("auraDescription5"),
                            AuraDescription6 = reader.GetString("auraDescription6"),
                            AuraDescription7 = reader.GetString("auraDescription7"),
                            AuraDescription8 = reader.GetString("auraDescription8"),
                            AuraDescriptionFlags = reader.GetUInt32("auraDescriptionFlags"),
                            ManaCostPercentage = reader.GetUInt32("manaCostPercentage"),
                            StartRecoveryCategory = reader.GetUInt32("startRecoveryCategory"),
                            StartRecoveryTime = reader.GetUInt32("startRecoveryTime"),
                            MaxTargetLevel = reader.GetUInt32("maxTargetLevel"),
                            SpellFamilyName = reader.GetUInt32("spellFamilyName"),
                            SpellFamilyFlags = reader.GetUInt64("spellFamilyFlags"),
                            MaxAffectedTargets = reader.GetUInt32("maxAffectedTargets"),
                            DmgClass = reader.GetUInt32("dmgClass"),
                            PreventionType = reader.GetUInt32("preventionType"),
                            StanceBarOrder = reader.GetInt32("stanceBarOrder"),
                            DmgMultiplier1 = reader.GetFloat("dmgMultiplier1"),
                            DmgMultiplier2 = reader.GetFloat("dmgMultiplier2"),
                            DmgMultiplier3 = reader.GetFloat("dmgMultiplier3"),
                            MinFactionId = reader.GetUInt32("minFactionId"),
                            MinReputation = reader.GetUInt32("minReputation"),
                            RequiredAuraVision = reader.GetUInt32("requiredAuraVision")
                        };


                        spellTemplates.Add(spellTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellTemplates;
        }

        public static List<SpellThreat> GetSpellThreats()
        {
            List<SpellThreat> spellThreats = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM spell_threat";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SpellThreat spellThreat = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Threat = reader.GetInt32("Threat"),
                            Multiplier = reader.GetFloat("multiplier"),
                            ApBonus = reader.GetFloat("ap_bonus")
                        };

                        spellThreats.Add(spellThreat);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spellThreats;
        }
    }
}
