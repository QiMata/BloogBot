using GameData.Core.Frames;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ForegroundBotRunner.Frames;

public sealed class FgQuestGreetingFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult) : IQuestGreetingFrame
{
    private const string QuestGreetingVisibleLua =
        "if QuestGreetingFrame and QuestGreetingFrame:IsVisible() then {0} = 1 else {0} = 0 end";
    private const string QuestGreetingCountLua =
        "local count = 0; " +
        "local frame = QuestGreetingFrame; " +
        "if frame and frame:IsVisible() then " +
        "  for i = 1, 32 do " +
        "    local button = getglobal('QuestTitleButton' .. i); " +
        "    if button and button:IsVisible() then count = count + 1 end " +
        "  end " +
        "end; " +
        "{0} = count";

    public bool IsOpen => FrameLuaReader.ReadBool(luaCallWithResult, QuestGreetingVisibleLua);

    public void Close()
        => luaCall("if QuestGreetingFrame and QuestGreetingFrame:IsVisible() and QuestGreetingFrameCloseButton then QuestGreetingFrameCloseButton:Click() end");

    public void AcceptQuest(int parId) => SelectQuest(parId);

    public void CompleteQuest(int parId) => SelectQuest(parId);

    public List<QuestOption> Quests
        => Enumerable.Range(0, FrameLuaReader.ReadInt(luaCallWithResult, QuestGreetingCountLua))
            .Select(_ => (QuestOption)new FgQuestGreetingOption())
            .ToList();

    private void SelectQuest(int questIndex)
    {
        int buttonIndex = Math.Max(0, questIndex) + 1;
        luaCall(
            $"local button = getglobal('QuestTitleButton{buttonIndex}'); " +
            "if button and button:IsVisible() then button:Click() end");
    }

    private sealed class FgQuestGreetingOption : QuestOption
    {
    }
}
