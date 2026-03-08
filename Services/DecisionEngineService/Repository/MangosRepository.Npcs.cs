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
        public static List<GossipMenu> GetGossipMenus()
        {
            List<GossipMenu> gossipMenus = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gossip_menu";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gossipMenu = new GossipMenu
                        {
                            Entry = reader.GetUInt16("entry"),
                            TextId = reader.GetUInt32("text_id"),
                            ConditionId = reader.GetUInt32("condition_id")
                        };

                        gossipMenus.Add(gossipMenu);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gossipMenus;
        }

        public static List<GossipMenuOption> GetGossipMenuOptions()
        {
            List<GossipMenuOption> gossipMenuOptions = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gossip_menu_option";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gossipMenuOption = new GossipMenuOption
                        {
                            MenuId = reader.GetUInt16("menu_id"),
                            Id = reader.GetUInt16("id"),
                            OptionIcon = reader.GetUInt32("option_icon"),
                            OptionText = reader.IsDBNull("option_text") ? string.Empty : reader.GetString("option_text"),
                            OptionBroadcastTextId = reader.GetUInt32("OptionBroadcastTextID"),
                            OptionId = reader.GetByte("option_id"),
                            NpcOptionNpcflag = reader.GetUInt32("npc_option_npcflag"),
                            ActionMenuId = reader.GetInt32("action_menu_id"),
                            ActionPoiId = reader.GetUInt32("action_poi_id"),
                            ActionScriptId = reader.GetUInt32("action_script_id"),
                            BoxCoded = reader.GetByte("box_coded"),
                            BoxMoney = reader.GetUInt32("box_money"),
                            BoxText = reader.IsDBNull("box_text") ? string.Empty : reader.GetString("box_text"),
                            BoxBroadcastTextId = reader.GetUInt32("BoxBroadcastTextID"),
                            ConditionId = reader.GetUInt32("condition_id")
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

        public static List<GossipScript> GetGossipScripts()
        {
            List<GossipScript> gossipScripts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM gossip_scripts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var gossipScript = new GossipScript
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
                            Comments = reader.IsDBNull("comments") ? string.Empty : reader.GetString("comments")
                        };

                        gossipScripts.Add(gossipScript);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return gossipScripts;
        }

        public static List<NpcGossip> GetNpcGossips()
        {
            List<NpcGossip> npcGossips = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM npc_gossip";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var npcGossip = new NpcGossip
                        {
                            NpcGuid = reader.GetUInt32("npc_guid"),
                            Textid = reader.GetUInt32("textid")
                        };

                        npcGossips.Add(npcGossip);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return npcGossips;
        }

        public static List<NpcText> GetNpcTexts()
        {
            List<NpcText> npcTexts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM npc_text";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var npcText = new NpcText
                        {
                            ID = reader.GetUInt32("ID"),
                            BroadcastTextID0 = reader.GetUInt32("BroadcastTextID0"),
                            Probability0 = reader.GetFloat("Probability0"),
                            BroadcastTextID1 = reader.GetUInt32("BroadcastTextID1"),
                            Probability1 = reader.GetFloat("Probability1"),
                            BroadcastTextID2 = reader.GetUInt32("BroadcastTextID2"),
                            Probability2 = reader.GetFloat("Probability2"),
                            BroadcastTextID3 = reader.GetUInt32("BroadcastTextID3"),
                            Probability3 = reader.GetFloat("Probability3"),
                            BroadcastTextID4 = reader.GetUInt32("BroadcastTextID4"),
                            Probability4 = reader.GetFloat("Probability4"),
                            BroadcastTextID5 = reader.GetUInt32("BroadcastTextID5"),
                            Probability5 = reader.GetFloat("Probability5"),
                            BroadcastTextID6 = reader.GetUInt32("BroadcastTextID6"),
                            Probability6 = reader.GetFloat("Probability6"),
                            BroadcastTextID7 = reader.GetUInt32("BroadcastTextID7"),
                            Probability7 = reader.GetFloat("Probability7")
                        };

                        npcTexts.Add(npcText);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return npcTexts;
        }

        public static List<NpcTrainer> GetNpcTrainers()
        {
            List<NpcTrainer> npcTrainers = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM npc_trainer";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var npcTrainer = new NpcTrainer
                        {
                            Entry = reader.GetUInt32("entry"),
                            Spell = reader.GetUInt32("spell"),
                            Spellcost = reader.GetUInt32("spellcost"),
                            Reqskill = reader.GetUInt16("reqskill"),
                            Reqskillvalue = reader.GetUInt16("reqskillvalue"),
                            Reqlevel = reader.GetByte("reqlevel")
                        };

                        npcTrainers.Add(npcTrainer);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return npcTrainers;
        }

        public static List<NpcTrainerTemplate> GetNpcTrainerTemplates()
        {
            List<NpcTrainerTemplate> npcTrainerTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM npc_trainer_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var npcTrainerTemplate = new NpcTrainerTemplate
                        {
                            Entry = reader.GetUInt32("entry"),
                            Spell = reader.GetUInt32("spell"),
                            Spellcost = reader.GetUInt32("spellcost"),
                            Reqskill = reader.GetUInt16("reqskill"),
                            Reqskillvalue = reader.GetUInt16("reqskillvalue"),
                            Reqlevel = reader.GetByte("reqlevel")
                        };

                        npcTrainerTemplates.Add(npcTrainerTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return npcTrainerTemplates;
        }

        public static List<NpcVendor> GetNpcVendors()
        {
            List<NpcVendor> npcVendors = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM npc_vendor";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var npcVendor = new NpcVendor
                        {
                            Entry = reader.GetUInt32("entry"),
                            Item = reader.GetUInt32("item"),
                            Maxcount = reader.GetByte("maxcount"),
                            Incrtime = reader.GetUInt32("incrtime")
                        };

                        npcVendors.Add(npcVendor);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return npcVendors;
        }

        public static List<NpcVendorTemplate> GetNpcVendorTemplates()
        {
            List<NpcVendorTemplate> npcVendorTemplates = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM npc_vendor_template";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var npcVendorTemplate = new NpcVendorTemplate
                        {
                            Entry = reader.GetUInt32("entry"),
                            Item = reader.GetUInt32("item"),
                            Maxcount = reader.GetByte("maxcount"),
                            Incrtime = reader.GetUInt32("incrtime")
                        };

                        npcVendorTemplates.Add(npcVendorTemplate);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return npcVendorTemplates;
        }
    }
}
