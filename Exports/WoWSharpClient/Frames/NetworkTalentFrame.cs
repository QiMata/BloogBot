using GameData.Core.Frames;
using System;
using System.Collections.Generic;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Frames;

/// <summary>
/// Minimal BG talent-frame surface backed by <see cref="ITalentNetworkClientComponent"/>.
/// Routes <see cref="ITalentFrame"/> operations through the BG packet path so
/// InteractionSequenceBuilder's "Train Talent" sequence stops short-circuiting
/// with "TalentFrame is null -- requires FG bot or packet-based path" on
/// BG bots. Closes the Talent half of S1.19.
///
/// <para>
/// <c>Tabs</c> returns empty until <see cref="TalentTab"/> grows a public
/// constructor (it currently has only get-only auto-properties without any
/// way to seed values). Dispatcher only consumes <c>TalentPointsAvailable</c>
/// and <c>LearnTalent</c>, both of which route through the agent.
/// </para>
/// </summary>
public sealed class NetworkTalentFrame(Func<ITalentNetworkClientComponent?> resolveTalentAgent) : ITalentFrame
{
    public bool IsOpen
    {
        get
        {
            // ITalentNetworkClientComponent does not expose an "is open" flag —
            // the talent window in vanilla 1.12.1 is a client-side overlay and
            // talents can be learned without an explicit server-side "open"
            // gesture. Treat the frame as always-open when the agent is wired so
            // the dispatcher's surrounding gates (TalentPointsAvailable > 0)
            // remain the load-bearing check.
            return resolveTalentAgent() != null;
        }
    }

    public void Close()
    {
        var talent = resolveTalentAgent();
        if (talent == null) return;
        talent.CloseTalentWindowAsync().GetAwaiter().GetResult();
    }

    public IEnumerable<TalentTab> Tabs => Array.Empty<TalentTab>();

    public int TalentPointsAll
    {
        get
        {
            var talent = resolveTalentAgent();
            if (talent == null) return 0;
            return (int)(talent.AvailableTalentPoints + talent.TotalTalentPointsSpent);
        }
    }

    public int TalentPointsAvailable => (int)(resolveTalentAgent()?.AvailableTalentPoints ?? 0);

    public int TalentPointsSpent => (int)(resolveTalentAgent()?.TotalTalentPointsSpent ?? 0);

    public void LearnTalent(int spellId)
    {
        var talent = resolveTalentAgent();
        if (talent == null) return;
        talent.LearnTalentAsync((uint)Math.Max(0, spellId)).GetAwaiter().GetResult();
    }
}
