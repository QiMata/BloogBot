using GameData.Core.Frames;
using System;
using System.Collections.Generic;
using System.Linq;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Frames;

/// <summary>
/// Minimal BG taxi-frame surface backed by <see cref="IFlightMasterNetworkClientComponent"/>.
/// Routes <see cref="ITaxiFrame"/> operations through the BG packet path
/// (CMSG_ACTIVATETAXI / CMSG_TAXIQUERYAVAILABLENODES) so
/// InteractionSequenceBuilder's "Take Flight" sequence stops short-circuiting
/// with "TaxiFrame is null -- requires FG bot or packet-based path" on BG
/// bots. Closes S1.18.
///
/// <para>
/// <strong>NodeNumber semantics.</strong> FG's <see cref="ITaxiFrame"/>
/// surfaces a 1-based UI-slot index (the position in the open TaxiFrame's
/// node list). The BG <see cref="IFlightMasterNetworkClientComponent"/>
/// instead deals in DBC taxi-node IDs (e.g. 26 = Crossroads). To keep the
/// dispatcher's <c>SelectNodeByNumber(int)</c> / <c>HasNodeUnlocked(int)</c>
/// callers working, this frame populates <c>TaxiNode.NodeNumber</c> with
/// the DBC node ID directly. Callers that hold an FG-style 1-based UI slot
/// must remap via <c>Nodes[slot].NodeNumber</c> first; current
/// <c>TakeFlightPathTask</c> and related callers already prefer DBC IDs.
/// </para>
///
/// <para>
/// <strong>Name lookup gap.</strong>
/// <see cref="IFlightMasterNetworkClientComponent.AvailableTaxiNodes"/>
/// surfaces only DBC node IDs, with no public name-by-id accessor.
/// <c>TaxiNode.Name</c> is therefore populated as the stringified node ID
/// so name-based lookups (<see cref="ITaxiFrame.SelectNodeByName(string)"/>,
/// <see cref="ITaxiFrame.CurrentNodeName"/>) work positionally against the
/// same ID without requiring a DBC lookup. <strong>TODO (agent-extension):</strong>
/// extend <see cref="IFlightMasterNetworkClientComponent"/> with a
/// <c>GetNodeName(uint nodeId)</c> accessor backed by either TaxiNodes.dbc
/// or the cached <c>TaxiNodeData</c> record (already defined in
/// AgentData.cs but unused by this component), then rewrite the
/// <c>Name</c> projection here. Mirrors the
/// <see cref="NetworkTrainerFrame"/> / <see cref="NetworkCraftFrame"/>
/// "let server arbitrate" approach — server-side
/// CMSG_ACTIVATETAXI is the authoritative reject path
/// (TAXI_ERR_NO_PATH / TAXI_ERR_NOT_ENOUGH_MONEY).
/// </para>
///
/// <para>
/// <strong>Cost.</strong> Backed by
/// <see cref="IFlightMasterNetworkClientComponent.GetFlightCost(uint)"/>,
/// which returns the cost in copper or null when unknown (it is populated
/// post-flight from SMSG_ACTIVATETAXIREPLY, so it is typically null for
/// destinations the bot has not flown to yet). Unknown cost projects to 0
/// so the dispatcher's "Has Enough Gold" gate proceeds and the server-side
/// activation packet produces the authoritative TAXI_ERR_NOT_ENOUGH_MONEY
/// failure if the player cannot afford the route.
/// </para>
/// </summary>
public sealed class NetworkTaxiFrame(Func<IFlightMasterNetworkClientComponent?> resolveFlightMasterAgent) : ITaxiFrame
{
    private const string CurrentStatus = "CURRENT";
    private const string ReachableStatus = "REACHABLE";

    private ulong _lastSelectedFlightMasterGuid;

    public bool IsOpen => resolveFlightMasterAgent()?.IsTaxiMapOpen == true;

    public void Close()
    {
        var agent = resolveFlightMasterAgent();
        if (agent?.IsTaxiMapOpen != true) return;
        agent.CloseTaxiMapAsync().GetAwaiter().GetResult();
    }

