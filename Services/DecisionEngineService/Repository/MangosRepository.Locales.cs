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
        public static List<LocalesArea> GetLocalesArea()
        {
            List<LocalesArea> localesAreas = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM locales_area";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var localesArea = new LocalesArea
                        {
                            Entry = reader.GetUInt32("Entry"),
                            NameLoc1 = reader.GetString("NameLoc1"),
                            NameLoc2 = reader.GetString("NameLoc2"),
                            NameLoc3 = reader.GetString("NameLoc3"),
                            NameLoc4 = reader.GetString("NameLoc4"),
                            NameLoc5 = reader.GetString("NameLoc5"),
                            NameLoc6 = reader.GetString("NameLoc6"),
                            NameLoc7 = reader.GetString("NameLoc7"),
                            NameLoc8 = reader.GetString("NameLoc8")
                        };

                        localesAreas.Add(localesArea);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return localesAreas;
        }

        public static List<LocalesBroadcastText> GetLocalesBroadcastTexts()
        {
            List<LocalesBroadcastText> broadcastTexts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM locales_broadcast_text";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var broadcastText = new LocalesBroadcastText
                        {
                            Id = reader.GetUInt32("ID"),
                            MaleTextLoc1 = reader.IsDBNull(reader.GetOrdinal("MaleText_loc1")) ? string.Empty : reader.GetString("MaleText_loc1"),
                            MaleTextLoc2 = reader.IsDBNull(reader.GetOrdinal("MaleText_loc2")) ? string.Empty : reader.GetString("MaleText_loc2"),
                            MaleTextLoc3 = reader.IsDBNull(reader.GetOrdinal("MaleText_loc3")) ? string.Empty : reader.GetString("MaleText_loc3"),
                            MaleTextLoc4 = reader.IsDBNull(reader.GetOrdinal("MaleText_loc4")) ? string.Empty : reader.GetString("MaleText_loc4"),
                            MaleTextLoc5 = reader.IsDBNull(reader.GetOrdinal("MaleText_loc5")) ? string.Empty : reader.GetString("MaleText_loc5"),
                            MaleTextLoc6 = reader.IsDBNull(reader.GetOrdinal("MaleText_loc6")) ? string.Empty : reader.GetString("MaleText_loc6"),
                            MaleTextLoc7 = reader.IsDBNull(reader.GetOrdinal("MaleText_loc7")) ? string.Empty : reader.GetString("MaleText_loc7"),
                            MaleTextLoc8 = reader.IsDBNull(reader.GetOrdinal("MaleText_loc8")) ? string.Empty : reader.GetString("MaleText_loc8"),
                            FemaleTextLoc1 = reader.IsDBNull(reader.GetOrdinal("FemaleText_loc1")) ? string.Empty : reader.GetString("FemaleText_loc1"),
                            FemaleTextLoc2 = reader.IsDBNull(reader.GetOrdinal("FemaleText_loc2")) ? string.Empty : reader.GetString("FemaleText_loc2"),
                            FemaleTextLoc3 = reader.IsDBNull(reader.GetOrdinal("FemaleText_loc3")) ? string.Empty : reader.GetString("FemaleText_loc3"),
                            FemaleTextLoc4 = reader.IsDBNull(reader.GetOrdinal("FemaleText_loc4")) ? string.Empty : reader.GetString("FemaleText_loc4"),
                            FemaleTextLoc5 = reader.IsDBNull(reader.GetOrdinal("FemaleText_loc5")) ? string.Empty : reader.GetString("FemaleText_loc5"),
                            FemaleTextLoc6 = reader.IsDBNull(reader.GetOrdinal("FemaleText_loc6")) ? string.Empty : reader.GetString("FemaleText_loc6"),
                            FemaleTextLoc7 = reader.IsDBNull(reader.GetOrdinal("FemaleText_loc7")) ? string.Empty : reader.GetString("FemaleText_loc7"),
                            FemaleTextLoc8 = reader.IsDBNull(reader.GetOrdinal("FemaleText_loc8")) ? string.Empty : reader.GetString("FemaleText_loc8"),
                            VerifiedBuild = reader.GetInt16("VerifiedBuild")
                        };

                        broadcastTexts.Add(broadcastText);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return broadcastTexts;
        }

        public static List<LocalesCreature> GetLocalesCreatures()
        {
            List<LocalesCreature> creatures = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM locales_creature";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var creature = new LocalesCreature
                        {
                            Entry = reader.GetUInt32("entry"),
                            NameLoc1 = reader.GetString("name_loc1"),
                            NameLoc2 = reader.GetString("name_loc2"),
                            NameLoc3 = reader.GetString("name_loc3"),
                            NameLoc4 = reader.GetString("name_loc4"),
                            NameLoc5 = reader.GetString("name_loc5"),
                            NameLoc6 = reader.GetString("name_loc6"),
                            NameLoc7 = reader.GetString("name_loc7"),
                            NameLoc8 = reader.GetString("name_loc8"),
                            SubnameLoc1 = reader.IsDBNull(reader.GetOrdinal("subname_loc1")) ? string.Empty : reader.GetString("subname_loc1"),
                            SubnameLoc2 = reader.IsDBNull(reader.GetOrdinal("subname_loc2")) ? string.Empty : reader.GetString("subname_loc2"),
                            SubnameLoc3 = reader.IsDBNull(reader.GetOrdinal("subname_loc3")) ? string.Empty : reader.GetString("subname_loc3"),
                            SubnameLoc4 = reader.IsDBNull(reader.GetOrdinal("subname_loc4")) ? string.Empty : reader.GetString("subname_loc4"),
                            SubnameLoc5 = reader.IsDBNull(reader.GetOrdinal("subname_loc5")) ? string.Empty : reader.GetString("subname_loc5"),
                            SubnameLoc6 = reader.IsDBNull(reader.GetOrdinal("subname_loc6")) ? string.Empty : reader.GetString("subname_loc6"),
                            SubnameLoc7 = reader.IsDBNull(reader.GetOrdinal("subname_loc7")) ? string.Empty : reader.GetString("subname_loc7"),
                            SubnameLoc8 = reader.IsDBNull(reader.GetOrdinal("subname_loc8")) ? string.Empty : reader.GetString("subname_loc8")
                        };

                        creatures.Add(creature);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return creatures;
        }

        public static List<LocalesGameObject> GetLocalesGameObjects()
        {
            List<LocalesGameObject> gameObjects = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM locales_gameobject";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gameObject = new LocalesGameObject
                        {
                            Entry = reader.GetUInt32("entry"),
                            NameLoc1 = reader.GetString("name_loc1"),
                            NameLoc2 = reader.GetString("name_loc2"),
                            NameLoc3 = reader.GetString("name_loc3"),
                            NameLoc4 = reader.GetString("name_loc4"),
                            NameLoc5 = reader.GetString("name_loc5"),
                            NameLoc6 = reader.GetString("name_loc6"),
                            NameLoc7 = reader.GetString("name_loc7"),
                            NameLoc8 = reader.GetString("name_loc8")
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

        public static List<LocalesGossipMenuOption> GetLocalesGossipMenuOptions()
        {
            List<LocalesGossipMenuOption> gossipMenuOptions = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM locales_gossip_menu_option";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gossipMenuOption = new LocalesGossipMenuOption
                        {
                            MenuId = reader.GetUInt16("menu_id"),
                            Id = reader.GetUInt16("id"),
                            OptionTextLoc1 = reader.IsDBNull(reader.GetOrdinal("option_text_loc1")) ? string.Empty : reader.GetString("option_text_loc1"),
                            OptionTextLoc2 = reader.IsDBNull(reader.GetOrdinal("option_text_loc2")) ? string.Empty : reader.GetString("option_text_loc2"),
                            OptionTextLoc3 = reader.IsDBNull(reader.GetOrdinal("option_text_loc3")) ? string.Empty : reader.GetString("option_text_loc3"),
                            OptionTextLoc4 = reader.IsDBNull(reader.GetOrdinal("option_text_loc4")) ? string.Empty : reader.GetString("option_text_loc4"),
                            OptionTextLoc5 = reader.IsDBNull(reader.GetOrdinal("option_text_loc5")) ? string.Empty : reader.GetString("option_text_loc5"),
                            OptionTextLoc6 = reader.IsDBNull(reader.GetOrdinal("option_text_loc6")) ? string.Empty : reader.GetString("option_text_loc6"),
                            OptionTextLoc7 = reader.IsDBNull(reader.GetOrdinal("option_text_loc7")) ? string.Empty : reader.GetString("option_text_loc7"),
                            OptionTextLoc8 = reader.IsDBNull(reader.GetOrdinal("option_text_loc8")) ? string.Empty : reader.GetString("option_text_loc8"),
                            BoxTextLoc1 = reader.IsDBNull(reader.GetOrdinal("box_text_loc1")) ? string.Empty : reader.GetString("box_text_loc1"),
                            BoxTextLoc2 = reader.IsDBNull(reader.GetOrdinal("box_text_loc2")) ? string.Empty : reader.GetString("box_text_loc2"),
                            BoxTextLoc3 = reader.IsDBNull(reader.GetOrdinal("box_text_loc3")) ? string.Empty : reader.GetString("box_text_loc3"),
                            BoxTextLoc4 = reader.IsDBNull(reader.GetOrdinal("box_text_loc4")) ? string.Empty : reader.GetString("box_text_loc4"),
                            BoxTextLoc5 = reader.IsDBNull(reader.GetOrdinal("box_text_loc5")) ? string.Empty : reader.GetString("box_text_loc5"),
                            BoxTextLoc6 = reader.IsDBNull(reader.GetOrdinal("box_text_loc6")) ? string.Empty : reader.GetString("box_text_loc6"),
                            BoxTextLoc7 = reader.IsDBNull(reader.GetOrdinal("box_text_loc7")) ? string.Empty : reader.GetString("box_text_loc7"),
                            BoxTextLoc8 = reader.IsDBNull(reader.GetOrdinal("box_text_loc8")) ? string.Empty : reader.GetString("box_text_loc8")
                        };

                        gossipMenuOptions.Add(gossipMenuOption);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gossipMenuOptions;
        }

        public static List<LocalesItem> GetLocalesItems()
        {
            List<LocalesItem> localesItems = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM locales_item";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var localesItem = new LocalesItem
                        {
                            Entry = reader.GetUInt32("entry"),
                            NameLoc1 = reader.GetString("name_loc1"),
                            NameLoc2 = reader.GetString("name_loc2"),
                            NameLoc3 = reader.GetString("name_loc3"),
                            NameLoc4 = reader.GetString("name_loc4"),
                            NameLoc5 = reader.GetString("name_loc5"),
                            NameLoc6 = reader.GetString("name_loc6"),
                            NameLoc7 = reader.GetString("name_loc7"),
                            NameLoc8 = reader.GetString("name_loc8"),
                            DescriptionLoc1 = reader.IsDBNull(reader.GetOrdinal("description_loc1")) ? string.Empty : reader.GetString("description_loc1"),
                            DescriptionLoc2 = reader.IsDBNull(reader.GetOrdinal("description_loc2")) ? string.Empty : reader.GetString("description_loc2"),
                            DescriptionLoc3 = reader.IsDBNull(reader.GetOrdinal("description_loc3")) ? string.Empty : reader.GetString("description_loc3"),
                            DescriptionLoc4 = reader.IsDBNull(reader.GetOrdinal("description_loc4")) ? string.Empty : reader.GetString("description_loc4"),
                            DescriptionLoc5 = reader.IsDBNull(reader.GetOrdinal("description_loc5")) ? string.Empty : reader.GetString("description_loc5"),
                            DescriptionLoc6 = reader.IsDBNull(reader.GetOrdinal("description_loc6")) ? string.Empty : reader.GetString("description_loc6"),
                            DescriptionLoc7 = reader.IsDBNull(reader.GetOrdinal("description_loc7")) ? string.Empty : reader.GetString("description_loc7"),
                            DescriptionLoc8 = reader.IsDBNull(reader.GetOrdinal("description_loc8")) ? string.Empty : reader.GetString("description_loc8")
                        };

                        localesItems.Add(localesItem);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return localesItems;
        }

        public static List<LocalesPageText> GetLocalesPageTexts()
        {
            List<LocalesPageText> localesPageTexts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM locales_page_text";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var localesPageText = new LocalesPageText
                        {
                            Entry = reader.GetUInt32("entry"),
                            TextLoc1 = reader.IsDBNull(reader.GetOrdinal("Text_loc1")) ? string.Empty : reader.GetString("Text_loc1"),
                            TextLoc2 = reader.IsDBNull(reader.GetOrdinal("Text_loc2")) ? string.Empty : reader.GetString("Text_loc2"),
                            TextLoc3 = reader.IsDBNull(reader.GetOrdinal("Text_loc3")) ? string.Empty : reader.GetString("Text_loc3"),
                            TextLoc4 = reader.IsDBNull(reader.GetOrdinal("Text_loc4")) ? string.Empty : reader.GetString("Text_loc4"),
                            TextLoc5 = reader.IsDBNull(reader.GetOrdinal("Text_loc5")) ? string.Empty : reader.GetString("Text_loc5"),
                            TextLoc6 = reader.IsDBNull(reader.GetOrdinal("Text_loc6")) ? string.Empty : reader.GetString("Text_loc6"),
                            TextLoc7 = reader.IsDBNull(reader.GetOrdinal("Text_loc7")) ? string.Empty : reader.GetString("Text_loc7"),
                            TextLoc8 = reader.IsDBNull(reader.GetOrdinal("Text_loc8")) ? string.Empty : reader.GetString("Text_loc8")
                        };

                        localesPageTexts.Add(localesPageText);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return localesPageTexts;
        }

        public static List<LocalesPointsOfInterest> GetLocalesPointsOfInterest()
        {
            List<LocalesPointsOfInterest> pointsOfInterest = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM locales_points_of_interest";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var pointOfInterest = new LocalesPointsOfInterest
                        {
                            Entry = reader.GetUInt32("entry"),
                            IconNameLoc1 = reader.IsDBNull(reader.GetOrdinal("icon_name_loc1")) ? string.Empty : reader.GetString("icon_name_loc1"),
                            IconNameLoc2 = reader.IsDBNull(reader.GetOrdinal("icon_name_loc2")) ? string.Empty : reader.GetString("icon_name_loc2"),
                            IconNameLoc3 = reader.IsDBNull(reader.GetOrdinal("icon_name_loc3")) ? string.Empty : reader.GetString("icon_name_loc3"),
                            IconNameLoc4 = reader.IsDBNull(reader.GetOrdinal("icon_name_loc4")) ? string.Empty : reader.GetString("icon_name_loc4"),
                            IconNameLoc5 = reader.IsDBNull(reader.GetOrdinal("icon_name_loc5")) ? string.Empty : reader.GetString("icon_name_loc5"),
                            IconNameLoc6 = reader.IsDBNull(reader.GetOrdinal("icon_name_loc6")) ? string.Empty : reader.GetString("icon_name_loc6"),
                            IconNameLoc7 = reader.IsDBNull(reader.GetOrdinal("icon_name_loc7")) ? string.Empty : reader.GetString("icon_name_loc7"),
                            IconNameLoc8 = reader.IsDBNull(reader.GetOrdinal("icon_name_loc8")) ? string.Empty : reader.GetString("icon_name_loc8")
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

        public static List<LocalesQuest> GetLocalesQuest()
        {
            List<LocalesQuest> quests = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM locales_quest";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var quest = new LocalesQuest
                        {
                            Entry = reader.GetUInt32("entry"),
                            TitleLoc1 = reader.IsDBNull(reader.GetOrdinal("Title_loc1")) ? string.Empty : reader.GetString("Title_loc1"),
                            TitleLoc2 = reader.IsDBNull(reader.GetOrdinal("Title_loc2")) ? string.Empty : reader.GetString("Title_loc2"),
                            TitleLoc3 = reader.IsDBNull(reader.GetOrdinal("Title_loc3")) ? string.Empty : reader.GetString("Title_loc3"),
                            TitleLoc4 = reader.IsDBNull(reader.GetOrdinal("Title_loc4")) ? string.Empty : reader.GetString("Title_loc4"),
                            TitleLoc5 = reader.IsDBNull(reader.GetOrdinal("Title_loc5")) ? string.Empty : reader.GetString("Title_loc5"),
                            TitleLoc6 = reader.IsDBNull(reader.GetOrdinal("Title_loc6")) ? string.Empty : reader.GetString("Title_loc6"),
                            TitleLoc7 = reader.IsDBNull(reader.GetOrdinal("Title_loc7")) ? string.Empty : reader.GetString("Title_loc7"),
                            TitleLoc8 = reader.IsDBNull(reader.GetOrdinal("Title_loc8")) ? string.Empty : reader.GetString("Title_loc8"),
                            DetailsLoc1 = reader.IsDBNull(reader.GetOrdinal("Details_loc1")) ? string.Empty : reader.GetString("Details_loc1"),
                            DetailsLoc2 = reader.IsDBNull(reader.GetOrdinal("Details_loc2")) ? string.Empty : reader.GetString("Details_loc2"),
                            DetailsLoc3 = reader.IsDBNull(reader.GetOrdinal("Details_loc3")) ? string.Empty : reader.GetString("Details_loc3"),
                            DetailsLoc4 = reader.IsDBNull(reader.GetOrdinal("Details_loc4")) ? string.Empty : reader.GetString("Details_loc4"),
                            DetailsLoc5 = reader.IsDBNull(reader.GetOrdinal("Details_loc5")) ? string.Empty : reader.GetString("Details_loc5"),
                            DetailsLoc6 = reader.IsDBNull(reader.GetOrdinal("Details_loc6")) ? string.Empty : reader.GetString("Details_loc6"),
                            DetailsLoc7 = reader.IsDBNull(reader.GetOrdinal("Details_loc7")) ? string.Empty : reader.GetString("Details_loc7"),
                            DetailsLoc8 = reader.IsDBNull(reader.GetOrdinal("Details_loc8")) ? string.Empty : reader.GetString("Details_loc8"),
                            ObjectivesLoc1 = reader.IsDBNull(reader.GetOrdinal("Objectives_loc1")) ? string.Empty : reader.GetString("Objectives_loc1"),
                            ObjectivesLoc2 = reader.IsDBNull(reader.GetOrdinal("Objectives_loc2")) ? string.Empty : reader.GetString("Objectives_loc2"),
                            ObjectivesLoc3 = reader.IsDBNull(reader.GetOrdinal("Objectives_loc3")) ? string.Empty : reader.GetString("Objectives_loc3"),
                            ObjectivesLoc4 = reader.IsDBNull(reader.GetOrdinal("Objectives_loc4")) ? string.Empty : reader.GetString("Objectives_loc4"),
                            ObjectivesLoc5 = reader.IsDBNull(reader.GetOrdinal("Objectives_loc5")) ? string.Empty : reader.GetString("Objectives_loc5"),
                            ObjectivesLoc6 = reader.IsDBNull(reader.GetOrdinal("Objectives_loc6")) ? string.Empty : reader.GetString("Objectives_loc6"),
                            ObjectivesLoc7 = reader.IsDBNull(reader.GetOrdinal("Objectives_loc7")) ? string.Empty : reader.GetString("Objectives_loc7"),
                            ObjectivesLoc8 = reader.IsDBNull(reader.GetOrdinal("Objectives_loc8")) ? string.Empty : reader.GetString("Objectives_loc8"),
                            OfferRewardTextLoc1 = reader.IsDBNull(reader.GetOrdinal("OfferRewardText_loc1")) ? string.Empty : reader.GetString("OfferRewardText_loc1"),
                            OfferRewardTextLoc2 = reader.IsDBNull(reader.GetOrdinal("OfferRewardText_loc2")) ? string.Empty : reader.GetString("OfferRewardText_loc2"),
                            OfferRewardTextLoc3 = reader.IsDBNull(reader.GetOrdinal("OfferRewardText_loc3")) ? string.Empty : reader.GetString("OfferRewardText_loc3"),
                            OfferRewardTextLoc4 = reader.IsDBNull(reader.GetOrdinal("OfferRewardText_loc4")) ? string.Empty : reader.GetString("OfferRewardText_loc4"),
                            OfferRewardTextLoc5 = reader.IsDBNull(reader.GetOrdinal("OfferRewardText_loc5")) ? string.Empty : reader.GetString("OfferRewardText_loc5"),
                            OfferRewardTextLoc6 = reader.IsDBNull(reader.GetOrdinal("OfferRewardText_loc6")) ? string.Empty : reader.GetString("OfferRewardText_loc6"),
                            OfferRewardTextLoc7 = reader.IsDBNull(reader.GetOrdinal("OfferRewardText_loc7")) ? string.Empty : reader.GetString("OfferRewardText_loc7"),
                            OfferRewardTextLoc8 = reader.IsDBNull(reader.GetOrdinal("OfferRewardText_loc8")) ? string.Empty : reader.GetString("OfferRewardText_loc8"),
                            RequestItemsTextLoc1 = reader.IsDBNull(reader.GetOrdinal("RequestItemsText_loc1")) ? string.Empty : reader.GetString("RequestItemsText_loc1"),
                            RequestItemsTextLoc2 = reader.IsDBNull(reader.GetOrdinal("RequestItemsText_loc2")) ? string.Empty : reader.GetString("RequestItemsText_loc2"),
                            RequestItemsTextLoc3 = reader.IsDBNull(reader.GetOrdinal("RequestItemsText_loc3")) ? string.Empty : reader.GetString("RequestItemsText_loc3"),
                            RequestItemsTextLoc4 = reader.IsDBNull(reader.GetOrdinal("RequestItemsText_loc4")) ? string.Empty : reader.GetString("RequestItemsText_loc4"),
                            RequestItemsTextLoc5 = reader.IsDBNull(reader.GetOrdinal("RequestItemsText_loc5")) ? string.Empty : reader.GetString("RequestItemsText_loc5"),
                            RequestItemsTextLoc6 = reader.IsDBNull(reader.GetOrdinal("RequestItemsText_loc6")) ? string.Empty : reader.GetString("RequestItemsText_loc6"),
                            RequestItemsTextLoc7 = reader.IsDBNull(reader.GetOrdinal("RequestItemsText_loc7")) ? string.Empty : reader.GetString("RequestItemsText_loc7"),
                            RequestItemsTextLoc8 = reader.IsDBNull(reader.GetOrdinal("RequestItemsText_loc8")) ? string.Empty : reader.GetString("RequestItemsText_loc8"),
                            EndTextLoc1 = reader.IsDBNull(reader.GetOrdinal("EndText_loc1")) ? string.Empty : reader.GetString("EndText_loc1"),
                            EndTextLoc2 = reader.IsDBNull(reader.GetOrdinal("EndText_loc2")) ? string.Empty : reader.GetString("EndText_loc2"),
                            EndTextLoc3 = reader.IsDBNull(reader.GetOrdinal("EndText_loc3")) ? string.Empty : reader.GetString("EndText_loc3"),
                            EndTextLoc4 = reader.IsDBNull(reader.GetOrdinal("EndText_loc4")) ? string.Empty : reader.GetString("EndText_loc4"),
                            EndTextLoc5 = reader.IsDBNull(reader.GetOrdinal("EndText_loc5")) ? string.Empty : reader.GetString("EndText_loc5"),
                            EndTextLoc6 = reader.IsDBNull(reader.GetOrdinal("EndText_loc6")) ? string.Empty : reader.GetString("EndText_loc6"),
                            EndTextLoc7 = reader.IsDBNull(reader.GetOrdinal("EndText_loc7")) ? string.Empty : reader.GetString("EndText_loc7"),
                            EndTextLoc8 = reader.IsDBNull(reader.GetOrdinal("EndText_loc8")) ? string.Empty : reader.GetString("EndText_loc8"),
                            ObjectiveText1Loc1 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText1_loc1")) ? string.Empty : reader.GetString("ObjectiveText1_loc1"),
                            ObjectiveText1Loc2 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText1_loc2")) ? string.Empty : reader.GetString("ObjectiveText1_loc2"),
                            ObjectiveText1Loc3 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText1_loc3")) ? string.Empty : reader.GetString("ObjectiveText1_loc3"),
                            ObjectiveText1Loc4 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText1_loc4")) ? string.Empty : reader.GetString("ObjectiveText1_loc4"),
                            ObjectiveText1Loc5 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText1_loc5")) ? string.Empty : reader.GetString("ObjectiveText1_loc5"),
                            ObjectiveText1Loc6 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText1_loc6")) ? string.Empty : reader.GetString("ObjectiveText1_loc6"),
                            ObjectiveText1Loc7 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText1_loc7")) ? string.Empty : reader.GetString("ObjectiveText1_loc7"),
                            ObjectiveText1Loc8 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText1_loc8")) ? string.Empty : reader.GetString("ObjectiveText1_loc8"),
                            ObjectiveText2Loc1 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText2_loc1")) ? string.Empty : reader.GetString("ObjectiveText2_loc1"),
                            ObjectiveText2Loc2 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText2_loc2")) ? string.Empty : reader.GetString("ObjectiveText2_loc2"),
                            ObjectiveText2Loc3 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText2_loc3")) ? string.Empty : reader.GetString("ObjectiveText2_loc3"),
                            ObjectiveText2Loc4 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText2_loc4")) ? string.Empty : reader.GetString("ObjectiveText2_loc4"),
                            ObjectiveText2Loc5 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText2_loc5")) ? string.Empty : reader.GetString("ObjectiveText2_loc5"),
                            ObjectiveText2Loc6 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText2_loc6")) ? string.Empty : reader.GetString("ObjectiveText2_loc6"),
                            ObjectiveText2Loc7 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText2_loc7")) ? string.Empty : reader.GetString("ObjectiveText2_loc7"),
                            ObjectiveText2Loc8 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText2_loc8")) ? string.Empty : reader.GetString("ObjectiveText2_loc8"),
                            ObjectiveText3Loc1 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText3_loc1")) ? string.Empty : reader.GetString("ObjectiveText3_loc1"),
                            ObjectiveText3Loc2 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText3_loc2")) ? string.Empty : reader.GetString("ObjectiveText3_loc2"),
                            ObjectiveText3Loc3 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText3_loc3")) ? string.Empty : reader.GetString("ObjectiveText3_loc3"),
                            ObjectiveText3Loc4 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText3_loc4")) ? string.Empty : reader.GetString("ObjectiveText3_loc4"),
                            ObjectiveText4Loc1 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText4_loc1")) ? string.Empty : reader.GetString("ObjectiveText4_loc1"),
                            ObjectiveText4Loc2 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText4_loc2")) ? string.Empty : reader.GetString("ObjectiveText4_loc2"),
                            ObjectiveText4Loc3 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText4_loc3")) ? string.Empty : reader.GetString("ObjectiveText4_loc3"),
                            ObjectiveText4Loc4 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText4_loc4")) ? string.Empty : reader.GetString("ObjectiveText4_loc4"),
                            ObjectiveText4Loc5 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText4_loc5")) ? string.Empty : reader.GetString("ObjectiveText4_loc5"),
                            ObjectiveText4Loc6 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText4_loc6")) ? string.Empty : reader.GetString("ObjectiveText4_loc6"),
                            ObjectiveText4Loc7 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText4_loc7")) ? string.Empty : reader.GetString("ObjectiveText4_loc7"),
                            ObjectiveText4Loc8 = reader.IsDBNull(reader.GetOrdinal("ObjectiveText4_loc8")) ? string.Empty : reader.GetString("ObjectiveText4_loc8")
                        };

                        quests.Add(quest);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return quests;
        }
    }
}
