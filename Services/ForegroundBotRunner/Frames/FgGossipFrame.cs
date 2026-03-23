using GameData.Core.Enums;
using GameData.Core.Frames;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ForegroundBotRunner.Frames;

public sealed class FgGossipFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult,
    Func<ulong>? npcGuidProvider = null) : IGossipFrame
{
    private const string GossipVisibleLua =
        "if GossipFrame and GossipFrame:IsVisible() then {0} = 1 else {0} = 0 end";
    private const string GossipOptionCountLua =
        "if GossipFrame and GossipFrame:IsVisible() then {0} = GetNumGossipOptions() or 0 else {0} = 0 end";

    private readonly Func<ulong> _npcGuidProvider = npcGuidProvider ?? (() => 0UL);

    public bool IsOpen => FrameLuaReader.ReadBool(luaCallWithResult, GossipVisibleLua);

    public void Close() => luaCall("if GossipFrame and GossipFrame:IsVisible() then CloseGossip() end");

    public ulong NPCGuid => _npcGuidProvider();

    public void SelectGossipOption(int parOptionIndex)
        => luaCall($"if GossipFrame and GossipFrame:IsVisible() then SelectGossipOption({Math.Max(1, parOptionIndex)}) end");

    public void SelectFirstGossipOfType(DialogType type)
    {
        try
        {
            var dialog = new DialogFrame();
            if (dialog.DialogOptions.Any(option => option.Type == type))
            {
                dialog.SelectFirstGossipOfType(type);
                return;
            }
        }
        catch
        {
        }

        SelectGossipOption(1);
    }

    public List<GossipOption> Options
        => Enumerable.Range(0, FrameLuaReader.ReadInt(luaCallWithResult, GossipOptionCountLua))
            .Select(_ => (GossipOption)new FgGossipOption())
            .ToList();

    public List<QuestOption> Quests => [];

    private sealed class FgGossipOption : GossipOption
    {
    }
}
