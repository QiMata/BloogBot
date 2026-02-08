using ForegroundBotRunner.Mem;
using Serilog;

namespace ForegroundBotRunner.Questing
{
    /// <summary>
    /// Handles Lua frame interactions for quest pickup and turn-in.
    /// Uses a state machine to click through gossip/quest frames.
    /// </summary>
    public static class QuestNpcInteraction
    {
        /// <summary>
        /// Check if a quest-related frame is visible.
        /// </summary>
        public static bool IsQuestFrameVisible()
        {
            return IsFrameVisible("QuestFrame") || IsFrameVisible("GossipFrame");
        }

        /// <summary>
        /// Try to accept the currently displayed quest. Returns true if AcceptButton was clicked.
        /// </summary>
        public static bool TryAcceptQuest()
        {
            try
            {
                // If quest accept button is visible, click it
                if (IsFrameVisible("QuestFrameAcceptButton"))
                {
                    Functions.LuaCall("QuestFrameAcceptButton:Click()");
                    Log.Debug("[QuestNpc] Clicked AcceptQuest");
                    return true;
                }

                // If gossip frame is showing, look for quest options
                if (IsFrameVisible("GossipFrame"))
                {
                    // Click first available quest option
                    for (int i = 1; i <= 8; i++)
                    {
                        if (IsFrameVisible($"GossipTitleButton{i}"))
                        {
                            Functions.LuaCall($"GossipTitleButton{i}:Click()");
                            Log.Debug("[QuestNpc] Clicked gossip option {Index}", i);
                            return false; // Need another tick to accept
                        }
                    }
                }

                // If quest title buttons are showing (multi-quest NPC)
                for (int i = 1; i <= 8; i++)
                {
                    if (IsFrameVisible($"QuestTitleButton{i}"))
                    {
                        Functions.LuaCall($"QuestTitleButton{i}:Click()");
                        Log.Debug("[QuestNpc] Clicked quest title {Index}", i);
                        return false; // Need another tick
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[QuestNpc] TryAcceptQuest error");
            }
            return false;
        }

        /// <summary>
        /// Try to turn in the currently displayed quest. Returns true if completed successfully.
        /// </summary>
        public static bool TryTurnInQuest()
        {
            try
            {
                // If the complete quest button is visible, click it
                if (IsFrameVisible("QuestFrameCompleteQuestButton"))
                {
                    Functions.LuaCall("QuestFrameCompleteQuestButton:Click()");
                    Log.Debug("[QuestNpc] Clicked CompleteQuest");
                    return true;
                }

                if (IsFrameVisible("QuestFrameCompleteButton"))
                {
                    Functions.LuaCall("QuestFrameCompleteButton:Click()");
                    Log.Debug("[QuestNpc] Clicked CompleteButton");
                    return true;
                }

                // If reward items are visible, select the first one
                if (IsFrameVisible("QuestRewardItem1"))
                {
                    Functions.LuaCall("QuestRewardItem1:Click()");
                    Log.Debug("[QuestNpc] Selected reward 1");
                    return false; // Need another tick to complete
                }

                // If gossip frame is showing, look for completable quest options
                if (IsFrameVisible("GossipFrame"))
                {
                    for (int i = 1; i <= 8; i++)
                    {
                        if (IsFrameVisible($"GossipTitleButton{i}"))
                        {
                            Functions.LuaCall($"GossipTitleButton{i}:Click()");
                            Log.Debug("[QuestNpc] Clicked gossip option {Index} for turn-in", i);
                            return false;
                        }
                    }
                }

                // Quest title buttons
                for (int i = 1; i <= 8; i++)
                {
                    if (IsFrameVisible($"QuestTitleButton{i}"))
                    {
                        Functions.LuaCall($"QuestTitleButton{i}:Click()");
                        Log.Debug("[QuestNpc] Clicked quest title {Index} for turn-in", i);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[QuestNpc] TryTurnInQuest error");
            }
            return false;
        }

        /// <summary>
        /// Close any open quest/gossip frames.
        /// </summary>
        public static void CloseFrames()
        {
            try
            {
                if (IsFrameVisible("QuestFrame"))
                    Functions.LuaCall("QuestFrameCloseButton:Click()");
                if (IsFrameVisible("GossipFrame"))
                    Functions.LuaCall("GossipFrameCloseButton:Click()");
            }
            catch { }
        }

        private static bool IsFrameVisible(string frameName)
        {
            try
            {
                var result = Functions.LuaCallWithResult(
                    $"if {frameName} and {frameName}:IsVisible() then {{0}} = '1' else {{0}} = '0' end");
                return result.Length > 0 && result[0] == "1";
            }
            catch { return false; }
        }
    }
}