    public List<TaxiNode> Nodes
    {
        get
        {
            var agent = resolveFlightMasterAgent();
            // FG seeds index 0 with a sentinel "NONE" node so the 1-based UI
            // slot indexer maps cleanly to Nodes[slot]. Mirror that here so
            // dispatcher code shared with FG keeps the same indexing.
            var nodes = new List<TaxiNode> { new BgTaxiNode("NONE", string.Empty, 0, 0) };
            if (agent == null) return nodes;

            var ids = agent.AvailableTaxiNodes;
            if (ids == null || ids.Count == 0) return nodes;

            var currentId = agent.CurrentNodeId ?? 0u;
            foreach (var nodeId in ids)
            {
                var status = nodeId == currentId && currentId != 0u
                    ? CurrentStatus
                    : ReachableStatus;
                var cost = (int)(agent.GetFlightCost(nodeId) ?? 0u);
                // Name is the stringified DBC id pending agent extension --
                // see class doc TODO. SelectNodeByName matches the same form.
                nodes.Add(new BgTaxiNode(status, nodeId.ToString(), cost, (int)nodeId));
            }

            return nodes;
        }
    }

    public int NodesAvailable
    {
        get
        {
            var agent = resolveFlightMasterAgent();
            return agent?.AvailableTaxiNodes?.Count ?? 0;
        }
    }

    public string CurrentNodeName
    {
        get
        {
            var currentId = resolveFlightMasterAgent()?.CurrentNodeId ?? 0u;
            return currentId == 0u ? string.Empty : currentId.ToString();
        }
    }

    public void SelectNodeByNumber(int parNodeNumber)
        => SelectNode(parNodeNumber);

    public void SelectNodeByName(string parNodeName)
    {
        if (string.IsNullOrEmpty(parNodeName)) return;
        // Names project to stringified DBC ids -- parse and dispatch through
        // the same node-id path. Drops the lookup if the caller passed a
        // human-readable name (no DBC table available on BG yet -- see TODO).
        if (!uint.TryParse(parNodeName, out var nodeId)) return;
        SelectNode((int)nodeId);
    }

    public bool HasNodeUnlocked(int nodeId)
    {
        if (nodeId <= 0) return false;
        var agent = resolveFlightMasterAgent();
        return agent?.IsNodeAvailable((uint)nodeId) == true;
    }

    public void SelectNode(int nodeId)
    {
        if (nodeId <= 0) return;
        var agent = resolveFlightMasterAgent();
        if (agent == null) return;

        // Source node is the current (nearest) taxi node from
        // SMSG_SHOWTAXINODES. Without it the server rejects CMSG_ACTIVATETAXI
        // with TAXI_ERR_NO_SUCH_PATH, so bail rather than send a packet that
        // will always fail.
        var sourceNodeId = agent.CurrentNodeId ?? 0u;
        if (sourceNodeId == 0u) return;

        // Flight master GUID: prefer the GUID this frame observed on the last
        // taxi-map open. The agent does not currently expose the live
        // flight-master GUID directly; HelloFlightMasterAsync wires it server-
        // side and the open event carries it. Tests / external callers can
        // seed it via SetActiveFlightMaster below; live BG callers will set
        // it via the bot-side TakeFlightPathTask which already tracks the
        // flight-master GUID it called Hello on.
        if (_lastSelectedFlightMasterGuid == 0UL) return;

        agent.ActivateFlightAsync(_lastSelectedFlightMasterGuid, sourceNodeId, (uint)nodeId)
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Seeds the flight-master GUID used by <see cref="SelectNode(int)"/>.
    /// Called by <c>TakeFlightPathTask</c> immediately after it issues
    /// <see cref="IFlightMasterNetworkClientComponent.HelloFlightMasterAsync"/>
    /// so the activation packet targets the correct flight master. Not part
    /// of the <see cref="ITaxiFrame"/> contract -- BG-only.
    /// </summary>
    public void SetActiveFlightMaster(ulong flightMasterGuid)
        => _lastSelectedFlightMasterGuid = flightMasterGuid;

    private sealed class BgTaxiNode : TaxiNode
    {
        internal BgTaxiNode(string status, string name, int cost, int nodeNumber)
        {
            Status = status;
            Name = name;
            Cost = cost;
            NodeNumber = nodeNumber;
        }
    }
}
