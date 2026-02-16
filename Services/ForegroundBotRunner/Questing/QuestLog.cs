using ForegroundBotRunner.Mem;
using Serilog;
using System;
using System.Collections.Generic;

namespace ForegroundBotRunner.Questing
{
    /// <summary>
    /// Reads the player's quest log via Lua and tracks quest state.
    /// </summary>
    public static class QuestLog
    {
        /// <summary>
        /// Get all quests currently in the player's quest log.
        /// </summary>
        public static List<QuestLogEntry> GetActiveQuests()
        {
            var quests = new List<QuestLogEntry>();
            try
            {
                var countResult = Functions.LuaCallWithResult("{0} = GetNumQuestLogEntries()");
                if (countResult.Length == 0 || !int.TryParse(countResult[0], out var count))
                    return quests;

                for (int i = 1; i <= count; i++)
                {
                    // GetQuestLogTitle returns: title, level, questTag, suggestedGroup, isHeader, isCollapsed, isComplete, isDaily
                    var results = Functions.LuaCallWithResult(
                        $"{{0}}, {{1}}, {{2}}, {{3}}, {{4}}, {{5}}, {{6}} = GetQuestLogTitle({i})");

                    if (results.Length < 7) continue;

                    var title = results[0];
                    var isHeader = results[4] == "1";
                    if (isHeader || string.IsNullOrEmpty(title)) continue;

                    int.TryParse(results[1], out var level);
                    var isComplete = results[6] == "1";

                    // Get the quest ID
                    // SelectQuestLogEntry sets the selected quest, then GetQuestLogQuestID returns it
                    var idResult = Functions.LuaCallWithResult(
                        $"SelectQuestLogEntry({i}); {{0}} = GetQuestLogQuestID()");
                    int.TryParse(idResult.Length > 0 ? idResult[0] : "0", out var questId);

                    quests.Add(new QuestLogEntry
                    {
                        Index = i,
                        QuestId = questId,
                        Title = title,
                        Level = level,
                        IsComplete = isComplete,
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[QuestLog] GetActiveQuests error");
            }

            return quests;
        }

        /// <summary>
        /// Get objective progress for a specific quest log entry.
        /// </summary>
        public static List<QuestObjective> GetQuestObjectives(int questLogIndex)
        {
            var objectives = new List<QuestObjective>();
            try
            {
                var countResult = Functions.LuaCallWithResult(
                    $"SelectQuestLogEntry({questLogIndex}); {{0}} = GetNumQuestLeaderBoards()");
                if (countResult.Length == 0 || !int.TryParse(countResult[0], out var count))
                    return objectives;

                for (int i = 1; i <= count; i++)
                {
                    // GetQuestLogLeaderBoard returns: text, type, finished
                    var results = Functions.LuaCallWithResult(
                        $"{{0}}, {{1}}, {{2}} = GetQuestLogLeaderBoard({i}, {questLogIndex})");

                    if (results.Length < 3) continue;

                    var text = results[0]; // e.g., "Kobold Vermin slain: 5/10" or "Linen Cloth: 3/8"
                    var type = results[1]; // "monster", "item", "object", "event"
                    var finished = results[2] == "1";

                    // Parse progress from text like "X slain: 5/10" or "ItemName: 3/8"
                    int current = 0, required = 0;
                    var colonIdx = text.LastIndexOf(':');
                    if (colonIdx >= 0)
                    {
                        var progressPart = text[(colonIdx + 1)..].Trim();
                        var slashIdx = progressPart.IndexOf('/');
                        if (slashIdx >= 0)
                        {
                            int.TryParse(progressPart[..slashIdx], out current);
                            int.TryParse(progressPart[(slashIdx + 1)..], out required);
                        }
                    }

                    objectives.Add(new QuestObjective
                    {
                        Index = i,
                        Text = text,
                        Type = type,
                        IsFinished = finished,
                        Current = current,
                        Required = required,
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[QuestLog] GetQuestObjectives error");
            }

            return objectives;
        }

        /// <summary>
        /// Get the number of quests in the log (excluding headers).
        /// </summary>
        public static int GetQuestCount()
        {
            try
            {
                var results = Functions.LuaCallWithResult(
                    "{0} = 0; local n = GetNumQuestLogEntries(); " +
                    "for i=1,n do local _,_,_,_,h = GetQuestLogTitle(i); if not h then {0} = {0} + 1 end end");
                if (results.Length > 0 && int.TryParse(results[0], out var count))
                    return count;
            }
            catch { }
            return 0;
        }
    }

    public class QuestLogEntry
    {
        public int Index { get; set; }
        public int QuestId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Level { get; set; }
        public bool IsComplete { get; set; }
    }

    public class QuestObjective
    {
        public int Index { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsFinished { get; set; }
        public int Current { get; set; }
        public int Required { get; set; }
    }
}
