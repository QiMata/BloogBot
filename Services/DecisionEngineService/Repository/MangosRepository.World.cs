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
        public static List<AreaTriggerBgEntrance> GetAreaTriggerBgEntrances()
        {
            List<AreaTriggerBgEntrance> areaTriggers = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM areatrigger_bg_entrance";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        AreaTriggerBgEntrance areaTrigger = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Name = reader.IsDBNull("name") ? string.Empty : reader.GetString("name"),
                            Team = reader.GetUInt32("team"),
                            BgTemplate = reader.GetUInt32("bg_template"),
                            ExitMap = reader.GetFloat("exit_map"),
                            ExitPositionX = reader.GetFloat("exit_position_x"),
                            ExitPositionY = reader.GetFloat("exit_position_y"),
                            ExitPositionZ = reader.GetFloat("exit_position_z"),
                            ExitOrientation = reader.GetFloat("exit_orientation")
                        };
                        areaTriggers.Add(areaTrigger);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return areaTriggers;
        }

        public static List<AreaTriggerInvolvedRelation> GetAreaTriggerInvolvedRelations()
        {
            List<AreaTriggerInvolvedRelation> areaTriggers = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM areatrigger_involvedrelation";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        AreaTriggerInvolvedRelation areaTrigger = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Quest = reader.GetUInt32("quest")
                        };
                        areaTriggers.Add(areaTrigger);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return areaTriggers;
        }

        public static List<AreaTriggerTavern> GetAreaTriggerTaverns()
        {
            List<AreaTriggerTavern> areaTriggerTaverns = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM areatrigger_tavern";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        AreaTriggerTavern areaTriggerTavern = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Name = reader.IsDBNull("name") ? string.Empty : reader.GetString("name")
                        };
                        areaTriggerTaverns.Add(areaTriggerTavern);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return areaTriggerTaverns;
        }

        public static List<AreaTriggerTeleport> GetAreaTriggerTeleports()
        {
            List<AreaTriggerTeleport> areaTriggerTeleports = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM areatrigger_teleport";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        AreaTriggerTeleport areaTriggerTeleport = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Patch = reader.GetUInt32("patch"),
                            Name = reader.IsDBNull("name") ? string.Empty : reader.GetString("name"),
                            RequiredLevel = reader.GetUInt32("required_level"),
                            RequiredItem = reader.GetUInt32("required_item"),
                            RequiredItem2 = reader.GetUInt32("required_item2"),
                            RequiredQuestDone = reader.GetUInt32("required_quest_done"),
                            RequiredEvent = reader.GetInt32("required_event"),
                            RequiredPvpRank = reader.GetUInt32("required_pvp_rank"),
                            RequiredTeam = reader.GetUInt32("required_team"),
                            RequiredFailedText = reader.IsDBNull("required_failed_text") ? string.Empty : reader.GetString("required_failed_text"),
                            TargetMap = reader.GetUInt32("target_map"),
                            TargetPositionX = reader.GetFloat("target_position_x"),
                            TargetPositionY = reader.GetFloat("target_position_y"),
                            TargetPositionZ = reader.GetFloat("target_position_z"),
                            TargetOrientation = reader.GetFloat("target_orientation")
                        };
                        areaTriggerTeleports.Add(areaTriggerTeleport);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return areaTriggerTeleports;
        }

        public static List<AreaTemplate> GetAreaTemplates()
        {
            List<AreaTemplate> areaTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM area_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        AreaTemplate areaTemplate = new()
                        {
                            Entry = reader.GetUInt32("Entry"),
                            MapId = reader.GetUInt32("MapId"),
                            ZoneId = reader.GetUInt32("ZoneId"),
                            ExploreFlag = reader.GetUInt32("ExploreFlag"),
                            Flags = reader.GetUInt32("Flags"),
                            AreaLevel = reader.GetInt32("AreaLevel"),
                            Name = reader.IsDBNull("Name") ? string.Empty : reader.GetString("Name"),
                            Team = reader.GetUInt32("Team"),
                            LiquidTypeId = reader.GetUInt32("LiquidTypeId")
                        };
                        areaTemplates.Add(areaTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return areaTemplates;
        }

        public static List<BattlegroundEvent> GetBattlegroundEvents()
        {
            List<BattlegroundEvent> battlegroundEvents = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM battleground_events";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        BattlegroundEvent battlegroundEvent = new()
                        {
                            Map = reader.GetInt32("map"),
                            Event1 = reader.GetUInt32("event1"),
                            Event2 = reader.GetUInt32("event2"),
                            Description = reader.GetString("description")
                        };
                        battlegroundEvents.Add(battlegroundEvent);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return battlegroundEvents;
        }

        public static List<BattlegroundTemplate> GetBattlegroundTemplates()
        {
            List<BattlegroundTemplate> battlegroundTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT * FROM battleground_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        BattlegroundTemplate battlegroundTemplate = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Patch = reader.GetUInt32("patch"),
                            MinPlayersPerTeam = reader.GetUInt32("MinPlayersPerTeam"),
                            MaxPlayersPerTeam = reader.GetUInt32("MaxPlayersPerTeam"),
                            MinLvl = reader.GetUInt32("MinLvl"),
                            MaxLvl = reader.GetUInt32("MaxLvl"),
                            AllianceWinSpell = reader.GetUInt32("AllianceWinSpell"),
                            AllianceLoseSpell = reader.GetUInt32("AllianceLoseSpell"),
                            HordeWinSpell = reader.GetUInt32("HordeWinSpell"),
                            HordeLoseSpell = reader.GetUInt32("HordeLoseSpell"),
                            AllianceStartLoc = reader.GetUInt32("AllianceStartLoc"),
                            AllianceStartO = reader.GetFloat("AllianceStartO"),
                            HordeStartLoc = reader.GetUInt32("HordeStartLoc"),
                            HordeStartO = reader.GetFloat("HordeStartO")
                        };
                        battlegroundTemplates.Add(battlegroundTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return battlegroundTemplates;
        }

        public static List<BattlemasterEntry> GetBattlemasterEntries()
        {
            List<BattlemasterEntry> battlemasterEntries = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM battlemaster_entry";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        BattlemasterEntry battlemasterEntry = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            BgTemplate = reader.GetUInt32("bg_template")
                        };
                        battlemasterEntries.Add(battlemasterEntry);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return battlemasterEntries;
        }

        public static List<GameObject> GetGameObjects()
        {
            List<GameObject> gameObjects = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gameobject";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gameObject = new GameObject
                        {
                            Guid = reader.GetUInt32("guid"),
                            Id = reader.GetUInt32("id"),
                            Map = reader.GetUInt16("map"),
                            PositionX = reader.GetFloat("position_x"),
                            PositionY = reader.GetFloat("position_y"),
                            PositionZ = reader.GetFloat("position_z"),
                            Orientation = reader.GetFloat("orientation"),
                            Rotation0 = reader.GetFloat("rotation0"),
                            Rotation1 = reader.GetFloat("rotation1"),
                            Rotation2 = reader.GetFloat("rotation2"),
                            Rotation3 = reader.GetFloat("rotation3"),
                            Spawntimesecsmin = reader.GetInt32("spawntimesecsmin"),
                            Spawntimesecsmax = reader.GetInt32("spawntimesecsmax"),
                            Animprogress = reader.GetByte("animprogress"),
                            State = reader.GetByte("state"),
                            SpawnFlags = reader.GetUInt32("spawnFlags"),
                            Visibilitymod = reader.GetFloat("visibilitymod"),
                            PatchMin = reader.GetByte("patch_min"),
                            PatchMax = reader.GetByte("patch_max")
                        };

                        gameObjects.Add(gameObject);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameObjects;
        }

        public static List<GameObjectBattleground> GetGameObjectBattlegrounds()
        {
            List<GameObjectBattleground> gameObjectBattlegrounds = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gameobject_battleground";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gameObjectBattleground = new GameObjectBattleground
                        {
                            Guid = reader.GetUInt32("guid"),
                            Event1 = reader.GetByte("event1"),
                            Event2 = reader.GetByte("event2")
                        };

                        gameObjectBattlegrounds.Add(gameObjectBattleground);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameObjectBattlegrounds;
        }

        public static List<GameObjectInvolvedRelation> GetGameObjectInvolvedRelations()
        {
            List<GameObjectInvolvedRelation> relations = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gameobject_involvedrelation";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        GameObjectInvolvedRelation relation = new()
                        {
                            Id = reader.GetUInt32("id"),
                            Quest = reader.GetUInt32("quest"),
                            Patch = reader.GetByte("patch")
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

        public static List<GameObjectLootTemplate> GetGameObjectLootTemplates()
        {
            List<GameObjectLootTemplate> lootTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gameobject_loot_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var lootTemplate = new GameObjectLootTemplate
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

        public static List<GameObjectQuestRelation> GetGameObjectQuestRelations()
        {
            List<GameObjectQuestRelation> questRelations = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gameobject_questrelation";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var questRelation = new GameObjectQuestRelation
                        {
                            Id = reader.GetUInt32("id"),
                            Quest = reader.GetUInt32("quest"),
                            Patch = reader.GetByte("patch")
                        };

                        questRelations.Add(questRelation);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return questRelations;
        }

        public static List<GameObjectRequirement> GetGameObjectRequirements()
        {
            List<GameObjectRequirement> requirements = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gameobject_requirement";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var requirement = new GameObjectRequirement
                        {
                            Guid = reader.GetUInt32("guid"),
                            ReqType = reader.GetUInt32("reqType"),
                            ReqGuid = reader.GetUInt32("reqGuid")
                        };

                        requirements.Add(requirement);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return requirements;
        }

        public static List<GameObjectScript> GetGameObjectScripts()
        {
            List<GameObjectScript> scripts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gameobject_scripts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var script = new GameObjectScript
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

                        scripts.Add(script);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return scripts;
        }

        public static List<GameObjectTemplate> GetGameObjectTemplates()
        {
            List<GameObjectTemplate> gameObjectTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gameobject_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var template = new GameObjectTemplate
                        {
                            Entry = reader.GetUInt32("entry"),
                            Patch = reader.GetByte("patch"),
                            Type = reader.GetByte("type"),
                            DisplayId = reader.GetUInt32("displayId"),
                            Name = reader.GetString("name"),
                            Faction = reader.GetUInt16("faction"),
                            Flags = reader.GetUInt32("flags"),
                            Size = reader.GetFloat("size"),
                            Mingold = reader.GetUInt32("mingold"),
                            Maxgold = reader.GetUInt32("maxgold"),
                            ScriptName = reader.GetString("ScriptName")
                        };

                        template.Data.Add(reader.GetUInt32("data0"));
                        template.Data.Add(reader.GetUInt32("data1"));
                        template.Data.Add(reader.GetUInt32("data2"));
                        template.Data.Add(reader.GetUInt32("data3"));
                        template.Data.Add(reader.GetUInt32("data4"));
                        template.Data.Add(reader.GetUInt32("data5"));
                        template.Data.Add(reader.GetUInt32("data6"));
                        template.Data.Add(reader.GetUInt32("data7"));
                        template.Data.Add(reader.GetUInt32("data8"));
                        template.Data.Add(reader.GetUInt32("data9"));
                        template.Data.Add(reader.GetUInt32("data10"));
                        template.Data.Add(reader.GetUInt32("data11"));
                        template.Data.Add(reader.GetUInt32("data12"));
                        template.Data.Add(reader.GetUInt32("data13"));
                        template.Data.Add(reader.GetUInt32("data14"));
                        template.Data.Add(reader.GetUInt32("data15"));
                        template.Data.Add(reader.GetUInt32("data16"));
                        template.Data.Add(reader.GetUInt32("data17"));
                        template.Data.Add(reader.GetUInt32("data18"));
                        template.Data.Add(reader.GetUInt32("data19"));
                        template.Data.Add(reader.GetUInt32("data20"));
                        template.Data.Add(reader.GetUInt32("data21"));
                        template.Data.Add(reader.GetUInt32("data22"));
                        template.Data.Add(reader.GetUInt32("data23"));

                        gameObjectTemplates.Add(template);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameObjectTemplates;
        }

        public static List<GameEvent> GetGameEvents()
        {
            List<GameEvent> gameEvents = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM game_event";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gameEvent = new GameEvent
                        {
                            Entry = reader.GetUInt32("entry"),
                            StartTime = GetTimestampSafe(reader, "start_time"),
                            EndTime = GetTimestampSafe(reader, "end_time"),
                            Occurrence = reader.GetUInt64("occurence"),
                            Length = reader.GetUInt64("length"),
                            Holiday = reader.GetUInt32("holiday"),
                            Description = reader.IsDBNull("description") ? string.Empty : reader.GetString("description"),
                            Hardcoded = reader.GetBoolean("hardcoded"),
                            Disabled = reader.GetBoolean("disabled"),
                            PatchMin = reader.GetByte("patch_min"),
                            PatchMax = reader.GetByte("patch_max")
                        };

                        gameEvents.Add(gameEvent);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameEvents;
        }

        public static List<GameEventCreature> GetGameEventCreatures()
        {
            List<GameEventCreature> gameEventCreatures = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM game_event_creature";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gameEventCreature = new GameEventCreature
                        {
                            Guid = reader.GetUInt32("guid"),
                            Event = reader.GetInt16("event")
                        };

                        gameEventCreatures.Add(gameEventCreature);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameEventCreatures;
        }

        public static List<GameEventCreatureData> GetGameEventCreatureDatas()
        {
            List<GameEventCreatureData> gameEventCreatureDataList = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM game_event_creature_data";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gameEventCreatureData = new GameEventCreatureData
                        {
                            Guid = reader.GetUInt32("guid"),
                            EntryId = reader.GetUInt32("entry_id"),
                            Modelid = reader.GetUInt32("modelid"),
                            EquipmentId = reader.GetUInt32("equipment_id"),
                            SpellStart = reader.GetUInt32("spell_start"),
                            SpellEnd = reader.GetUInt32("spell_end"),
                            Event = reader.GetUInt16("event")
                        };

                        gameEventCreatureDataList.Add(gameEventCreatureData);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameEventCreatureDataList;
        }

        public static List<GameEventGameObject> GetGameEventGameObjects()
        {
            List<GameEventGameObject> gameEventGameObjectList = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM game_event_gameobject";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gameEventGameObject = new GameEventGameObject
                        {
                            Guid = reader.GetUInt32("guid"),
                            Event = reader.GetInt16("event")
                        };

                        gameEventGameObjectList.Add(gameEventGameObject);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameEventGameObjectList;
        }

        public static List<GameEventQuest> GetGameEventQuests()
        {
            List<GameEventQuest> gameEventQuestList = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM game_event_quest";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gameEventQuest = new GameEventQuest
                        {
                            Quest = reader.GetUInt32("quest"),
                            Event = reader.GetUInt16("event"),
                            Patch = reader.GetByte("patch")
                        };

                        gameEventQuestList.Add(gameEventQuest);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameEventQuestList;
        }

        public static List<GameGraveyardZone> GetGameGraveyardZones()
        {
            List<GameGraveyardZone> gameGraveyardZones = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM game_graveyard_zone";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gameGraveyardZone = new GameGraveyardZone
                        {
                            Id = reader.GetUInt32("id"),
                            GhostZone = reader.GetUInt32("ghost_zone"),
                            Faction = reader.GetUInt16("faction")
                        };

                        gameGraveyardZones.Add(gameGraveyardZone);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameGraveyardZones;
        }

        public static List<GameTele> GetGameTeles()
        {
            List<GameTele> gameTeles = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM game_tele";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gameTele = new GameTele
                        {
                            Id = reader.GetUInt32("id"),
                            PositionX = reader.GetFloat("position_x"),
                            PositionY = reader.GetFloat("position_y"),
                            PositionZ = reader.GetFloat("position_z"),
                            Orientation = reader.GetFloat("orientation"),
                            Map = reader.GetUInt16("map"),
                            Name = reader.GetString("name")
                        };

                        gameTeles.Add(gameTele);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameTeles;
        }

        public static List<GameWeather> GetGameWeathers()
        {
            List<GameWeather> gameWeathers = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM game_weather";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        GameWeather gameWeather = new()
                        {
                            Zone = reader.GetUInt32("zone"),
                            SpringRainChance = reader.GetByte("spring_rain_chance"),
                            SpringSnowChance = reader.GetByte("spring_snow_chance"),
                            SpringStormChance = reader.GetByte("spring_storm_chance"),
                            SummerRainChance = reader.GetByte("summer_rain_chance"),
                            SummerSnowChance = reader.GetByte("summer_snow_chance"),
                            SummerStormChance = reader.GetByte("summer_storm_chance"),
                            FallRainChance = reader.GetByte("fall_rain_chance"),
                            FallSnowChance = reader.GetByte("fall_snow_chance"),
                            FallStormChance = reader.GetByte("fall_storm_chance"),
                            WinterRainChance = reader.GetByte("winter_rain_chance"),
                            WinterSnowChance = reader.GetByte("winter_snow_chance"),
                            WinterStormChance = reader.GetByte("winter_storm_chance")
                        };
                        gameWeathers.Add(gameWeather);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gameWeathers;
        }

        public static List<MapTemplate> GetMapTemplates()
        {
            List<MapTemplate> mapTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM map_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var mapTemplate = new MapTemplate
                        {
                            Entry = reader.GetUInt16("Entry"),
                            Patch = reader.GetByte("patch"),
                            Parent = reader.GetUInt32("Parent"),
                            MapType = reader.GetByte("MapType"),
                            LinkedZone = reader.GetUInt32("LinkedZone"),
                            LevelMin = reader.GetByte("LevelMin"),
                            LevelMax = reader.GetByte("LevelMax"),
                            MaxPlayers = reader.GetByte("MaxPlayers"),
                            ResetDelay = reader.GetUInt32("ResetDelay"),
                            GhostEntranceMap = reader.GetInt16("GhostEntranceMap"),
                            GhostEntranceX = reader.GetFloat("GhostEntranceX"),
                            GhostEntranceY = reader.GetFloat("GhostEntranceY"),
                            MapName = reader.GetString("MapName"),
                            ScriptName = reader.GetString("ScriptName")
                        };

                        mapTemplates.Add(mapTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return mapTemplates;
        }

        public static List<ScriptedAreatrigger> GetScriptedAreaTriggers()
        {
            List<ScriptedAreatrigger> areaTriggers = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM scripted_areatrigger";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        ScriptedAreatrigger trigger = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            ScriptName = reader.GetString("ScriptName")
                        };

                        areaTriggers.Add(trigger);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return areaTriggers;
        }

        public static List<ScriptedEventId> GetScriptedEvents()
        {
            List<ScriptedEventId> events = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM scripted_event_id";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        ScriptedEventId scriptedEvent = new()
                        {
                            Id = reader.GetUInt32("id"),
                            ScriptName = reader.GetString("ScriptName")
                        };

                        events.Add(scriptedEvent);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return events;
        }

        public static List<ScriptEscortData> GetScriptEscortData()
        {
            List<ScriptEscortData> escortDataList = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM script_escort_data";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        ScriptEscortData escortData = new()
                        {
                            CreatureId = reader.GetUInt32("creature_id"),
                            Quest = reader.GetUInt32("quest"),
                            EscortFaction = reader.GetUInt32("escort_faction")
                        };

                        escortDataList.Add(escortData);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return escortDataList;
        }

        public static List<ScriptText> GetScriptTexts()
        {
            List<ScriptText> scriptTextList = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM script_texts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        ScriptText scriptText = new()
                        {
                            Entry = reader.GetInt32("entry"),
                            ContentDefault = reader.GetString("content_default"),
                            ContentLoc1 = reader.IsDBNull(reader.GetOrdinal("content_loc1")) ? string.Empty : reader.GetString("content_loc1"),
                            ContentLoc2 = reader.IsDBNull(reader.GetOrdinal("content_loc2")) ? string.Empty : reader.GetString("content_loc2"),
                            ContentLoc3 = reader.IsDBNull(reader.GetOrdinal("content_loc3")) ? string.Empty : reader.GetString("content_loc3"),
                            ContentLoc4 = reader.IsDBNull(reader.GetOrdinal("content_loc4")) ? string.Empty : reader.GetString("content_loc4"),
                            ContentLoc5 = reader.IsDBNull(reader.GetOrdinal("content_loc5")) ? string.Empty : reader.GetString("content_loc5"),
                            ContentLoc6 = reader.IsDBNull(reader.GetOrdinal("content_loc6")) ? string.Empty : reader.GetString("content_loc6"),
                            ContentLoc7 = reader.IsDBNull(reader.GetOrdinal("content_loc7")) ? string.Empty : reader.GetString("content_loc7"),
                            ContentLoc8 = reader.IsDBNull(reader.GetOrdinal("content_loc8")) ? string.Empty : reader.GetString("content_loc8"),
                            Sound = reader.GetUInt32("sound"),
                            Type = reader.GetUInt32("type"),
                            Language = reader.GetUInt32("language"),
                            Emote = reader.GetUInt32("emote"),
                            Comment = reader.IsDBNull(reader.GetOrdinal("comment")) ? string.Empty : reader.GetString("comment")
                        };

                        scriptTextList.Add(scriptText);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return scriptTextList;
        }

        public static List<ScriptWaypoint> GetScriptWaypoints()
        {
            List<ScriptWaypoint> waypoints = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM script_waypoint";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        ScriptWaypoint waypoint = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Pointid = reader.GetUInt32("pointid"),
                            LocationX = reader.GetFloat("location_x"),
                            LocationY = reader.GetFloat("location_y"),
                            LocationZ = reader.GetFloat("location_z"),
                            Waittime = reader.GetUInt32("waittime"),
                            PointComment = reader.IsDBNull(reader.GetOrdinal("point_comment")) ? string.Empty : reader.GetString("point_comment")
                        };

                        waypoints.Add(waypoint);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return waypoints;
        }

        public static List<TaxiPathTransition> GetTaxiPathTransitions()
        {
            List<TaxiPathTransition> taxiPathTransitions = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM taxi_path_transitions";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        TaxiPathTransition transition = new()
                        {
                            InPath = reader.GetUInt32("inPath"),
                            OutPath = reader.GetUInt32("outPath"),
                            InNode = reader.GetUInt32("inNode"),
                            OutNode = reader.GetUInt32("outNode"),
                            Comment = reader.IsDBNull("comment") ? string.Empty : reader.GetString("comment")
                        };

                        taxiPathTransitions.Add(transition);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return taxiPathTransitions;
        }

        public static List<Transport> GetTransports()
        {
            List<Transport> transports = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM transports";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Transport transport = new()
                        {
                            Guid = reader.GetUInt32("guid"),
                            Entry = reader.GetUInt32("entry"),
                            Name = reader.IsDBNull("name") ? string.Empty : reader.GetString("name"),
                            Period = reader.GetUInt32("period")
                        };

                        transports.Add(transport);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return transports;
        }
    }
}
