using GameData.Core.Enums;
using GameData.Core.Frames;
using System;
using System.Collections.Generic;
using System.Linq;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Frames;

/// <summary>
/// Minimal BG gossip-frame surface backed by <see cref="IGossipNetworkClientComponent"/>.
/// Routes <see cref="IGossipFrame"/> operations through the BG packet path so
/// InteractionSequenceBuilder's "Select Gossip" sequence stops short-circuiting
/// with "GossipFrame is null -- requires FG bot or packet-based path" on
/// BG bots. Closes the Gossip half of S1.19.
///
/// <para>
/// <c>Options</c> uses a private <see cref="GossipOption"/> subclass to bridge
/// the abstract contract type — the public abstract <see cref="GossipOption"/>
/// has no constructor exposed for setting <c>Type</c>/<c>Text</c>, so a
/// concrete subclass within the BG frame is the only non-reflection path. The
/// dispatcher uses <c>Options.Count &gt; 0</c> for window-readiness gating, so
/// returning placeholders sized to the live menu's option count satisfies the
/// gate without lying about the underlying server state.
/// </para>
/// </summary>
public sealed class NetworkGossipFrame(Func<IGossipNetworkClientComponent?> resolveGossipAgent) : IGossipFrame
{
    public bool IsOpen => resolveGossipAgent()?.IsGossipWindowOpen == true;

    public void Close()
    {
        var gossip = resolveGossipAgent();
        if (gossip?.IsGossipWindowOpen != true) return;
        gossip.CloseGossipAsync().GetAwaiter().GetResult();
    }

    public ulong NPCGuid => resolveGossipAgent()?.CurrentNpcGuid ?? 0UL;

    public void SelectGossipOption(int parOptionIndex)
    {
        var gossip = resolveGossipAgent();
        if (gossip == null) return;
        gossip.SelectGossipOptionAsync((uint)Math.Max(0, parOptionIndex))
            .GetAwaiter().GetResult();
    }

    public void SelectFirstGossipOfType(DialogType type)
    {
        var gossip = resolveGossipAgent();
        if (gossip == null) return;
        var menu = gossip.GetCurrentGossipMenu();
        if (menu == null) return;
        var match = menu.Options
            .Select((opt, idx) => (opt, idx))
            .FirstOrDefault(t => t.opt.GossipType == MapTypeEnum(type));
        if (match.opt == null) return;
        gossip.SelectGossipOptionAsync((uint)match.idx).GetAwaiter().GetResult();
    }

    public List<GossipOption> Options
    {
        get
        {
            var menu = resolveGossipAgent()?.GetCurrentGossipMenu();
            if (menu == null || menu.Options.Count == 0) return new List<GossipOption>();
            return menu.Options
                .Select(opt => (GossipOption)new BgGossipOption(opt.GossipType, opt.Text ?? string.Empty))
                .ToList();
        }
    }

    public List<QuestOption> Quests => new();

    private static GossipTypes MapTypeEnum(DialogType type)
    {
        // DialogType uses lowercase enum names (gossip/vendor/taxi/...) while
        // GossipTypes uses PascalCase. Both share the same numeric layout
        // (gossip=0, vendor=1, ..., auctioneer=10), so a direct numeric cast is
        // safe and avoids the case-sensitivity gap a string Parse would have.
        return (GossipTypes)(int)type;
    }

    private sealed class BgGossipOption : GossipOption
    {
        public BgGossipOption(GossipTypes type, string text)
        {
            Type = type;
            Text = text;
        }
    }
}
