using GameData.Core.Frames;
using System;

namespace ForegroundBotRunner.Frames;

public sealed class FgCraftFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult) : ICraftFrame
{
    private const string CraftVisibleLua =
        "if CraftFrame and CraftFrame:IsVisible() then {0} = 1 else {0} = 0 end";

    public bool IsOpen => FrameLuaReader.ReadBool(luaCallWithResult, CraftVisibleLua);

    public bool HasMaterialsNeeded(int slot)
    {
        int craftIndex = Math.Max(0, slot) + 1;
        string[] result = luaCallWithResult(
            "local ok = 1; " +
            $"local craftIndex = {craftIndex}; " +
            "if not (CraftFrame and CraftFrame:IsVisible()) then ok = 0 " +
            "else " +
            "  local _, _, craftType = GetCraftInfo(craftIndex); " +
            "  if craftType == 'header' then ok = 0 " +
            "  else " +
            "    local reagentCount = GetCraftNumReagents(craftIndex) or 0; " +
            "    for reagentIndex = 1, reagentCount do " +
            "      local _, _, requiredCount, playerCount = GetCraftReagentInfo(craftIndex, reagentIndex); " +
            "      if (playerCount or 0) < (requiredCount or 0) then ok = 0 break end; " +
            "    end; " +
            "  end; " +
            "end; " +
            "{0} = ok");

        return result.Length > 0 && result[0] == "1";
    }

    public void Craft(int slot)
    {
        int craftIndex = Math.Max(0, slot) + 1;
        luaCall($"if CraftFrame and CraftFrame:IsVisible() then DoCraft({craftIndex}) end");
    }
}
