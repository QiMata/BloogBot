using BotRunner.Interfaces;
using Microsoft.Data.Sqlite;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

namespace BotRunner.Repository;

/// <summary>
/// SQLite implementation of IQuestRepository.
/// Reads quest, creature, and game object data from a MaNGOS-schema database.
///
/// Expected tables (MaNGOS vanilla 1.12.1 schema):
/// - quest_template
/// - creature (spawns)
/// - creature_template
/// - creature_loot_template
/// - creature_involvedrelation (quest givers/turnins)
/// - gameobject (spawns)
/// - gameobject_template
/// - gameobject_loot_template
/// - items / item_template
///
/// Database search order:
/// 1. Documents/BloogBot/database.db
/// 2. Working directory/database.db
/// </summary>
public class SqliteQuestRepository : IQuestRepository
{
    private readonly string _connectionString;

    public SqliteQuestRepository(string databasePath)
    {
        _connectionString = $"Data Source={databasePath};Mode=ReadOnly";
        Log.Information("[QuestRepo] Using database: {Path}", databasePath);
    }

    public static SqliteQuestRepository? TryCreate()
    {
        var paths = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BloogBot", "database.db"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.db"),
            Path.Combine(Directory.GetCurrentDirectory(), "database.db")
        };

        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;

            var info = new FileInfo(path);
            if (info.Length < 1024) // Too small to be a valid DB
            {
                Log.Debug("[QuestRepo] Skipping {Path} (too small: {Size} bytes)", path, info.Length);
                continue;
            }

            try
            {
                var repo = new SqliteQuestRepository(path);
                // Verify connectivity
                using var conn = new SqliteConnection(repo._connectionString);
                conn.Open();
                return repo;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[QuestRepo] Failed to open {Path}", path);
            }
        }

        Log.Information("[QuestRepo] No quest database found - questing disabled");
        return null;
    }

    public QuestTemplateData? GetQuestTemplateById(int questId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT entry, Title, SrcItemId,
            ReqCreatureOrGOId1, ReqCreatureOrGOId2, ReqCreatureOrGOId3, ReqCreatureOrGOId4,
            ReqCreatureOrGOCount1, ReqCreatureOrGOCount2, ReqCreatureOrGOCount3, ReqCreatureOrGOCount4,
            ReqItemId1, ReqItemId2, ReqItemId3, ReqItemId4,
            ReqItemCount1, ReqItemCount2, ReqItemCount3, ReqItemCount4,
            RewChoiceItemId1, RewChoiceItemId2, RewChoiceItemId3, RewChoiceItemId4, RewChoiceItemId5, RewChoiceItemId6
            FROM quest_template WHERE entry = $id";
        cmd.Parameters.AddWithValue("$id", questId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new QuestTemplateData
        {
            Entry = reader.GetInt32(0),
            Title = reader.GetString(1),
            SrcItemId = reader.GetInt32(2),
            ReqCreatureOrGOId1 = reader.GetInt32(3),
            ReqCreatureOrGOId2 = reader.GetInt32(4),
            ReqCreatureOrGOId3 = reader.GetInt32(5),
            ReqCreatureOrGOId4 = reader.GetInt32(6),
            ReqCreatureOrGOCount1 = reader.GetInt32(7),
            ReqCreatureOrGOCount2 = reader.GetInt32(8),
            ReqCreatureOrGOCount3 = reader.GetInt32(9),
            ReqCreatureOrGOCount4 = reader.GetInt32(10),
            ReqItemId1 = reader.GetInt32(11),
            ReqItemId2 = reader.GetInt32(12),
            ReqItemId3 = reader.GetInt32(13),
            ReqItemId4 = reader.GetInt32(14),
            ReqItemCount1 = reader.GetInt32(15),
            ReqItemCount2 = reader.GetInt32(16),
            ReqItemCount3 = reader.GetInt32(17),
            ReqItemCount4 = reader.GetInt32(18),
            RewChoiceItemId1 = reader.GetInt32(19),
            RewChoiceItemId2 = reader.GetInt32(20),
            RewChoiceItemId3 = reader.GetInt32(21),
            RewChoiceItemId4 = reader.GetInt32(22),
            RewChoiceItemId5 = reader.GetInt32(23),
            RewChoiceItemId6 = reader.GetInt32(24),
        };
    }

    public IEnumerable<int> GetQuestRelatedNpcIds(int questId)
    {
        var ids = new List<int>();
        using var conn = Open();

        // Quest givers
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id FROM creature_questrelation WHERE quest = $id";
            cmd.Parameters.AddWithValue("$id", questId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                ids.Add(reader.GetInt32(0));
        }

        // Quest turn-in NPCs
        using (var cmd2 = conn.CreateCommand())
        {
            cmd2.CommandText = "SELECT id FROM creature_involvedrelation WHERE quest = $id";
            cmd2.Parameters.AddWithValue("$id", questId);
            using var reader2 = cmd2.ExecuteReader();
            while (reader2.Read())
            {
                var id = reader2.GetInt32(0);
                if (!ids.Contains(id))
                    ids.Add(id);
            }
        }

        return ids;
    }

    public IEnumerable<CreatureSpawnData> GetCreatureSpawnsById(int creatureId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT guid, id, position_x, position_y, position_z, map FROM creature WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", creatureId);

        var spawns = new List<CreatureSpawnData>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            spawns.Add(new CreatureSpawnData
            {
                Id = reader.GetInt32(0),
                CreatureId = reader.GetInt32(1),
                PositionX = reader.GetFloat(2),
                PositionY = reader.GetFloat(3),
                PositionZ = reader.GetFloat(4),
                MapId = reader.GetInt32(5),
            });
        }
        return spawns;
    }

    public IEnumerable<CreatureSpawnData> GetCreaturesByLootableItemId(int itemId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT DISTINCT c.guid, c.id, c.position_x, c.position_y, c.position_z, c.map
            FROM creature c
            JOIN creature_template ct ON c.id = ct.entry
            JOIN creature_loot_template clt ON ct.entry = clt.entry
            WHERE clt.item = $itemId";
        cmd.Parameters.AddWithValue("$itemId", itemId);

        var spawns = new List<CreatureSpawnData>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            spawns.Add(new CreatureSpawnData
            {
                Id = reader.GetInt32(0),
                CreatureId = reader.GetInt32(1),
                PositionX = reader.GetFloat(2),
                PositionY = reader.GetFloat(3),
                PositionZ = reader.GetFloat(4),
                MapId = reader.GetInt32(5),
            });
        }
        return spawns;
    }

    public IEnumerable<GameObjectSpawnData> GetGameObjectsByLootableItemId(int itemId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT DISTINCT g.guid, g.id, g.position_x, g.position_y, g.position_z, g.map
            FROM gameobject g
            JOIN gameobject_template gt ON g.id = gt.entry
            JOIN gameobject_loot_template glt ON glt.entry = gt.data1
            WHERE glt.item = $itemId";
        cmd.Parameters.AddWithValue("$itemId", itemId);

        var spawns = new List<GameObjectSpawnData>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            spawns.Add(new GameObjectSpawnData
            {
                Id = reader.GetInt32(0),
                GameObjectId = reader.GetInt32(1),
                PositionX = reader.GetFloat(2),
                PositionY = reader.GetFloat(3),
                PositionZ = reader.GetFloat(4),
                MapId = reader.GetInt32(5),
            });
        }
        return spawns;
    }

    public IEnumerable<GameObjectSpawnData> GetGameObjectSpawnsById(int gameObjectId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT guid, id, position_x, position_y, position_z, map FROM gameobject WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", gameObjectId);

        var spawns = new List<GameObjectSpawnData>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            spawns.Add(new GameObjectSpawnData
            {
                Id = reader.GetInt32(0),
                GameObjectId = reader.GetInt32(1),
                PositionX = reader.GetFloat(2),
                PositionY = reader.GetFloat(3),
                PositionZ = reader.GetFloat(4),
                MapId = reader.GetInt32(5),
            });
        }
        return spawns;
    }

    public CreatureTemplateData? GetCreatureTemplateById(int creatureId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT entry, name FROM creature_template WHERE entry = $id";
        cmd.Parameters.AddWithValue("$id", creatureId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new CreatureTemplateData
        {
            Entry = reader.GetInt32(0),
            Name = reader.GetString(1),
        };
    }

    public ItemTemplateData? GetItemTemplateById(int itemId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        // Try 'items' table first (legacy schema), fallback to 'item_template'
        cmd.CommandText = "SELECT entry, name, class, subclass, InventoryType FROM item_template WHERE entry = $id";
        cmd.Parameters.AddWithValue("$id", itemId);

        try
        {
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new ItemTemplateData
            {
                Entry = reader.GetInt32(0),
                Name = reader.GetString(1),
                ItemClass = reader.GetInt32(2),
                ItemSubClass = reader.GetInt32(3),
                InventoryType = reader.GetInt32(4),
            };
        }
        catch
        {
            // Try alternate table name
            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = "SELECT ItemId, Name, Class, Subclass, InventoryType FROM items WHERE ItemId = $id";
            cmd2.Parameters.AddWithValue("$id", itemId);

            using var reader2 = cmd2.ExecuteReader();
            if (!reader2.Read()) return null;

            return new ItemTemplateData
            {
                Entry = reader2.GetInt32(0),
                Name = reader2.GetString(1),
                ItemClass = reader2.GetInt32(2),
                ItemSubClass = reader2.GetInt32(3),
                InventoryType = reader2.GetInt32(4),
            };
        }
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
