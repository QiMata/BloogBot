using System.Collections.Generic;

namespace BotRunner.Interfaces;

/// <summary>
/// Interface for quest-related database operations.
/// </summary>
public interface IQuestRepository
{
    /// <summary>
    /// Get quest template by ID from the database.
    /// </summary>
    QuestTemplateData? GetQuestTemplateById(int questId);

    /// <summary>
    /// Get creatures that are related to a quest (quest givers, turn-ins).
    /// </summary>
    IEnumerable<int> GetQuestRelatedNpcIds(int questId);

    /// <summary>
    /// Get creature spawns by creature template ID.
    /// </summary>
    IEnumerable<CreatureSpawnData> GetCreatureSpawnsById(int creatureId);

    /// <summary>
    /// Get creatures that drop a specific item.
    /// </summary>
    IEnumerable<CreatureSpawnData> GetCreaturesByLootableItemId(int itemId);

    /// <summary>
    /// Get game objects that contain a specific item.
    /// </summary>
    IEnumerable<GameObjectSpawnData> GetGameObjectsByLootableItemId(int itemId);

    /// <summary>
    /// Get game object spawns by template ID.
    /// </summary>
    IEnumerable<GameObjectSpawnData> GetGameObjectSpawnsById(int gameObjectId);

    /// <summary>
    /// Get creature template by ID.
    /// </summary>
    CreatureTemplateData? GetCreatureTemplateById(int creatureId);

    /// <summary>
    /// Get item template by ID.
    /// </summary>
    ItemTemplateData? GetItemTemplateById(int itemId);
}

/// <summary>
/// Quest template data from database.
/// </summary>
public class QuestTemplateData
{
    public int Entry { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SrcItemId { get; set; }
    public int ReqCreatureOrGOId1 { get; set; }
    public int ReqCreatureOrGOId2 { get; set; }
    public int ReqCreatureOrGOId3 { get; set; }
    public int ReqCreatureOrGOId4 { get; set; }
    public int ReqCreatureOrGOCount1 { get; set; }
    public int ReqCreatureOrGOCount2 { get; set; }
    public int ReqCreatureOrGOCount3 { get; set; }
    public int ReqCreatureOrGOCount4 { get; set; }
    public int ReqItemId1 { get; set; }
    public int ReqItemId2 { get; set; }
    public int ReqItemId3 { get; set; }
    public int ReqItemId4 { get; set; }
    public int ReqItemCount1 { get; set; }
    public int ReqItemCount2 { get; set; }
    public int ReqItemCount3 { get; set; }
    public int ReqItemCount4 { get; set; }
    public int RewChoiceItemId1 { get; set; }
    public int RewChoiceItemId2 { get; set; }
    public int RewChoiceItemId3 { get; set; }
    public int RewChoiceItemId4 { get; set; }
    public int RewChoiceItemId5 { get; set; }
    public int RewChoiceItemId6 { get; set; }
}

/// <summary>
/// Creature spawn data from database.
/// </summary>
public class CreatureSpawnData
{
    public int Id { get; set; }
    public int CreatureId { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public int MapId { get; set; }
}

/// <summary>
/// Creature template data from database.
/// </summary>
public class CreatureTemplateData
{
    public int Entry { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Game object spawn data from database.
/// </summary>
public class GameObjectSpawnData
{
    public int Id { get; set; }
    public int GameObjectId { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
    public int MapId { get; set; }
}

/// <summary>
/// Item template data from database.
/// </summary>
public class ItemTemplateData
{
    public int Entry { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ItemClass { get; set; }
    public int ItemSubClass { get; set; }
    public int InventoryType { get; set; }
}
