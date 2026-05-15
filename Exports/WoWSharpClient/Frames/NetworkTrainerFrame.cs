using GameData.Core.Frames;
using System;
using System.Collections.Generic;
using System.Linq;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Frames;

/// <summary>
/// Minimal BG trainer-frame surface backed by <see cref="ITrainerNetworkClientComponent"/>.
/// Routes <see cref="ITrainerFrame"/> operations through the BG packet path so
/// InteractionSequenceBuilder's "Train Skill" sequence stops short-circuiting
/// with "TrainerFrame is null -- requires FG bot or packet-based path" on
/// BG bots. Closes the Trainer half of S1.19.
///
/// <para>
/// Spells uses default-constructed <see cref="TrainerSpellItem"/> placeholders
/// (no public constructor on that contract type for setting fields). The
/// dispatcher's "Has Enough Gold" gate compares <c>Player.Copper</c> against
/// <c>TrainerSpellItem.Cost</c> — default 0 — so the gate proceeds and the
/// authoritative cost check happens server-side via CMSG_TRAINER_BUY_SPELL.
/// </para>
/// </summary>
public sealed class NetworkTrainerFrame(Func<ITrainerNetworkClientComponent?> resolveTrainerAgent) : ITrainerFrame
{
    public bool IsOpen => resolveTrainerAgent()?.IsTrainerWindowOpen == true;

    public void Close()
    {
        var trainer = resolveTrainerAgent();
        if (trainer?.IsTrainerWindowOpen != true) return;
        trainer.CloseTrainerAsync().GetAwaiter().GetResult();
    }

    public IEnumerable<TrainerSpellItem> Spells
    {
        get
        {
            var services = resolveTrainerAgent()?.GetAvailableServices();
            if (services == null || services.Length == 0)
                return Array.Empty<TrainerSpellItem>();
            // Placeholder TrainerSpellItem instances sized to the real service
            // count so dispatcher's `Spells.ElementAt(spellIndex)` returns a
            // valid object. Real cost/canLearn arbitration lives in the agent
            // (GetSpellCost, IsSpellAvailable) — wire those into a richer frame
            // when TrainerSpellItem grows a public constructor.
            return Enumerable.Range(0, services.Length).Select(_ => new TrainerSpellItem());
        }
    }

    public void TrainSpell(int spellIndex)
    {
        var trainer = resolveTrainerAgent();
        var trainerGuid = trainer?.CurrentTrainerGuid;
        if (trainer == null || trainerGuid == null || trainerGuid == 0UL) return;
        trainer.LearnSpellByIndexAsync(trainerGuid.Value, (uint)Math.Max(0, spellIndex))
            .GetAwaiter().GetResult();
    }

    public void Update()
    {
        // Refresh authority state (the agent's observables already drive the
        // cached service list). Explicit Update() is a no-op on BG; the
        // server's TRAINER_LIST response keeps the agent in sync automatically.
    }
}
