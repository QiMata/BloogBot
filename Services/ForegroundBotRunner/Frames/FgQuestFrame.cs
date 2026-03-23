using GameData.Core.Enums;
using GameData.Core.Frames;
using System;

namespace ForegroundBotRunner.Frames;

public sealed class FgQuestFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult,
    Func<ulong>? npcGuidProvider = null) : IQuestFrame
{
    private const string QuestOrGossipVisibleLua =
        "if (QuestFrame and QuestFrame:IsVisible()) or (GossipFrame and GossipFrame:IsVisible()) then {0} = 1 else {0} = 0 end";
    private const string QuestAcceptVisibleLua =
        "if QuestFrameAcceptButton and QuestFrameAcceptButton:IsVisible() then {0} = 1 else {0} = 0 end";
    private const string QuestCompleteVisibleLua =
        "if (QuestFrameCompleteQuestButton and QuestFrameCompleteQuestButton:IsVisible()) or " +
        "(QuestFrameCompleteButton and QuestFrameCompleteButton:IsVisible()) then {0} = 1 else {0} = 0 end";
    private const string QuestRewardCountLua =
        "if QuestFrame and QuestFrame:IsVisible() then {0} = GetNumQuestChoices() or 0 else {0} = 0 end";
    private const string ProceedLua =
        "local handled = 0; " +
        "if GossipFrame and GossipFrame:IsVisible() then " +
        "  for i = 1, 32 do " +
        "    local button = getglobal('GossipTitleButton' .. i); " +
        "    if button and button:IsVisible() then button:Click(); handled = 1; break; end " +
        "  end " +
        "elseif QuestFrame and QuestFrame:IsVisible() then " +
        "  for i = 1, 32 do " +
        "    local button = getglobal('QuestTitleButton' .. i); " +
        "    if button and button:IsVisible() then button:Click(); handled = 1; break; end " +
        "  end " +
        "end; " +
        "{0} = handled";
    private const string AcceptQuestLua =
        "local handled = 0; " +
        "if QuestFrameAcceptButton and QuestFrameAcceptButton:IsVisible() then " +
        "  QuestFrameAcceptButton:Click(); handled = 1; " +
        "elseif GossipFrame and GossipFrame:IsVisible() then " +
        "  for i = 1, 32 do " +
        "    local button = getglobal('GossipTitleButton' .. i); " +
        "    if button and button:IsVisible() then button:Click(); handled = 1; break; end " +
        "  end " +
        "else " +
        "  for i = 1, 32 do " +
        "    local button = getglobal('QuestTitleButton' .. i); " +
        "    if button and button:IsVisible() then button:Click(); handled = 1; break; end " +
        "  end " +
        "end; " +
        "{0} = handled";
    private const string CompleteQuestLua =
        "local handled = 0; " +
        "if QuestFrameCompleteQuestButton and QuestFrameCompleteQuestButton:IsVisible() then " +
        "  QuestFrameCompleteQuestButton:Click(); handled = 1; " +
        "elseif QuestFrameCompleteButton and QuestFrameCompleteButton:IsVisible() then " +
        "  QuestFrameCompleteButton:Click(); handled = 1; " +
        "elseif QuestRewardItem1 and QuestRewardItem1:IsVisible() then " +
        "  QuestRewardItem1:Click(); handled = 1; " +
        "elseif GossipFrame and GossipFrame:IsVisible() then " +
        "  for i = 1, 32 do " +
        "    local button = getglobal('GossipTitleButton' .. i); " +
        "    if button and button:IsVisible() then button:Click(); handled = 1; break; end " +
        "  end " +
        "else " +
        "  for i = 1, 32 do " +
        "    local button = getglobal('QuestTitleButton' .. i); " +
        "    if button and button:IsVisible() then button:Click(); handled = 1; break; end " +
        "  end " +
        "end; " +
        "{0} = handled";

    private readonly Func<ulong> _npcGuidProvider = npcGuidProvider ?? (() => 0UL);

    public bool IsOpen => FrameLuaReader.ReadBool(luaCallWithResult, QuestOrGossipVisibleLua);

    public void Close()
    {
        luaCall("if QuestFrame and QuestFrame:IsVisible() then QuestFrameCloseButton:Click() end");
        luaCall("if GossipFrame and GossipFrame:IsVisible() then CloseGossip() end");
    }

    public ulong NpcGuid => _npcGuidProvider();

    public void AcceptQuest() => luaCallWithResult(AcceptQuestLua);

    public void DeclineQuest()
        => luaCall("if QuestFrameDeclineButton and QuestFrameDeclineButton:IsVisible() then QuestFrameDeclineButton:Click() end");

    public void CompleteQuest() => CompleteQuest(parReward: null);

    public QuestFrameState State
    {
        get
        {
            if (FrameLuaReader.ReadBool(luaCallWithResult, QuestAcceptVisibleLua))
                return QuestFrameState.Accept;

            if (FrameLuaReader.ReadBool(luaCallWithResult, QuestCompleteVisibleLua))
                return QuestFrameState.Complete;

            return IsOpen ? QuestFrameState.Continue : QuestFrameState.Greeting;
        }
    }

    public int QuestFrameId => 0;

    public int RewardCount => FrameLuaReader.ReadInt(luaCallWithResult, QuestRewardCountLua);

    public bool Proceed() => FrameLuaReader.ReadBool(luaCallWithResult, ProceedLua);

    public void CompleteQuest(int? parReward = null)
    {
        if (parReward.HasValue)
        {
            int rewardButtonIndex = Math.Max(0, parReward.Value) + 1;
            luaCall(
                $"local reward = getglobal('QuestRewardItem{rewardButtonIndex}'); " +
                "if reward and reward:IsVisible() then reward:Click() end");
        }

        luaCallWithResult(CompleteQuestLua);
    }
}
