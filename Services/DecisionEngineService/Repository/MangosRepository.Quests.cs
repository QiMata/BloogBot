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
        public static List<QuestEndScripts> GetQuestEndScripts()
        {
            List<QuestEndScripts> questEndScripts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM quest_end_scripts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var questEndScript = new QuestEndScripts
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

                        questEndScripts.Add(questEndScript);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return questEndScripts;
        }

        public static List<QuestGreeting> GetQuestGreeting()
        {
            List<QuestGreeting> questGreetings = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM quest_greeting";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var questGreeting = new QuestGreeting
                        {
                            Entry = reader.GetUInt32("entry"),
                            Type = reader.GetByte("type"),
                            ContentDefault = reader.GetString("content_default"),
                            ContentLoc1 = reader.IsDBNull(reader.GetOrdinal("content_loc1")) ? string.Empty : reader.GetString("content_loc1"),
                            ContentLoc2 = reader.IsDBNull(reader.GetOrdinal("content_loc2")) ? string.Empty : reader.GetString("content_loc2"),
                            ContentLoc3 = reader.IsDBNull(reader.GetOrdinal("content_loc3")) ? string.Empty : reader.GetString("content_loc3"),
                            ContentLoc4 = reader.IsDBNull(reader.GetOrdinal("content_loc4")) ? string.Empty : reader.GetString("content_loc4"),
                            ContentLoc5 = reader.IsDBNull(reader.GetOrdinal("content_loc5")) ? string.Empty : reader.GetString("content_loc5"),
                            ContentLoc6 = reader.IsDBNull(reader.GetOrdinal("content_loc6")) ? string.Empty : reader.GetString("content_loc6"),
                            ContentLoc7 = reader.IsDBNull(reader.GetOrdinal("content_loc7")) ? string.Empty : reader.GetString("content_loc7"),
                            ContentLoc8 = reader.IsDBNull(reader.GetOrdinal("content_loc8")) ? string.Empty : reader.GetString("content_loc8"),
                            Emote = reader.GetUInt16("Emote"),
                            EmoteDelay = reader.GetUInt32("EmoteDelay")
                        };

                        questGreetings.Add(questGreeting);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return questGreetings;
        }

        public static List<QuestStartScripts> GetQuestStartScripts()
        {
            List<QuestStartScripts> questStartScripts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM quest_start_scripts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var questStartScript = new QuestStartScripts
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

                        questStartScripts.Add(questStartScript);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return questStartScripts;
        }

        public static List<QuestTemplate> GetQuestTemplates()
        {
            List<QuestTemplate> questTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM quest_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var questTemplate = new QuestTemplate
                        {
                            Entry = reader.GetUInt32("entry"),
                            Patch = reader.GetByte("patch"),
                            Method = reader.GetByte("Method"),
                            ZoneOrSort = reader.GetInt16("ZoneOrSort"),
                            MinLevel = reader.GetByte("MinLevel"),
                            MaxLevel = reader.GetByte("MaxLevel"),
                            QuestLevel = reader.GetByte("QuestLevel"),
                            Type = reader.GetUInt16("Type"),
                            RequiredClasses = reader.GetUInt16("RequiredClasses"),
                            RequiredRaces = reader.GetUInt16("RequiredRaces"),
                            RequiredSkill = reader.GetUInt16("RequiredSkill"),
                            RequiredSkillValue = reader.GetUInt16("RequiredSkillValue"),
                            RepObjectiveFaction = reader.GetUInt16("RepObjectiveFaction"),
                            RepObjectiveValue = reader.GetInt32("RepObjectiveValue"),
                            RequiredMinRepFaction = reader.GetUInt16("RequiredMinRepFaction"),
                            RequiredMinRepValue = reader.GetInt32("RequiredMinRepValue"),
                            RequiredMaxRepFaction = reader.GetUInt16("RequiredMaxRepFaction"),
                            RequiredMaxRepValue = reader.GetInt32("RequiredMaxRepValue"),
                            SuggestedPlayers = reader.GetByte("SuggestedPlayers"),
                            LimitTime = reader.GetUInt32("LimitTime"),
                            QuestFlags = reader.GetUInt16("QuestFlags"),
                            SpecialFlags = reader.GetByte("SpecialFlags"),
                            PrevQuestId = reader.GetInt32("PrevQuestId"),
                            NextQuestId = reader.GetInt32("NextQuestId"),
                            ExclusiveGroup = reader.GetInt32("ExclusiveGroup"),
                            NextQuestInChain = reader.GetUInt32("NextQuestInChain"),
                            SrcItemId = reader.GetUInt32("SrcItemId"),
                            SrcItemCount = reader.GetByte("SrcItemCount"),
                            SrcSpell = reader.GetUInt32("SrcSpell"),
                            Title = reader.IsDBNull(reader.GetOrdinal("Title")) ? string.Empty : reader.GetString("Title"),
                            Details = reader.IsDBNull(reader.GetOrdinal("Details")) ? string.Empty : reader.GetString("Details"),
                            Objectives = reader.IsDBNull(reader.GetOrdinal("Objectives")) ? string.Empty : reader.GetString("Objectives"),
                            OfferRewardText = reader.IsDBNull(reader.GetOrdinal("OfferRewardText")) ? string.Empty : reader.GetString("OfferRewardText"),
                            RequestItemsText = reader.IsDBNull(reader.GetOrdinal("RequestItemsText")) ? string.Empty : reader.GetString("RequestItemsText"),
                            EndText = reader.IsDBNull(reader.GetOrdinal("EndText")) ? string.Empty : reader.GetString("EndText"),
                            ObjectiveText1 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText1")) ? string.Empty : reader.GetString("ObjectiveText1"),
                            ObjectiveText2 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText2")) ? string.Empty : reader.GetString("ObjectiveText2"),
                            ObjectiveText3 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText3")) ? string.Empty : reader.GetString("ObjectiveText3"),
                            ObjectiveText4 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText4")) ? string.Empty : reader.GetString("ObjectiveText4"),
                            ReqItemId1 = reader.GetUInt32("ReqItemId1"),
                            ReqItemId2 = reader.GetUInt32("ReqItemId2"),
                            ReqItemId3 = reader.GetUInt32("ReqItemId3"),
                            ReqItemId4 = reader.GetUInt32("ReqItemId4"),
                            ReqItemCount1 = reader.GetUInt16("ReqItemCount1"),
                            ReqItemCount2 = reader.GetUInt16("ReqItemCount2"),
                            ReqItemCount3 = reader.GetUInt16("ReqItemCount3"),
                            ReqItemCount4 = reader.GetUInt16("ReqItemCount4"),
                            ReqSourceId1 = reader.GetUInt32("ReqSourceId1"),
                            ReqSourceId2 = reader.GetUInt32("ReqSourceId2"),
                            ReqSourceId3 = reader.GetUInt32("ReqSourceId3"),
                            ReqSourceId4 = reader.GetUInt32("ReqSourceId4"),
                            ReqSourceCount1 = reader.GetUInt16("ReqSourceCount1"),
                            ReqSourceCount2 = reader.GetUInt16("ReqSourceCount2"),
                            ReqSourceCount3 = reader.GetUInt16("ReqSourceCount3"),
                            ReqSourceCount4 = reader.GetUInt16("ReqSourceCount4"),
                            ReqCreatureOrGoId1 = reader.GetInt32("ReqCreatureOrGOId1"),
                            ReqCreatureOrGoId2 = reader.GetInt32("ReqCreatureOrGOId2"),
                            ReqCreatureOrGoId3 = reader.GetInt32("ReqCreatureOrGOId3"),
                            ReqCreatureOrGoId4 = reader.GetInt32("ReqCreatureOrGOId4"),
                            ReqCreatureOrGoCount1 = reader.GetUInt16("ReqCreatureOrGOCount1"),
                            ReqCreatureOrGoCount2 = reader.GetUInt16("ReqCreatureOrGOCount2"),
                            ReqCreatureOrGoCount3 = reader.GetUInt16("ReqCreatureOrGOCount3"),
                            ReqCreatureOrGoCount4 = reader.GetUInt16("ReqCreatureOrGOCount4"),
                            ReqSpellCast1 = reader.GetUInt32("ReqSpellCast1"),
                            ReqSpellCast2 = reader.GetUInt32("ReqSpellCast2"),
                            ReqSpellCast3 = reader.GetUInt32("ReqSpellCast3"),
                            ReqSpellCast4 = reader.GetUInt32("ReqSpellCast4"),
                            RewChoiceItemId1 = reader.GetUInt32("RewChoiceItemId1"),
                            RewChoiceItemId2 = reader.GetUInt32("RewChoiceItemId2"),
                            RewChoiceItemId3 = reader.GetUInt32("RewChoiceItemId3"),
                            RewChoiceItemId4 = reader.GetUInt32("RewChoiceItemId4"),
                            RewChoiceItemId5 = reader.GetUInt32("RewChoiceItemId5"),
                            RewChoiceItemId6 = reader.GetUInt32("RewChoiceItemId6"),
                            RewChoiceItemCount1 = reader.GetUInt16("RewChoiceItemCount1"),
                            RewChoiceItemCount2 = reader.GetUInt16("RewChoiceItemCount2"),
                            RewChoiceItemCount3 = reader.GetUInt16("RewChoiceItemCount3"),
                            RewChoiceItemCount4 = reader.GetUInt16("RewChoiceItemCount4"),
                            RewChoiceItemCount5 = reader.GetUInt16("RewChoiceItemCount5"),
                            RewChoiceItemCount6 = reader.GetUInt16("RewChoiceItemCount6"),
                            RewItemId1 = reader.GetUInt32("RewItemId1"),
                            RewItemId2 = reader.GetUInt32("RewItemId2"),
                            RewItemId3 = reader.GetUInt32("RewItemId3"),
                            RewItemId4 = reader.GetUInt32("RewItemId4"),
                            RewItemCount1 = reader.GetUInt16("RewItemCount1"),
                            RewItemCount2 = reader.GetUInt16("RewItemCount2"),
                            RewItemCount3 = reader.GetUInt16("RewItemCount3"),
                            RewItemCount4 = reader.GetUInt16("RewItemCount4"),
                            RewRepFaction1 = reader.GetUInt16("RewRepFaction1"),
                            RewRepFaction2 = reader.GetUInt16("RewRepFaction2"),
                            RewRepFaction3 = reader.GetUInt16("RewRepFaction3"),
                            RewRepFaction4 = reader.GetUInt16("RewRepFaction4"),
                            RewRepFaction5 = reader.GetUInt16("RewRepFaction5"),
                            RewRepValue1 = reader.GetInt32("RewRepValue1"),
                            RewRepValue2 = reader.GetInt32("RewRepValue2"),
                            RewRepValue3 = reader.GetInt32("RewRepValue3"),
                            RewRepValue4 = reader.GetInt32("RewRepValue4"),
                            RewRepValue5 = reader.GetInt32("RewRepValue5"),
                            RewOrReqMoney = reader.GetInt32("RewOrReqMoney"),
                            RewMoneyMaxLevel = reader.GetUInt32("RewMoneyMaxLevel"),
                            RewSpell = reader.GetUInt32("RewSpell"),
                            RewSpellCast = reader.GetUInt32("RewSpellCast"),
                            RewMailTemplateId = reader.GetUInt32("RewMailTemplateId"),
                            RewMailDelaySecs = reader.GetUInt32("RewMailDelaySecs"),
                            PointMapId = reader.GetUInt16("PointMapId"),
                            PointX = reader.GetFloat("PointX"),
                            PointY = reader.GetFloat("PointY"),
                            PointOpt = reader.GetUInt32("PointOpt"),
                            DetailsEmote1 = reader.GetUInt16("DetailsEmote1"),
                            DetailsEmote2 = reader.GetUInt16("DetailsEmote2"),
                            DetailsEmote3 = reader.GetUInt16("DetailsEmote3"),
                            DetailsEmote4 = reader.GetUInt16("DetailsEmote4"),
                            DetailsEmoteDelay1 = reader.GetUInt32("DetailsEmoteDelay1"),
                            DetailsEmoteDelay2 = reader.GetUInt32("DetailsEmoteDelay2"),
                            DetailsEmoteDelay3 = reader.GetUInt32("DetailsEmoteDelay3"),
                            DetailsEmoteDelay4 = reader.GetUInt32("DetailsEmoteDelay4"),
                            IncompleteEmote = reader.GetUInt16("IncompleteEmote"),
                            CompleteEmote = reader.GetUInt16("CompleteEmote"),
                            OfferRewardEmote1 = reader.GetUInt16("OfferRewardEmote1"),
                            OfferRewardEmote2 = reader.GetUInt16("OfferRewardEmote2"),
                            OfferRewardEmote3 = reader.GetUInt16("OfferRewardEmote3"),
                            OfferRewardEmote4 = reader.GetUInt16("OfferRewardEmote4"),
                            OfferRewardEmoteDelay1 = reader.GetUInt32("OfferRewardEmoteDelay1"),
                            OfferRewardEmoteDelay2 = reader.GetUInt32("OfferRewardEmoteDelay2"),
                            OfferRewardEmoteDelay3 = reader.GetUInt32("OfferRewardEmoteDelay3"),
                            OfferRewardEmoteDelay4 = reader.GetUInt32("OfferRewardEmoteDelay4"),
                            StartScript = reader.GetUInt32("StartScript"),
                            CompleteScript = reader.GetUInt32("CompleteScript")
                        };

                        questTemplates.Add(questTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return questTemplates;
        }
    }
}
