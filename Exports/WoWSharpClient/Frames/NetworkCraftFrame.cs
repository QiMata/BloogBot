using GameData.Core.Frames;
using System;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Frames;

/// <summary>
/// Minimal BG craft-frame surface backed by <see cref="IProfessionsNetworkClientComponent"/>.
/// Routes <see cref="ICraftFrame"/> operations through the BG packet path
/// (CMSG_CAST_SPELL via the agent's <c>CraftItemAsync</c>) so
/// InteractionSequenceBuilder's "Craft" sequence stops short-circuiting with
/// "CraftFrame is null -- requires FG bot or packet-based path" on BG bots.
/// Closes S1.16.
///
/// <para>
/// <strong>Slot semantics.</strong> <see cref="ICraftFrame.Craft(int)"/> and
/// <see cref="ICraftFrame.HasMaterialsNeeded(int)"/> take a <c>slot</c> that is
/// the 1-based UI index Lua's <c>DoCraft(craftIndex)</c> consumes on FG. The
/// BG <see cref="IProfessionsNetworkClientComponent"/> deals in recipe
/// <em>spell IDs</em>, not slot indices, and does not currently expose a public
/// slot-keyed known-recipes list. We therefore treat the frame's <c>slot</c>
/// argument as a direct recipe spell ID and let the server be the cost /
/// reagent / "is this recipe known" authority — mirrors the
/// <see cref="NetworkTrainerFrame"/> "let server arbitrate" approach.
/// </para>
///
/// <para>
/// <strong>HasMaterialsNeeded.</strong> The agent's
/// <see cref="IProfessionsNetworkClientComponent.GetRecipeMaterials"/> currently
/// returns <see cref="Array.Empty{T}()"/> (stub). A truthy default keeps the
/// dispatcher's "Can Craft Item" gate non-blocking so the cast packet itself
/// produces the authoritative failure (SMSG_CAST_FAILED with
/// SPELL_FAILED_REAGENTS) if reagents are missing. When the agent grows
/// real recipe-material tracking this becomes a strict per-reagent check.
/// </para>
/// </summary>
public sealed class NetworkCraftFrame(Func<IProfessionsNetworkClientComponent?> resolveProfessionsAgent) : ICraftFrame
{
    public bool IsOpen => resolveProfessionsAgent()?.IsCraftingWindowOpen == true;

    public bool HasMaterialsNeeded(int slot)
    {
        var agent = resolveProfessionsAgent();
        if (agent == null) return false;
        // See class doc — slot is treated as a recipe spell ID. GetRecipeMaterials
        // returns Array.Empty<RecipeMaterial>() today, so an "all materials present"
        // result is the correct semantic ("nothing reported missing"). The
        // server-side CMSG_CAST_SPELL will return SPELL_FAILED_REAGENTS if the
        // reagents are actually missing.
        uint recipeId = (uint)Math.Max(0, slot);
        var materials = agent.GetRecipeMaterials(recipeId);
        if (materials == null || materials.Length == 0) return true;
        foreach (var mat in materials)
        {
            if (!mat.HasSufficientQuantity) return false;
        }
        return true;
    }

    public void Craft(int slot)
    {
        var agent = resolveProfessionsAgent();
        if (agent == null) return;
        // slot is treated as a recipe spell ID (see class doc). Quantity 1 mirrors
        // FG's `DoCraft(craftIndex)` single-press semantic — InteractionSequenceBuilder
        // dispatches bulk crafting through the 2-param Craft action that routes
        // through CastSpell directly, not through this frame.
        uint recipeId = (uint)Math.Max(0, slot);
        agent.CraftItemAsync(recipeId).GetAwaiter().GetResult();
    }
}
