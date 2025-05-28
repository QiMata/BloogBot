using BloogBot.AI.States;
using Communication;
using GameData.Core.Interfaces;

namespace BloogBot.AI;

public static class HeuristicPicker
{
    public static BotActivity PickNext(IObjectManager obj)
    {
        // Pre-login and character setup
        if (!obj.LoginScreen.IsLoggedIn)
            return BotActivity.LoggingIn;
        if (obj.RealmSelectScreen.CurrentRealm == null)
            return BotActivity.RealmSelecting;
        if (
            obj.CharacterSelectScreen.HasReceivedCharacterList
            && !obj.CharacterSelectScreen.HasEnteredWorld
        )
            return BotActivity.CharacterSelecting;

        // In-world heuristics using snapshot
        var player = obj.Player;
        var healthPercent = player.Health / (float)player.MaxHealth;
        if (healthPercent < 0.3f)
            return BotActivity.Resting;
        if (player.IsInCombat)
            return BotActivity.Combat;

        // Fallback idle
        return BotActivity.Resting;
    }
}
