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
        public static List<PetCreateInfoSpell> GetPetCreateInfoSpells()
        {
            List<PetCreateInfoSpell> petCreateInfoSpells = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM petcreateinfo_spell";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var petCreateInfoSpell = new PetCreateInfoSpell
                        {
                            Entry = reader.GetUInt32("entry"),
                            Spell1 = reader.GetUInt32("Spell1"),
                            Spell2 = reader.GetUInt32("Spell2"),
                            Spell3 = reader.GetUInt32("Spell3"),
                            Spell4 = reader.GetUInt32("Spell4")
                        };

                        petCreateInfoSpells.Add(petCreateInfoSpell);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return petCreateInfoSpells;
        }

        public static List<PetLevelStats> GetPetLevelStats()
        {
            List<PetLevelStats> petLevelStats = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM pet_levelstats";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var stats = new PetLevelStats
                        {
                            CreatureEntry = reader.GetUInt32("creature_entry"),
                            Level = reader.GetByte("level"),
                            Hp = reader.GetUInt16("hp"),
                            Mana = reader.GetUInt16("mana"),
                            Armor = reader.GetUInt32("armor"),
                            Str = reader.GetUInt16("str"),
                            Agi = reader.GetUInt16("agi"),
                            Sta = reader.GetUInt16("sta"),
                            Inte = reader.GetUInt16("inte"),
                            Spi = reader.GetUInt16("spi")
                        };

                        petLevelStats.Add(stats);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return petLevelStats;
        }

        public static List<PetNameGeneration> GetPetNameGenerations()
        {
            List<PetNameGeneration> petNames = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM pet_name_generation";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var petName = new PetNameGeneration
                        {
                            Id = reader.GetUInt32("id"),
                            Word = reader.GetString("word"),
                            Entry = reader.GetUInt32("entry"),
                            Half = reader.GetUInt32("half")
                        };

                        petNames.Add(petName);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return petNames;
        }

        public static List<PlayerBot> GetPlayerBots()
        {
            List<PlayerBot> playerBots = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM playerbot";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var playerBot = new PlayerBot
                        {
                            CharGuid = reader.GetUInt64("char_guid"),
                            Chance = reader.GetUInt32("chance"),
                            Comment = reader.IsDBNull(reader.GetOrdinal("comment")) ? string.Empty : reader.GetString("comment")
                        };

                        playerBots.Add(playerBot);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return playerBots;
        }

        public static List<PlayerCreateInfo> GetPlayerCreateInfo()
        {
            List<PlayerCreateInfo> playerCreateInfos = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM playercreateinfo";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var playerCreateInfo = new PlayerCreateInfo
                        {
                            Race = reader.GetByte("race"),
                            Class = reader.GetByte("class"),
                            Map = reader.GetUInt16("map"),
                            Zone = reader.GetUInt32("zone"),
                            PositionX = reader.GetFloat("position_x"),
                            PositionY = reader.GetFloat("position_y"),
                            PositionZ = reader.GetFloat("position_z"),
                            Orientation = reader.GetFloat("orientation")
                        };

                        playerCreateInfos.Add(playerCreateInfo);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return playerCreateInfos;
        }

        public static List<PlayerCreateInfoAction> GetPlayerCreateInfoActions()
        {
            List<PlayerCreateInfoAction> playerCreateInfoActions = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM playercreateinfo_action";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var playerCreateInfoAction = new PlayerCreateInfoAction
                        {
                            Race = reader.GetByte("race"),
                            Class = reader.GetByte("class"),
                            Button = reader.GetUInt16("button"),
                            Action = reader.GetUInt32("action"),
                            Type = reader.GetUInt16("type")
                        };

                        playerCreateInfoActions.Add(playerCreateInfoAction);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return playerCreateInfoActions;
        }

        public static List<PlayerCreateInfoItem> GetPlayerCreateInfoItems()
        {
            List<PlayerCreateInfoItem> playerCreateInfoItems = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM playercreateinfo_item";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var playerCreateInfoItem = new PlayerCreateInfoItem
                        {
                            Race = reader.GetByte("race"),
                            Class = reader.GetByte("class"),
                            Itemid = reader.GetUInt32("itemid"),
                            Amount = reader.GetByte("amount")
                        };

                        playerCreateInfoItems.Add(playerCreateInfoItem);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return playerCreateInfoItems;
        }

        public static List<PlayerCreateInfoSpell> GetPlayerCreateInfoSpells()
        {
            List<PlayerCreateInfoSpell> playerCreateInfoSpells = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM playercreateinfo_spell";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var playerCreateInfoSpell = new PlayerCreateInfoSpell
                        {
                            Race = reader.GetByte("race"),
                            Class = reader.GetByte("class"),
                            Spell = reader.GetUInt32("Spell"),
                            Note = reader.IsDBNull(reader.GetOrdinal("Note")) ? string.Empty : reader.GetString("Note")
                        };

                        playerCreateInfoSpells.Add(playerCreateInfoSpell);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return playerCreateInfoSpells;
        }

        public static List<PlayerClassLevelStats> GetPlayerClassLevelStats()
        {
            List<PlayerClassLevelStats> classLevelStats = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM player_classlevelstats";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var stats = new PlayerClassLevelStats
                        {
                            Class = reader.GetByte("class"),
                            Level = reader.GetByte("level"),
                            Basehp = reader.GetUInt16("basehp"),
                            Basemana = reader.GetUInt16("basemana")
                        };

                        classLevelStats.Add(stats);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return classLevelStats;
        }

        public static List<PlayerFactionChangeItems> GetPlayerFactionChangeItems()
        {
            List<PlayerFactionChangeItems> factionChangeItems = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM player_factionchange_items";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var item = new PlayerFactionChangeItems
                        {
                            AllianceId = reader.GetInt32("alliance_id"),
                            HordeId = reader.GetInt32("horde_id"),
                            Comment = reader.GetString("comment")
                        };

                        factionChangeItems.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return factionChangeItems;
        }

        public static List<PlayerFactionChangeMounts> GetPlayerFactionChangeMounts()
        {
            List<PlayerFactionChangeMounts> factionChangeMounts = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM player_factionchange_mounts";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var mount = new PlayerFactionChangeMounts
                        {
                            RaceId = reader.GetInt32("RaceId"),
                            MountNum = reader.GetInt32("MountNum"),
                            ItemEntry = reader.GetInt32("ItemEntry"),
                            Comment = reader.GetString("Comment")
                        };

                        factionChangeMounts.Add(mount);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return factionChangeMounts;
        }

        public static List<PlayerFactionChangeQuests> GetPlayerFactionChangeQuests()
        {
            List<PlayerFactionChangeQuests> factionChangeQuests = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM player_factionchange_quests";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var quest = new PlayerFactionChangeQuests
                        {
                            AllianceId = reader.GetInt32("alliance_id"),
                            HordeId = reader.GetInt32("horde_id"),
                            Comment = reader.GetString("comment")
                        };

                        factionChangeQuests.Add(quest);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return factionChangeQuests;
        }

        public static List<PlayerFactionChangeReputations> GetPlayerFactionChangeReputations()
        {
            List<PlayerFactionChangeReputations> factionChangeReputations = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM player_factionchange_reputations";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var reputation = new PlayerFactionChangeReputations
                        {
                            AllianceId = reader.GetInt32("alliance_id"),
                            HordeId = reader.GetInt32("horde_id")
                        };

                        factionChangeReputations.Add(reputation);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return factionChangeReputations;
        }

        public static List<PlayerFactionChangeSpells> GetPlayerFactionChangeSpells()
        {
            List<PlayerFactionChangeSpells> factionChangeSpells = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM player_factionchange_spells";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var factionChangeSpell = new PlayerFactionChangeSpells
                        {
                            AllianceId = reader.GetInt32("alliance_id"),
                            HordeId = reader.GetInt32("horde_id"),
                            Comment = reader.GetString("comment")
                        };

                        factionChangeSpells.Add(factionChangeSpell);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return factionChangeSpells;
        }

        public static List<PlayerLevelStats> GetPlayerLevelStats()
        {
            List<PlayerLevelStats> levelStats = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM player_levelstats";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var levelStat = new PlayerLevelStats
                        {
                            Race = reader.GetByte("race"),
                            Class = reader.GetByte("class"),
                            Level = reader.GetByte("level"),
                            Str = reader.GetByte("str"),
                            Agi = reader.GetByte("agi"),
                            Sta = reader.GetByte("sta"),
                            Inte = reader.GetByte("inte"),
                            Spi = reader.GetByte("spi")
                        };

                        levelStats.Add(levelStat);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return levelStats;
        }

        public static List<PlayerXpForLevel> GetPlayerXpForLevel()
        {
            List<PlayerXpForLevel> xpForLevels = [];

            using (MySqlConnection connection = new(ConnectionString))
            {
                try
                {
                    connection.Open();

                    MySqlCommand command = connection.CreateCommand();
                    command.CommandText = @"SELECT * FROM player_xp_for_level";

                    using MySqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var xpForLevel = new PlayerXpForLevel
                        {
                            Lvl = reader.GetUInt32("lvl"),
                            XpForNextLevel = reader.GetUInt32("xp_for_next_level")
                        };

                        xpForLevels.Add(xpForLevel);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[ERROR] {ex.Message} {ex.StackTrace}");
                }
            }

            return xpForLevels;
        }
    }
}
