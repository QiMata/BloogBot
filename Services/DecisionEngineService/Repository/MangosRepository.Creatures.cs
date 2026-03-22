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
        public static List<Creature> GetCreatures()
        {
            List<Creature> creatureResults = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Creature creatureResult = new()
                        {
                            Guid = reader.GetUInt32("guid"),
                            Id = reader.GetUInt32("id"),
                            Map = reader.GetUInt32("map"),
                            Modelid = reader.GetUInt32("modelid"),
                            EquipmentId = reader.GetUInt32("equipment_id"),
                            PositionX = reader.GetFloat("position_x"),
                            PositionY = reader.GetFloat("position_y"),
                            PositionZ = reader.GetFloat("position_z"),
                            Orientation = reader.GetFloat("orientation"),
                            Spawntimesecsmin = reader.GetUInt32("spawntimesecsmin"),
                            Spawntimesecsmax = reader.GetUInt32("spawntimesecsmax"),
                            Spawndist = reader.GetFloat("spawndist"),
                            Currentwaypoint = reader.GetUInt32("currentwaypoint"),
                            Curhealth = reader.GetUInt32("curhealth"),
                            Curmana = reader.GetUInt32("curmana"),
                            DeathState = reader.GetUInt32("DeathState"),
                            MovementType = reader.GetUInt32("MovementType"),
                            SpawnFlags = reader.GetUInt32("spawnFlags"),
                            Visibilitymod = reader.GetFloat("visibilitymod"),
                            PatchMin = reader.GetUInt32("patch_min"),
                            PatchMax = reader.GetUInt32("patch_max")
                        };
                        creatureResults.Add(creatureResult);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return creatureResults;
        }

        public static List<CreatureAddon> GetCreatureAddons()
        {
            List<CreatureAddon> creatureAddons = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_addon";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureAddon creatureAddon = new()
                        {
                            Guid = reader.GetUInt32("guid"),
                            Patch = reader.GetUInt32("patch"),
                            Mount = reader.GetUInt32("mount"),
                            Bytes1 = reader.GetUInt32("bytes1"),
                            B20Sheath = reader.GetUInt32("b2_0_sheath"),
                            B21Flags = reader.GetUInt32("b2_1_flags"),
                            Emote = reader.GetUInt32("emote"),
                            Moveflags = reader.GetUInt32("moveflags"),
                            Auras = reader.IsDBNull("auras") ? string.Empty : reader.GetString("auras")
                        };
                        creatureAddons.Add(creatureAddon);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return creatureAddons;
        }

        public static List<CreatureAIEvent> GetCreatureAIEvents()
        {
            List<CreatureAIEvent> aiEvents = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_ai_events";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureAIEvent aiEvent = new()
                        {
                            Id = reader.GetUInt32("id"),
                            CreatureId = reader.GetUInt32("creature_id"),
                            ConditionId = reader.GetUInt32("condition_id"),
                            EventType = reader.GetByte("event_type"),
                            EventInversePhaseMask = reader.GetUInt32("event_inverse_phase_mask"),
                            EventChance = reader.GetUInt32("event_chance"),
                            EventFlags = reader.GetUInt32("event_flags"),
                            EventParam1 = reader.GetInt32("event_param1"),
                            EventParam2 = reader.GetInt32("event_param2"),
                            EventParam3 = reader.GetInt32("event_param3"),
                            EventParam4 = reader.GetInt32("event_param4"),
                            Action1Script = reader.GetUInt32("action1_script"),
                            Action2Script = reader.GetUInt32("action2_script"),
                            Action3Script = reader.GetUInt32("action3_script"),
                            Comment = reader.GetString("comment")
                        };
                        aiEvents.Add(aiEvent);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return aiEvents;
        }

        public static List<CreatureBattleground> GetCreatureBattlegrounds()
        {
            List<CreatureBattleground> creatureBattlegrounds = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_battleground";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureBattleground creatureBattleground = new()
                        {
                            Guid = reader.GetUInt32("guid"),
                            Event1 = reader.GetByte("event1"),
                            Event2 = reader.GetByte("event2")
                        };
                        creatureBattlegrounds.Add(creatureBattleground);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return creatureBattlegrounds;
        }

        public static List<CreatureEquipTemplate> GetCreatureEquipTemplate()
        {
            List<CreatureEquipTemplate> equipTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_equip_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureEquipTemplate equipTemplate = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Patch = reader.GetByte("patch"),
                            Equipentry1 = reader.GetUInt32("equipentry1"),
                            Equipentry2 = reader.GetUInt32("equipentry2"),
                            Equipentry3 = reader.GetUInt32("equipentry3")
                        };
                        equipTemplates.Add(equipTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return equipTemplates;
        }

        public static List<CreatureEquipTemplateRaw> GetCreatureEquipTemplateRaws()
        {
            List<CreatureEquipTemplateRaw> equipTemplateRaws = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT *
                        FROM creature_equip_template_raw
                        ";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureEquipTemplateRaw equipTemplateRaw = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Patch = reader.GetByte("patch"),
                            Equipmodel1 = reader.GetUInt32("equipmodel1"),
                            Equipmodel2 = reader.GetUInt32("equipmodel2"),
                            Equipmodel3 = reader.GetUInt32("equipmodel3"),
                            Equipinfo1 = reader.GetUInt32("equipinfo1"),
                            Equipinfo2 = reader.GetUInt32("equipinfo2"),
                            Equipinfo3 = reader.GetUInt32("equipinfo3"),
                            Equipslot1 = reader.GetInt32("equipslot1"),
                            Equipslot2 = reader.GetInt32("equipslot2"),
                            Equipslot3 = reader.GetInt32("equipslot3")
                        };
                        equipTemplateRaws.Add(equipTemplateRaw);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return equipTemplateRaws;
        }

        public static List<CreatureAIScript> GetCreatureAIScripts()
        {
            List<CreatureAIScript> aiScripts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_ai_scripts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureAIScript aiScript = new()
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
                        aiScripts.Add(aiScript);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return aiScripts;
        }

        public static List<CreatureInvolvedRelation> GetCreatureInvolvedRelations()
        {
            List<CreatureInvolvedRelation> relations = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_involvedrelation";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureInvolvedRelation relation = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Quest = reader.GetUInt32("quest"),
                            Patch = reader.GetUInt32("patch")
                        };
                        relations.Add(relation);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return relations;
        }

        public static List<CreatureLinking> GetCreatureLinkings()
        {
            List<CreatureLinking> linkings = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_linking";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureLinking linking = new()
                        {
                            Guid = reader.GetUInt32("guid"),
                            MasterGuid = reader.GetUInt32("master_guid"),
                            Flag = reader.GetUInt32("flag")
                        };
                        linkings.Add(linking);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return linkings;
        }

        public static List<CreatureLinkingTemplate> GetCreatureLinkingTemplates()
        {
            List<CreatureLinkingTemplate> linkingTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_linking_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureLinkingTemplate linkingTemplate = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Map = reader.GetUInt32("map"),
                            MasterEntry = reader.GetUInt32("master_entry"),
                            Flag = reader.GetUInt32("flag"),
                            SearchRange = reader.GetUInt32("search_range")
                        };
                        linkingTemplates.Add(linkingTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return linkingTemplates;
        }

        public static List<CreatureLootTemplate> GetCreatureLootTemplates()
        {
            List<CreatureLootTemplate> lootTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_loot_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureLootTemplate lootTemplate = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Item = reader.GetUInt32("item"),
                            ChanceOrQuestChance = reader.GetFloat("ChanceOrQuestChance"),
                            Groupid = reader.GetUInt32("groupid"),
                            MincountOrRef = reader.GetInt32("mincountOrRef"),
                            Maxcount = reader.GetUInt32("maxcount"),
                            ConditionId = reader.GetUInt32("condition_id"),
                            PatchMin = reader.GetUInt32("patch_min"),
                            PatchMax = reader.GetUInt32("patch_max")
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

        public static List<CreatureModelInfo> GetCreatureModelInfos()
        {
            List<CreatureModelInfo> modelInfos = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_model_info";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureModelInfo modelInfo = new()
                        {
                            Modelid = reader.GetUInt32("modelid"),
                            BoundingRadius = reader.GetFloat("bounding_radius"),
                            CombatReach = reader.GetFloat("combat_reach"),
                            Gender = reader.GetByte("gender"),
                            ModelidOtherGender = reader.GetUInt32("modelid_other_gender"),
                            ModelidOtherTeam = reader.GetUInt32("modelid_other_team")
                        };
                        modelInfos.Add(modelInfo);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return modelInfos;
        }

        public static List<CreatureMovement> GetCreatureMovements()
        {
            List<CreatureMovement> movements = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_movement";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureMovement movement = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Point = reader.GetUInt32("point"),
                            PositionX = reader.GetFloat("position_x"),
                            PositionY = reader.GetFloat("position_y"),
                            PositionZ = reader.GetFloat("position_z"),
                            Waittime = reader.GetUInt32("waittime"),
                            ScriptId = reader.GetUInt32("script_id"),
                            Textid1 = reader.GetInt32("textid1"),
                            Textid2 = reader.GetInt32("textid2"),
                            Textid3 = reader.GetInt32("textid3"),
                            Textid4 = reader.GetInt32("textid4"),
                            Textid5 = reader.GetInt32("textid5"),
                            Emote = reader.GetUInt32("emote"),
                            Spell = reader.GetUInt32("spell"),
                            Orientation = reader.GetFloat("orientation"),
                            Model1 = reader.GetUInt32("model1"),
                            Model2 = reader.GetUInt32("model2")
                        };
                        movements.Add(movement);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return movements;
        }

        public static List<CreatureMovementScript> GetCreatureMovementScripts()
        {
            List<CreatureMovementScript> movementScripts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_movement_scripts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureMovementScript movementScript = new()
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
                        movementScripts.Add(movementScript);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return movementScripts;
        }

        public static List<CreatureMovementSpecial> GetCreatureMovementSpecials()
        {
            List<CreatureMovementSpecial> movementSpecials = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_movement_special";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureMovementSpecial movementSpecial = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Point = reader.GetUInt32("point"),
                            PositionX = reader.GetFloat("position_x"),
                            PositionY = reader.GetFloat("position_y"),
                            PositionZ = reader.GetFloat("position_z"),
                            Waittime = reader.GetUInt32("waittime"),
                            ScriptId = reader.GetUInt32("script_id"),
                            Textid1 = reader.GetUInt32("textid1"),
                            Textid2 = reader.GetUInt32("textid2"),
                            Textid3 = reader.GetUInt32("textid3"),
                            Textid4 = reader.GetUInt32("textid4"),
                            Textid5 = reader.GetUInt32("textid5"),
                            Emote = reader.GetUInt32("emote"),
                            Spell = reader.GetUInt32("spell"),
                            Orientation = reader.GetFloat("orientation"),
                            Model1 = reader.GetUInt32("model1"),
                            Model2 = reader.GetUInt32("model2")
                        };
                        movementSpecials.Add(movementSpecial);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return movementSpecials;
        }

        public static List<CreatureMovementTemplate> GetCreatureMovementTemplates()
        {
            List<CreatureMovementTemplate> movementTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_movement_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureMovementTemplate movementTemplate = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Point = reader.GetUInt32("point"),
                            PositionX = reader.GetFloat("position_x"),
                            PositionY = reader.GetFloat("position_y"),
                            PositionZ = reader.GetFloat("position_z"),
                            Waittime = reader.GetUInt32("waittime"),
                            ScriptId = reader.GetUInt32("script_id"),
                            Textid1 = reader.GetUInt32("textid1"),
                            Textid2 = reader.GetUInt32("textid2"),
                            Textid3 = reader.GetUInt32("textid3"),
                            Textid4 = reader.GetUInt32("textid4"),
                            Textid5 = reader.GetUInt32("textid5"),
                            Emote = reader.GetUInt32("emote"),
                            Spell = reader.GetUInt32("spell"),
                            Orientation = reader.GetFloat("orientation"),
                            Model1 = reader.GetUInt32("model1"),
                            Model2 = reader.GetUInt32("model2")
                        };
                        movementTemplates.Add(movementTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return movementTemplates;
        }

        public static List<CreatureOnKillReputation> GetCreatureOnKillReputations()
        {
            List<CreatureOnKillReputation> reputations = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_onkill_reputation";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureOnKillReputation reputation = new()
                        {
                            CreatureId = reader.GetUInt32("creature_id"),
                            RewOnKillRepFaction1 = reader.GetInt16("RewOnKillRepFaction1"),
                            RewOnKillRepFaction2 = reader.GetInt16("RewOnKillRepFaction2"),
                            MaxStanding1 = reader.GetSByte("MaxStanding1"),
                            IsTeamAward1 = reader.GetBoolean("IsTeamAward1"),
                            RewOnKillRepValue1 = reader.GetInt32("RewOnKillRepValue1"),
                            MaxStanding2 = reader.GetSByte("MaxStanding2"),
                            IsTeamAward2 = reader.GetBoolean("IsTeamAward2"),
                            RewOnKillRepValue2 = reader.GetInt32("RewOnKillRepValue2"),
                            TeamDependent = reader.GetBoolean("TeamDependent")
                        };
                        reputations.Add(reputation);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return reputations;
        }

        public static List<CreatureQuestRelation> GetCreatureQuestRelations()
        {
            List<CreatureQuestRelation> creatureQuestRelations = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_questrelation";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureQuestRelation creatureQuestRelation = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Quest = reader.GetUInt32("quest"),
                            Patch = reader.GetByte("patch")
                        };
                        creatureQuestRelations.Add(creatureQuestRelation);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return creatureQuestRelations;
        }

        public static List<CreatureSpell> GetCreatureSpells()
        {
            List<CreatureSpell> creatureSpells = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_spells";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureSpell creatureSpell = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Name = reader.GetString("name"),

                            SpellId1 = reader.GetUInt16("spellId_1"),
                            Probability1 = reader.GetByte("probability_1"),
                            CastTarget1 = reader.GetByte("castTarget_1"),
                            TargetParam11 = reader.GetUInt16("targetParam1_1"),
                            TargetParam21 = reader.GetUInt16("targetParam2_1"),
                            CastFlags1 = reader.GetByte("castFlags_1"),
                            DelayInitialMin1 = reader.GetUInt16("delayInitialMin_1"),
                            DelayInitialMax1 = reader.GetUInt16("delayInitialMax_1"),
                            DelayRepeatMin1 = reader.GetUInt16("delayRepeatMin_1"),
                            DelayRepeatMax1 = reader.GetUInt16("delayRepeatMax_1"),
                            ScriptId1 = reader.GetUInt32("scriptId_1"),

                            SpellId2 = reader.GetUInt16("spellId_2"),
                            Probability2 = reader.GetByte("probability_2"),
                            CastTarget2 = reader.GetByte("castTarget_2"),
                            TargetParam12 = reader.GetUInt16("targetParam1_2"),
                            TargetParam22 = reader.GetUInt16("targetParam2_2"),
                            CastFlags2 = reader.GetByte("castFlags_2"),
                            DelayInitialMin2 = reader.GetUInt16("delayInitialMin_2"),
                            DelayInitialMax2 = reader.GetUInt16("delayInitialMax_2"),
                            DelayRepeatMin2 = reader.GetUInt16("delayRepeatMin_2"),
                            DelayRepeatMax2 = reader.GetUInt16("delayRepeatMax_2"),
                            ScriptId2 = reader.GetUInt32("scriptId_2"),

                            SpellId3 = reader.GetUInt16("spellId_3"),
                            Probability3 = reader.GetByte("probability_3"),
                            CastTarget3 = reader.GetByte("castTarget_3"),
                            TargetParam13 = reader.GetUInt16("targetParam1_3"),
                            TargetParam23 = reader.GetUInt16("targetParam2_3"),
                            CastFlags3 = reader.GetByte("castFlags_3"),
                            DelayInitialMin3 = reader.GetUInt16("delayInitialMin_3"),
                            DelayInitialMax3 = reader.GetUInt16("delayInitialMax_3"),
                            DelayRepeatMin3 = reader.GetUInt16("delayRepeatMin_3"),
                            DelayRepeatMax3 = reader.GetUInt16("delayRepeatMax_3"),
                            ScriptId3 = reader.GetUInt32("scriptId_3"),

                            SpellId4 = reader.GetUInt16("spellId_4"),
                            Probability4 = reader.GetByte("probability_4"),
                            CastTarget4 = reader.GetByte("castTarget_4"),
                            TargetParam14 = reader.GetUInt16("targetParam1_4"),
                            TargetParam24 = reader.GetUInt16("targetParam2_4"),
                            CastFlags4 = reader.GetByte("castFlags_4"),
                            DelayInitialMin4 = reader.GetUInt16("delayInitialMin_4"),
                            DelayInitialMax4 = reader.GetUInt16("delayInitialMax_4"),
                            DelayRepeatMin4 = reader.GetUInt16("delayRepeatMin_4"),
                            DelayRepeatMax4 = reader.GetUInt16("delayRepeatMax_4"),
                            ScriptId4 = reader.GetUInt32("scriptId_4"),

                            SpellId5 = reader.GetUInt16("spellId_5"),
                            Probability5 = reader.GetByte("probability_5"),
                            CastTarget5 = reader.GetByte("castTarget_5"),
                            TargetParam15 = reader.GetUInt16("targetParam1_5"),
                            TargetParam25 = reader.GetUInt16("targetParam2_5"),
                            CastFlags5 = reader.GetByte("castFlags_5"),
                            DelayInitialMin5 = reader.GetUInt16("delayInitialMin_5"),
                            DelayInitialMax5 = reader.GetUInt16("delayInitialMax_5"),
                            DelayRepeatMin5 = reader.GetUInt16("delayRepeatMin_5"),
                            DelayRepeatMax5 = reader.GetUInt16("delayRepeatMax_5"),
                            ScriptId5 = reader.GetUInt32("scriptId_5"),

                            SpellId6 = reader.GetUInt16("spellId_6"),
                            Probability6 = reader.GetByte("probability_6"),
                            CastTarget6 = reader.GetByte("castTarget_6"),
                            TargetParam16 = reader.GetUInt16("targetParam1_6"),
                            TargetParam26 = reader.GetUInt16("targetParam2_6"),
                            CastFlags6 = reader.GetByte("castFlags_6"),
                            DelayInitialMin6 = reader.GetUInt16("delayInitialMin_6"),
                            DelayInitialMax6 = reader.GetUInt16("delayInitialMax_6"),
                            DelayRepeatMin6 = reader.GetUInt16("delayRepeatMin_6"),
                            DelayRepeatMax6 = reader.GetUInt16("delayRepeatMax_6"),
                            ScriptId6 = reader.GetUInt32("scriptId_6"),

                            SpellId7 = reader.GetUInt16("spellId_7"),
                            Probability7 = reader.GetByte("probability_7"),
                            CastTarget7 = reader.GetByte("castTarget_7"),
                            TargetParam17 = reader.GetUInt16("targetParam1_7"),
                            TargetParam27 = reader.GetUInt16("targetParam2_7"),
                            CastFlags7 = reader.GetByte("castFlags_7"),
                            DelayInitialMin7 = reader.GetUInt16("delayInitialMin_7"),
                            DelayInitialMax7 = reader.GetUInt16("delayInitialMax_7"),
                            DelayRepeatMin7 = reader.GetUInt16("delayRepeatMin_7"),
                            DelayRepeatMax7 = reader.GetUInt16("delayRepeatMax_7"),
                            ScriptId7 = reader.GetUInt32("scriptId_7"),

                            SpellId8 = reader.GetUInt16("spellId_8"),
                            Probability8 = reader.GetByte("probability_8"),
                            CastTarget8 = reader.GetByte("castTarget_8"),
                            TargetParam18 = reader.GetUInt16("targetParam1_8"),
                            TargetParam28 = reader.GetUInt16("targetParam2_8"),
                            CastFlags8 = reader.GetByte("castFlags_8"),
                            DelayInitialMin8 = reader.GetUInt16("delayInitialMin_8"),
                            DelayInitialMax8 = reader.GetUInt16("delayInitialMax_8"),
                            DelayRepeatMin8 = reader.GetUInt16("delayRepeatMin_8"),
                            DelayRepeatMax8 = reader.GetUInt16("delayRepeatMax_8"),
                            ScriptId8 = reader.GetUInt32("scriptId_8")
                        };
                        creatureSpells.Add(creatureSpell);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return creatureSpells;
        }

        public static List<CreatureSpellScript> GetCreatureSpellsScripts()
        {
            List<CreatureSpellScript> creatureSpellsScripts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_spells_scripts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureSpellScript creatureSpellsScript = new()
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
                        creatureSpellsScripts.Add(creatureSpellsScript);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return creatureSpellsScripts;
        }

        public static List<CreatureTemplate> GetCreatureTemplates()
        {
            List<CreatureTemplate> creatureTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureTemplate creatureTemplate = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Patch = reader.GetByte("patch"),
                            KillCredit1 = reader.GetUInt32("KillCredit1"),
                            KillCredit2 = reader.GetUInt32("KillCredit2"),
                            ModelId1 = reader.GetUInt32("modelid_1"),
                            ModelId2 = reader.GetUInt32("modelid_2"),
                            ModelId3 = reader.GetUInt32("modelid_3"),
                            ModelId4 = reader.GetUInt32("modelid_4"),
                            Name = reader.GetString("name"),
                            Subname = reader.IsDBNull("subname") ? string.Empty : reader.GetString("subname"),
                            GossipMenuId = reader.GetUInt32("gossip_menu_id"),
                            MinLevel = reader.GetByte("minlevel"),
                            MaxLevel = reader.GetByte("maxlevel"),
                            MinHealth = reader.GetUInt32("minhealth"),
                            MaxHealth = reader.GetUInt32("maxhealth"),
                            MinMana = reader.GetUInt32("minmana"),
                            MaxMana = reader.GetUInt32("maxmana"),
                            Armor = reader.GetUInt32("armor"),
                            FactionA = reader.GetUInt16("faction_A"),
                            FactionH = reader.GetUInt16("faction_H"),
                            NpcFlag = reader.GetUInt32("npcflag"),
                            SpeedWalk = reader.GetFloat("speed_walk"),
                            SpeedRun = reader.GetFloat("speed_run"),
                            Scale = reader.GetFloat("scale"),
                            Rank = reader.GetByte("rank"),
                            MinDmg = reader.GetFloat("mindmg"),
                            MaxDmg = reader.GetFloat("maxdmg"),
                            DmgSchool = reader.GetByte("dmgschool"),
                            AttackPower = reader.GetUInt32("attackpower"),
                            DmgMultiplier = reader.GetFloat("dmg_multiplier"),
                            BaseAttackTime = reader.GetUInt32("baseattacktime"),
                            RangeAttackTime = reader.GetUInt32("rangeattacktime"),
                            UnitClass = reader.GetByte("unit_class"),
                            UnitFlags = reader.GetUInt32("unit_flags"),
                            DynamicFlags = reader.GetUInt32("dynamicflags"),
                            Family = reader.GetByte("family"),
                            TrainerType = reader.GetByte("trainer_type"),
                            TrainerSpell = reader.GetUInt32("trainer_spell"),
                            TrainerClass = reader.GetByte("trainer_class"),
                            TrainerRace = reader.GetByte("trainer_race"),
                            MinRangedDmg = reader.GetFloat("minrangedmg"),
                            MaxRangedDmg = reader.GetFloat("maxrangedmg"),
                            RangedAttackPower = reader.GetUInt16("rangedattackpower"),
                            Type = reader.GetByte("type"),
                            TypeFlags = reader.GetUInt32("type_flags"),
                            LootId = reader.GetUInt32("lootid"),
                            PickpocketLoot = reader.GetUInt32("pickpocketloot"),
                            SkinLoot = reader.GetUInt32("skinloot"),
                            Resistance1 = reader.GetInt16("resistance1"),
                            Resistance2 = reader.GetInt16("resistance2"),
                            Resistance3 = reader.GetInt16("resistance3"),
                            Resistance4 = reader.GetInt16("resistance4"),
                            Resistance5 = reader.GetInt16("resistance5"),
                            Resistance6 = reader.GetInt16("resistance6"),
                            Spell1 = reader.GetUInt32("spell1"),
                            Spell2 = reader.GetUInt32("spell2"),
                            Spell3 = reader.GetUInt32("spell3"),
                            Spell4 = reader.GetUInt32("spell4"),
                            SpellsTemplate = reader.GetUInt32("spells_template"),
                            PetSpellDataId = reader.GetUInt32("PetSpellDataId"),
                            MinGold = reader.GetUInt32("mingold"),
                            MaxGold = reader.GetUInt32("maxgold"),
                            AiName = reader.GetString("AIName"),
                            MovementType = reader.GetByte("MovementType"),
                            InhabitType = reader.GetByte("InhabitType"),
                            Civilian = reader.GetByte("Civilian"),
                            RacialLeader = reader.GetByte("RacialLeader"),
                            RegenHealth = reader.GetByte("RegenHealth"),
                            EquipmentId = reader.GetUInt32("equipment_id"),
                            TrainerId = reader.GetUInt32("trainer_id"),
                            VendorId = reader.GetUInt32("vendor_id"),
                            MechanicImmuneMask = reader.GetUInt32("MechanicImmuneMask"),
                            SchoolImmuneMask = reader.GetUInt32("SchoolImmuneMask"),
                            FlagsExtra = reader.GetUInt32("flags_extra"),
                            ScriptName = reader.GetString("ScriptName")
                        };
                        creatureTemplates.Add(creatureTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return creatureTemplates;
        }

        public static List<CreatureTemplateAddon> GetCreatureTemplateAddons()
        {
            List<CreatureTemplateAddon> creatureTemplateAddons = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM creature_template_addon";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CreatureTemplateAddon creatureTemplateAddon = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Patch = reader.GetByte("patch"),
                            Mount = reader.GetUInt32("mount"),
                            Bytes1 = reader.GetUInt32("bytes1"),
                            B20Sheath = reader.GetByte("b2_0_sheath"),
                            B21Flags = reader.GetByte("b2_1_flags"),
                            Emote = reader.GetUInt32("emote"),
                            Moveflags = reader.GetUInt32("moveflags"),
                            Auras = reader.IsDBNull("auras") ? string.Empty : reader.GetString("auras")
                        };
                        creatureTemplateAddons.Add(creatureTemplateAddon);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return creatureTemplateAddons;
        }
    }
}
