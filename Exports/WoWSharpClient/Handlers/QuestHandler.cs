using System.IO;
using GameData.Core.Enums;
using Serilog;

namespace WoWSharpClient.Handlers;

public static class QuestHandler
{
    /// <summary>
    /// Parses SMSG_QUESTUPDATE_COMPLETE (0x198).
    /// Format: uint32 questId
    /// Marks the quest as completed in the player's quest log.
    /// </summary>
    public static void HandleQuestUpdateComplete(Opcode opcode, byte[] data)
    {
        if (data.Length < 4)
        {
            Log.Warning("[QuestHandler] Truncated SMSG_QUESTUPDATE_COMPLETE ({Len} bytes)", data.Length);
            return;
        }

        using var reader = new BinaryReader(new MemoryStream(data));
        uint questId = reader.ReadUInt32();

        var player = WoWSharpObjectManager.Instance.Player as Models.WoWPlayer;
        if (player == null)
        {
            Log.Warning("[QuestHandler] SMSG_QUESTUPDATE_COMPLETE for quest {QuestId} but no local player", questId);
            return;
        }

        for (int i = 0; i < player.QuestLog.Length; i++)
        {
            if (player.QuestLog[i].QuestId == questId)
            {
                var prev = player.QuestLog[i].QuestState;
                player.QuestLog[i].QuestState = 1; // QUEST_STATE_COMPLETE
                Log.Information("[QuestFieldDiff] QuestLog[{Index}].QuestState: {Prev} -> 1 (SMSG_QUESTUPDATE_COMPLETE)",
                    i, prev);
                return;
            }
        }

        Log.Debug("[QuestHandler] SMSG_QUESTUPDATE_COMPLETE for quest {QuestId} not found in quest log", questId);
    }

    /// <summary>
    /// Parses SMSG_QUESTUPDATE_ADD_KILL (0x199).
    /// Format: uint32 questId, uint32 creatureEntry, uint32 killCount, uint32 requiredCount, uint64 guid
    /// Updates kill counters in the player's quest log.
    /// </summary>
    public static void HandleQuestUpdateAddKill(Opcode opcode, byte[] data)
    {
        if (data.Length < 16)
        {
            Log.Warning("[QuestHandler] Truncated SMSG_QUESTUPDATE_ADD_KILL ({Len} bytes)", data.Length);
            return;
        }

        using var reader = new BinaryReader(new MemoryStream(data));
        uint questId = reader.ReadUInt32();
        uint creatureEntry = reader.ReadUInt32();
        uint killCount = reader.ReadUInt32();
        uint requiredCount = reader.ReadUInt32();

        Log.Debug("[QuestHandler] SMSG_QUESTUPDATE_ADD_KILL quest={QuestId} creature={Creature} count={Count}/{Required}",
            questId, creatureEntry, killCount, requiredCount);
    }
}
