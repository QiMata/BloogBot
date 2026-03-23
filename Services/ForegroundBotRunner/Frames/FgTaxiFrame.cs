using GameData.Core.Frames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ForegroundBotRunner.Frames;

public sealed class FgTaxiFrame(
    Action<string> luaCall,
    Func<string, string[]> luaCallWithResult) : ITaxiFrame
{
    private const string TaxiVisibleLua =
        "if TaxiFrame and TaxiFrame:IsVisible() then {0} = 1 else {0} = 0 end";
    private const string TaxiNodeCountLua =
        "if TaxiFrame and TaxiFrame:IsVisible() then {0} = NumTaxiNodes() or 0 else {0} = 0 end";
    private const string TaxiNodeDataLuaPrefix =
        "{0}, {1}, {2} = TaxiNodeName(";

    private static readonly FieldInfo? StatusField =
        typeof(TaxiNode).GetField("<Status>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NameField =
        typeof(TaxiNode).GetField("<Name>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? CostField =
        typeof(TaxiNode).GetField("<Cost>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? NodeNumberField =
        typeof(TaxiNode).GetField("<NodeNumber>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

    public bool IsOpen => FrameLuaReader.ReadBool(luaCallWithResult, TaxiVisibleLua);

    public void Close() => luaCall("if TaxiFrame and TaxiFrame:IsVisible() then CloseTaxiMap() end");

    public List<TaxiNode> Nodes
    {
        get
        {
            int count = FrameLuaReader.ReadInt(luaCallWithResult, TaxiNodeCountLua);
            var nodes = new List<TaxiNode>(count + 1)
            {
                new FgTaxiNode("NONE", string.Empty, 0, 0)
            };

            for (int i = 1; i <= count; i++)
            {
                var result = luaCallWithResult(
                    $"{TaxiNodeDataLuaPrefix}{i}) or '', TaxiNodeCost({i}) or 0, TaxiNodeGetType({i}) or ''");
                string name = result.Length > 0 ? result[0] : string.Empty;
                int cost = result.Length > 1 && int.TryParse(result[1], out int parsedCost) ? parsedCost : 0;
                string status = result.Length > 2 ? result[2] : string.Empty;
                nodes.Add(new FgTaxiNode(status, name, cost, i));
            }

            return nodes;
        }
    }

    public int NodesAvailable => Math.Max(0, Nodes.Count - 1);

    public string CurrentNodeName
        => Nodes.Skip(1).FirstOrDefault(node => string.Equals(node.Status, "CURRENT", StringComparison.OrdinalIgnoreCase))?.Name ?? string.Empty;

    public void SelectNodeByNumber(int parNodeNumber)
        => luaCall($"if TaxiFrame and TaxiFrame:IsVisible() then TakeTaxiNode({Math.Max(1, parNodeNumber)}) end");

    public void SelectNodeByName(string parNodeName)
    {
        var node = Nodes.Skip(1)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, parNodeName, StringComparison.OrdinalIgnoreCase));
        if (node != null)
            SelectNodeByNumber(node.NodeNumber);
    }

    public bool HasNodeUnlocked(int nodeId)
    {
        var node = Nodes.Skip(1).FirstOrDefault(candidate => candidate.NodeNumber == nodeId);
        if (node == null)
            return false;

        return string.Equals(node.Status, "CURRENT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.Status, "REACHABLE", StringComparison.OrdinalIgnoreCase);
    }

    public void SelectNode(int nodeId) => SelectNodeByNumber(nodeId);

    private sealed class FgTaxiNode : TaxiNode
    {
        internal FgTaxiNode(string status, string name, int cost, int nodeNumber)
        {
            StatusField?.SetValue(this, status);
            NameField?.SetValue(this, name);
            CostField?.SetValue(this, cost);
            NodeNumberField?.SetValue(this, nodeNumber);
        }
    }
}
