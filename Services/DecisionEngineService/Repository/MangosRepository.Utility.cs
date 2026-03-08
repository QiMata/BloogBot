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
        public static List<AuctionHouseBot> GetAuctionHouseBots()
        {
            List<AuctionHouseBot> auctionHouseBots = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM auctionhousebot";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        AuctionHouseBot auctionHouseBot = new()
                        {
                            Item = reader.GetInt32("item"),
                            Stack = reader.IsDBNull("stack") ? 0 : reader.GetInt32("stack"),
                            Bid = reader.IsDBNull("bid") ? 0 : reader.GetInt32("bid"),
                            Buyout = reader.IsDBNull("buyout") ? 0 : reader.GetInt32("buyout")
                        };
                        auctionHouseBots.Add(auctionHouseBot);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return auctionHouseBots;
        }

        public static List<AutoBroadcast> GetAutoBroadcasts()
        {
            List<AutoBroadcast> autoBroadcasts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM autobroadcast";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        AutoBroadcast autoBroadcast = new()
                        {
                            Delay = reader.IsDBNull("delay") ? 0 : reader.GetInt32("delay"),
                            StringId = reader.IsDBNull("stringId") ? 0 : reader.GetInt32("stringId"),
                            Comments = reader.IsDBNull("comments") ? string.Empty : reader.GetString("comments")
                        };
                        autoBroadcasts.Add(autoBroadcast);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return autoBroadcasts;
        }

        public static List<BroadcastText> GetBroadcastTexts()
        {
            List<BroadcastText> broadcastTexts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM broadcast_text";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        BroadcastText broadcastText = new()
                        {
                            Id = reader.GetUInt32("ID"),
                            MaleText = reader.IsDBNull("MaleText") ? string.Empty : reader.GetString("MaleText"),
                            FemaleText = reader.IsDBNull("FemaleText") ? string.Empty : reader.GetString("FemaleText"),
                            Sound = reader.GetUInt32("Sound"),
                            Type = reader.GetUInt32("Type"),
                            Language = reader.GetUInt32("Language"),
                            EmoteId0 = reader.GetUInt32("EmoteId0"),
                            EmoteId1 = reader.GetUInt32("EmoteId1"),
                            EmoteId2 = reader.GetUInt32("EmoteId2"),
                            EmoteDelay0 = reader.GetUInt32("EmoteDelay0"),
                            EmoteDelay1 = reader.GetUInt32("EmoteDelay1"),
                            EmoteDelay2 = reader.GetUInt32("EmoteDelay2")
                        };
                        broadcastTexts.Add(broadcastText);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return broadcastTexts;
        }

        public static List<CinematicWaypoint> GetCinematicWaypoints()
        {
            List<CinematicWaypoint> waypoints = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM cinematic_waypoints";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CinematicWaypoint waypoint = new()
                        {
                            Cinematic = reader.GetInt32("cinematic"),
                            Timer = reader.GetInt32("timer"),
                            Posx = reader.IsDBNull("posx") ? 0 : reader.GetFloat("posx"),
                            Posy = reader.IsDBNull("posy") ? 0 : reader.GetFloat("posy"),
                            Posz = reader.IsDBNull("posz") ? 0 : reader.GetFloat("posz"),
                            Comment = reader.IsDBNull("comment") ? string.Empty : reader.GetString("comment")
                        };
                        waypoints.Add(waypoint);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return waypoints;
        }

        public static List<Command> GetCommands()
        {
            List<Command> commandResults = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM command";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Command commandResult = new()
                        {
                            Name = reader.GetString("name"),
                            Security = reader.GetUInt32("security"),
                            Help = reader.IsDBNull("help") ? string.Empty : reader.GetString("help"),
                            Flags = reader.GetInt32("flags")
                        };
                        commandResults.Add(commandResult);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return commandResults;
        }

        public static List<Condition> GetConditions()
        {
            List<Condition> conditionResults = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM conditions";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Condition conditionResult = new()
                        {
                            ConditionEntry = reader.GetUInt32("condition_entry"),
                            Type = reader.GetInt32("type"),
                            Value1 = reader.GetUInt32("value1"),
                            Value2 = reader.GetUInt32("value2"),
                            Flags = reader.GetUInt32("flags")
                        };
                        conditionResults.Add(conditionResult);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[MANGOS REPO]{ex.Message} {ex.StackTrace}");
                }
            }

            return conditionResults;
        }

        public static List<CustomText> GetCustomTexts()
        {
            List<CustomText> customTexts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM custom_texts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        CustomText customText = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            ContentDefault = reader.GetString("content_default"),
                            ContentLoc1 = reader.IsDBNull("content_loc1") ? string.Empty : reader.GetString("content_loc1"),
                            ContentLoc2 = reader.IsDBNull("content_loc2") ? string.Empty : reader.GetString("content_loc2"),
                            ContentLoc3 = reader.IsDBNull("content_loc3") ? string.Empty : reader.GetString("content_loc3"),
                            ContentLoc4 = reader.IsDBNull("content_loc4") ? string.Empty : reader.GetString("content_loc4"),
                            ContentLoc5 = reader.IsDBNull("content_loc5") ? string.Empty : reader.GetString("content_loc5"),
                            ContentLoc6 = reader.IsDBNull("content_loc6") ? string.Empty : reader.GetString("content_loc6"),
                            ContentLoc7 = reader.IsDBNull("content_loc7") ? string.Empty : reader.GetString("content_loc7"),
                            ContentLoc8 = reader.IsDBNull("content_loc8") ? string.Empty : reader.GetString("content_loc8"),
                            Sound = reader.GetUInt32("sound"),
                            Type = reader.GetByte("type"),
                            Language = reader.GetByte("language"),
                            Emote = reader.GetUInt16("emote"),
                            Comment = reader.IsDBNull("comment") ? string.Empty : reader.GetString("comment")
                        };
                        customTexts.Add(customText);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return customTexts;
        }

        public static List<DisenchantLootTemplate> GetDisenchantLootTemplates()
        {
            List<DisenchantLootTemplate> disenchantLootTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM disenchant_loot_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        DisenchantLootTemplate disenchantLootTemplate = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Item = reader.GetUInt32("item"),
                            ChanceOrQuestChance = reader.GetFloat("ChanceOrQuestChance"),
                            Groupid = reader.GetByte("groupid"),
                            MincountOrRef = reader.GetUInt32("mincountOrRef"),
                            Maxcount = reader.GetByte("maxcount"),
                            ConditionId = reader.GetUInt32("condition_id"),
                            PatchMin = reader.GetByte("patch_min"),
                            PatchMax = reader.GetByte("patch_max")
                        };
                        disenchantLootTemplates.Add(disenchantLootTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return disenchantLootTemplates;
        }

        public static List<EventScript> GetEventScripts()
        {
            List<EventScript> eventScripts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM event_scripts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        EventScript eventScript = new()
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
                            Dataint = reader.GetUInt32("dataint"),
                            Dataint2 = reader.GetUInt32("dataint2"),
                            Dataint3 = reader.GetUInt32("dataint3"),
                            Dataint4 = reader.GetUInt32("dataint4"),
                            X = reader.GetFloat("x"),
                            Y = reader.GetFloat("y"),
                            Z = reader.GetFloat("z"),
                            O = reader.GetFloat("o"),
                            ConditionId = reader.GetUInt32("condition_id"),
                            Comments = reader.GetString("comments")
                        };
                        eventScripts.Add(eventScript);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return eventScripts;
        }

        public static List<ExplorationBaseXP> GetExplorationBaseXPs()
        {
            List<ExplorationBaseXP> explorationBaseXPs = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM exploration_basexp";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        ExplorationBaseXP explorationBaseXP = new()
                        {
                            Level = reader.GetByte("level"),
                            Basexp = reader.GetUInt32("basexp")
                        };
                        explorationBaseXPs.Add(explorationBaseXP);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return explorationBaseXPs;
        }

        public static List<Faction> GetFactions()
        {
            List<Faction> factions = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM faction";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Faction faction = new()
                        {
                            Id = reader.GetUInt32("ID"),
                            ReputationListID = reader.GetInt32("reputationListID"),
                            BaseRepRaceMask1 = reader.GetUInt32("baseRepRaceMask1"),
                            BaseRepRaceMask2 = reader.GetUInt32("baseRepRaceMask2"),
                            BaseRepRaceMask3 = reader.GetUInt32("baseRepRaceMask3"),
                            BaseRepRaceMask4 = reader.GetUInt32("baseRepRaceMask4"),
                            BaseRepClassMask1 = reader.GetUInt32("baseRepClassMask1"),
                            BaseRepClassMask2 = reader.GetUInt32("baseRepClassMask2"),
                            BaseRepClassMask3 = reader.GetUInt32("baseRepClassMask3"),
                            BaseRepClassMask4 = reader.GetUInt32("baseRepClassMask4"),
                            BaseRepValue1 = reader.GetInt32("baseRepValue1"),
                            BaseRepValue2 = reader.GetInt32("baseRepValue2"),
                            BaseRepValue3 = reader.GetInt32("baseRepValue3"),
                            BaseRepValue4 = reader.GetInt32("baseRepValue4"),
                            ReputationFlags1 = reader.GetUInt32("reputationFlags1"),
                            ReputationFlags2 = reader.GetUInt32("reputationFlags2"),
                            ReputationFlags3 = reader.GetUInt32("reputationFlags3"),
                            ReputationFlags4 = reader.GetUInt32("reputationFlags4"),
                            Team = reader.GetUInt32("team"),
                            Name1 = reader.GetString("name1"),
                            Name2 = reader.GetString("name2"),
                            Name3 = reader.GetString("name3"),
                            Name4 = reader.GetString("name4"),
                            Name5 = reader.GetString("name5"),
                            Name6 = reader.GetString("name6"),
                            Name7 = reader.GetString("name7"),
                            Name8 = reader.GetString("name8"),
                            Description1 = reader.GetString("description1"),
                            Description2 = reader.GetString("description2"),
                            Description3 = reader.GetString("description3"),
                            Description4 = reader.GetString("description4"),
                            Description5 = reader.GetString("description5"),
                            Description6 = reader.GetString("description6"),
                            Description7 = reader.GetString("description7"),
                            Description8 = reader.GetString("description8")
                        };
                        factions.Add(faction);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return factions;
        }

        public static List<FactionTemplate> GetFactionTemplates()
        {
            List<FactionTemplate> factionTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM faction_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        FactionTemplate factionTemplate = new()
                        {
                            Id = reader.GetUInt16("ID"),
                            FactionId = reader.GetUInt32("factionId"),
                            FactionFlags = reader.GetUInt32("factionFlags"),
                            OurMask = reader.GetUInt32("ourMask"),
                            FriendlyMask = reader.GetUInt32("friendlyMask"),
                            HostileMask = reader.GetUInt32("hostileMask"),
                            EnemyFaction1 = reader.GetUInt32("enemyFaction1"),
                            EnemyFaction2 = reader.GetUInt32("enemyFaction2"),
                            EnemyFaction3 = reader.GetUInt32("enemyFaction3"),
                            EnemyFaction4 = reader.GetUInt32("enemyFaction4"),
                            FriendFaction1 = reader.GetUInt32("friendFaction1"),
                            FriendFaction2 = reader.GetUInt32("friendFaction2"),
                            FriendFaction3 = reader.GetUInt32("friendFaction3"),
                            FriendFaction4 = reader.GetUInt32("friendFaction4")
                        };
                        factionTemplates.Add(factionTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return factionTemplates;
        }

        public static List<FishingLootTemplate> GetFishingLootTemplates()
        {
            List<FishingLootTemplate> fishingLootTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM fishing_loot_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var fishingLootTemplate = new FishingLootTemplate
                        {
                            Entry = reader.GetUInt32("entry"),
                            Item = reader.GetUInt32("item"),
                            ChanceOrQuestChance = reader.GetFloat("ChanceOrQuestChance"),
                            GroupId = reader.GetByte("groupid"),
                            MinCountOrRef = reader.GetUInt32("mincountOrRef"),
                            MaxCount = reader.GetByte("maxcount"),
                            ConditionId = reader.GetUInt32("condition_id"),
                            PatchMin = reader.GetByte("patch_min"),
                            PatchMax = reader.GetByte("patch_max")
                        };

                        fishingLootTemplates.Add(fishingLootTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return fishingLootTemplates;
        }

        public static List<GMSubSurvey> GetGMSubSurveys()
        {
            List<GMSubSurvey> gmSubSurveys = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gm_subsurveys";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gmSubSurvey = new GMSubSurvey
                        {
                            SurveyId = reader.GetUInt32("surveyId"),
                            SubsurveyId = reader.GetUInt32("subsurveyId"),
                            Rank = reader.GetUInt32("rank"),
                            Comment = reader.GetString("comment")
                        };

                        gmSubSurveys.Add(gmSubSurvey);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gmSubSurveys;
        }

        public static List<GMSurvey> GetGmSurveys()
        {
            List<GMSurvey> gmSurveys = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gm_surveys";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gmSurvey = new GMSurvey
                        {
                            SurveyId = reader.GetUInt32("surveyId"),
                            Guid = reader.GetUInt32("guid"),
                            MainSurvey = reader.GetUInt32("mainSurvey"),
                            OverallComment = reader.GetString("overallComment"),
                            CreateTime = reader.GetUInt32("createTime")
                        };

                        gmSurveys.Add(gmSurvey);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gmSurveys;
        }

        public static List<GMTicket> GetGmTickets()
        {
            List<GMTicket> gmTickets = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gm_tickets";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gmTicket = new GMTicket
                        {
                            TicketId = reader.GetUInt32("ticketId"),
                            Guid = reader.GetUInt32("guid"),
                            Name = reader.GetString("name"),
                            Message = reader.GetString("message"),
                            CreateTime = reader.GetUInt32("createTime"),
                            MapId = reader.GetUInt16("mapId"),
                            PosX = reader.GetFloat("posX"),
                            PosY = reader.GetFloat("posY"),
                            PosZ = reader.GetFloat("posZ"),
                            LastModifiedTime = reader.GetUInt32("lastModifiedTime"),
                            ClosedBy = reader.GetUInt32("closedBy"),
                            AssignedTo = reader.GetUInt32("assignedTo"),
                            Comment = reader.GetString("comment"),
                            Response = reader.GetString("response"),
                            Completed = reader.GetByte("completed"),
                            Escalated = reader.GetByte("escalated"),
                            Viewed = reader.GetByte("viewed"),
                            HaveTicket = reader.GetByte("haveTicket"),
                            TicketType = reader.GetByte("ticketType"),
                            SecurityNeeded = reader.GetByte("securityNeeded")
                        };

                        gmTickets.Add(gmTicket);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gmTickets;
        }

        public static List<InstanceBuffRemoval> GetInstanceBuffRemovals()
        {
            List<InstanceBuffRemoval> instanceBuffRemovals = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM instance_buff_removal";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var instanceBuffRemoval = new InstanceBuffRemoval
                        {
                            MapId = reader.GetUInt32("mapId"),
                            AuraId = reader.GetUInt32("auraId"),
                            Enabled = reader.GetBoolean("enabled"),
                            Flags = reader.GetUInt32("flags"),
                            Comment = reader.IsDBNull("comment") ? string.Empty : reader.GetString("comment")
                        };

                        instanceBuffRemovals.Add(instanceBuffRemoval);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return instanceBuffRemovals;
        }

        public static List<InstanceCreatureKills> GetInstanceCreatureKills()
        {
            List<InstanceCreatureKills> instanceCreatureKills = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM instance_creature_kills";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var instanceCreatureKill = new InstanceCreatureKills
                        {
                            MapId = reader.GetUInt32("mapId"),
                            CreatureEntry = reader.GetUInt32("creatureEntry"),
                            SpellEntry = reader.GetUInt32("spellEntry"),
                            Count = reader.GetUInt32("count")
                        };

                        instanceCreatureKills.Add(instanceCreatureKill);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return instanceCreatureKills;
        }

        public static List<InstanceCustomCounter> GetInstanceCustomCounters()
        {
            List<InstanceCustomCounter> instanceCustomCounters = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM instance_custom_counters";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var instanceCustomCounter = new InstanceCustomCounter
                        {
                            Index = reader.GetUInt32("index"),
                            Count = reader.GetUInt32("count")
                        };

                        instanceCustomCounters.Add(instanceCustomCounter);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return instanceCustomCounters;
        }

        public static List<InstanceWipe> GetInstanceWipes()
        {
            List<InstanceWipe> instanceWipes = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM instance_wipes";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var instanceWipe = new InstanceWipe
                        {
                            MapId = reader.GetUInt32("mapId"),
                            CreatureEntry = reader.GetUInt32("creatureEntry"),
                            Count = reader.GetUInt32("count")
                        };

                        instanceWipes.Add(instanceWipe);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return instanceWipes;
        }

        public static List<MailLootTemplate> GetMailLootTemplates()
        {
            List<MailLootTemplate> lootTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM mail_loot_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var lootTemplate = new MailLootTemplate
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

        public static List<MangosString> GetMangosStrings()
        {
            List<MangosString> mangosStrings = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM mangos_string";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var mangosString = new MangosString
                        {
                            Entry = reader.GetUInt32("entry"),
                            ContentDefault = reader.GetString("content_default"),
                            ContentLoc1 = reader.IsDBNull(reader.GetOrdinal("content_loc1")) ? string.Empty : reader.GetString("content_loc1"),
                            ContentLoc2 = reader.IsDBNull(reader.GetOrdinal("content_loc2")) ? string.Empty : reader.GetString("content_loc2"),
                            ContentLoc3 = reader.IsDBNull(reader.GetOrdinal("content_loc3")) ? string.Empty : reader.GetString("content_loc3"),
                            ContentLoc4 = reader.IsDBNull(reader.GetOrdinal("content_loc4")) ? string.Empty : reader.GetString("content_loc4"),
                            ContentLoc5 = reader.IsDBNull(reader.GetOrdinal("content_loc5")) ? string.Empty : reader.GetString("content_loc5"),
                            ContentLoc6 = reader.IsDBNull(reader.GetOrdinal("content_loc6")) ? string.Empty : reader.GetString("content_loc6"),
                            ContentLoc7 = reader.IsDBNull(reader.GetOrdinal("content_loc7")) ? string.Empty : reader.GetString("content_loc7"),
                            ContentLoc8 = reader.IsDBNull(reader.GetOrdinal("content_loc8")) ? string.Empty : reader.GetString("content_loc8")
                        };

                        mangosStrings.Add(mangosString);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return mangosStrings;
        }

        public static List<string> GetMigrations()
        {
            List<string> migrations = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM migrations";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var migrationId = reader.GetString("id");
                        migrations.Add(migrationId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return migrations;
        }

        public static List<PageText> GetPageText()
        {
            List<PageText> pageTexts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM page_text";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var pageText = new PageText
                        {
                            Entry = reader.GetUInt32("entry"),
                            Text = reader.GetString("text"),
                            NextPage = reader.GetUInt32("next_page")
                        };

                        pageTexts.Add(pageText);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return pageTexts;
        }

        public static List<PickpocketingLootTemplate> GetPickpocketingLoots()
        {
            List<PickpocketingLootTemplate> lootTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM pickpocketing_loot_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var lootTemplate = new PickpocketingLootTemplate
                        {
                            Entry = reader.GetUInt32("entry"),
                            Item = reader.GetUInt32("item"),
                            ChanceOrQuestChance = reader.GetFloat("ChanceOrQuestChance"),
                            Groupid = reader.GetByte("groupid"),
                            MincountOrRef = reader.GetUInt32("mincountOrRef"),
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

        public static List<PointsOfInterest> GetPointsOfInterest()
        {
            List<PointsOfInterest> pointsOfInterest = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM points_of_interest";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var pointOfInterest = new PointsOfInterest
                        {
                            Entry = reader.GetUInt32("entry"),
                            X = reader.GetFloat("x"),
                            Y = reader.GetFloat("y"),
                            Icon = reader.GetUInt32("icon"),
                            Flags = reader.GetUInt32("flags"),
                            Data = reader.GetUInt32("data"),
                            IconName = reader.GetString("icon_name")
                        };

                        pointsOfInterest.Add(pointOfInterest);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return pointsOfInterest;
        }

        public static List<PoolCreature> GetPoolCreatures()
        {
            List<PoolCreature> poolCreatures = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM pool_creature";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var poolCreature = new PoolCreature
                        {
                            Guid = reader.GetUInt32("guid"),
                            PoolEntry = reader.GetUInt32("pool_entry"),
                            Chance = reader.GetFloat("chance"),
                            Description = reader.GetString("description"),
                            Flags = reader.GetUInt32("flags"),
                            PatchMin = reader.GetByte("patch_min"),
                            PatchMax = reader.GetByte("patch_max")
                        };

                        poolCreatures.Add(poolCreature);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return poolCreatures;
        }

        public static List<PoolCreatureTemplate> GetPoolCreatureTemplates()
        {
            List<PoolCreatureTemplate> poolCreatureTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM pool_creature_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var poolCreatureTemplate = new PoolCreatureTemplate
                        {
                            Id = reader.GetUInt32("id"),
                            PoolEntry = reader.GetUInt32("pool_entry"),
                            Chance = reader.GetFloat("chance"),
                            Description = reader.GetString("description"),
                            Flags = reader.GetUInt32("flags")
                        };

                        poolCreatureTemplates.Add(poolCreatureTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return poolCreatureTemplates;
        }

        public static List<PoolGameObject> GetPoolGameObjects()
        {
            List<PoolGameObject> poolGameObjects = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM pool_gameobject";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var poolGameObject = new PoolGameObject
                        {
                            Guid = reader.GetUInt32("guid"),
                            PoolEntry = reader.GetUInt32("pool_entry"),
                            Chance = reader.GetFloat("chance"),
                            Description = reader.GetString("description"),
                            Flags = reader.GetUInt32("flags"),
                            PatchMin = reader.GetByte("patch_min"),
                            PatchMax = reader.GetByte("patch_max")
                        };

                        poolGameObjects.Add(poolGameObject);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return poolGameObjects;
        }

        public static List<PoolGameObjectTemplate> GetPoolGameObjectTemplates()
        {
            List<PoolGameObjectTemplate> poolGameObjectTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM pool_gameobject_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var poolGameObjectTemplate = new PoolGameObjectTemplate
                        {
                            Id = reader.GetUInt32("id"),
                            PoolEntry = reader.GetUInt32("pool_entry"),
                            Chance = reader.GetFloat("chance"),
                            Description = reader.GetString("description"),
                            Flags = reader.GetUInt32("flags")
                        };

                        poolGameObjectTemplates.Add(poolGameObjectTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return poolGameObjectTemplates;
        }

        public static List<PoolPool> GetPoolPools()
        {
            List<PoolPool> poolPools = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM pool_pool";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var poolPool = new PoolPool
                        {
                            PoolId = reader.GetUInt32("pool_id"),
                            MotherPool = reader.GetUInt32("mother_pool"),
                            Chance = reader.GetFloat("chance"),
                            Description = reader.GetString("description"),
                            Flags = reader.GetUInt32("flags")
                        };

                        poolPools.Add(poolPool);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return poolPools;
        }

        public static List<PoolTemplate> GetPoolTemplates()
        {
            List<PoolTemplate> poolTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM pool_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var poolTemplate = new PoolTemplate
                        {
                            Entry = reader.GetUInt32("entry"),
                            MaxLimit = reader.GetUInt32("max_limit"),
                            Description = reader.GetString("description"),
                            Flags = reader.GetUInt32("flags"),
                            Instance = reader.GetUInt32("instance"),
                            PatchMin = reader.GetByte("patch_min"),
                            PatchMax = reader.GetByte("patch_max")
                        };

                        poolTemplates.Add(poolTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return poolTemplates;
        }

        public static List<ReferenceLootTemplate> GetReferenceLootTemplates()
        {
            List<ReferenceLootTemplate> lootTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM reference_loot_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        ReferenceLootTemplate lootTemplate = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Item = reader.GetUInt32("item"),
                            ChanceOrQuestChance = reader.GetFloat("ChanceOrQuestChance"),
                            GroupId = reader.GetUInt32("groupid"),
                            MinCountOrRef = reader.GetInt32("mincountOrRef"),
                            MaxCount = reader.GetUInt32("maxcount"),
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

        public static List<ReputationRewardRate> GetReputationRewardRates()
        {
            List<ReputationRewardRate> reputationRewardRates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM reputation_reward_rate";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        ReputationRewardRate reputationRewardRate = new()
                        {
                            Faction = reader.GetUInt32("faction"),
                            QuestRate = reader.GetFloat("quest_rate"),
                            CreatureRate = reader.GetFloat("creature_rate"),
                            SpellRate = reader.GetFloat("spell_rate")
                        };
                        reputationRewardRates.Add(reputationRewardRate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return reputationRewardRates;
        }

        public static List<ReputationSpilloverTemplate> GetReputationSpilloverTemplate()
        {
            List<ReputationSpilloverTemplate> spilloverTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM reputation_spillover_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        ReputationSpilloverTemplate spilloverTemplate = new()
                        {
                            Faction = reader.GetUInt16("faction"),
                            Faction1 = reader.GetUInt16("faction1"),
                            Rate1 = reader.GetFloat("rate_1"),
                            Rank1 = reader.GetByte("rank_1"),
                            Faction2 = reader.GetUInt16("faction2"),
                            Rate2 = reader.GetFloat("rate_2"),
                            Rank2 = reader.GetByte("rank_2"),
                            Faction3 = reader.GetUInt16("faction3"),
                            Rate3 = reader.GetFloat("rate_3"),
                            Rank3 = reader.GetByte("rank_3"),
                            Faction4 = reader.GetUInt16("faction4"),
                            Rate4 = reader.GetFloat("rate_4"),
                            Rank4 = reader.GetByte("rank_4")
                        };
                        spilloverTemplates.Add(spilloverTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return spilloverTemplates;
        }

        public static List<string> GetReservedNames()
        {
            List<string> reservedNames = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT name FROM reserved_name";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        reservedNames.Add(reader.GetString("name"));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return reservedNames;
        }

        public static List<SkinningLootTemplate> GetSkinningLootTemplates()
        {
            List<SkinningLootTemplate> skinningLootTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM skinning_loot_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SkinningLootTemplate lootTemplate = new()
                        {
                            Entry = reader.GetUInt32("entry"),
                            Item = reader.GetUInt32("item"),
                            ChanceOrQuestChance = reader.GetFloat("ChanceOrQuestChance"),
                            GroupId = reader.GetByte("groupid"),
                            MinCountOrRef = reader.GetInt32("mincountOrRef"),
                            MaxCount = reader.GetByte("maxcount"),
                            ConditionId = reader.GetUInt32("condition_id"),
                            PatchMin = reader.GetByte("patch_min"),
                            PatchMax = reader.GetByte("patch_max")
                        };

                        skinningLootTemplates.Add(lootTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return skinningLootTemplates;
        }

        public static List<SoundEntries> GetSoundEntries()
        {
            List<SoundEntries> soundEntries = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM sound_entries";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        SoundEntries soundEntry = new()
                        {
                            Id = reader.GetUInt32("ID"),
                            Name = reader.GetString("name")
                        };
                        soundEntries.Add(soundEntry);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return soundEntries;
        }

        public static List<Variables> GetVariables()
        {
            List<Variables> variables = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM variables";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Variables variable = new()
                        {
                            Index = reader.GetUInt32("index"),
                            Value = reader.GetUInt32("value")
                        };

                        variables.Add(variable);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return variables;
        }
    }
}
