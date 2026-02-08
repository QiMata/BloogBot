namespace GameData.Core.Interfaces
{
    /// <summary>
    /// Interface for activity snapshots used to communicate bot state.
    /// This interface allows GameData.Core to remain independent of BotCommLayer
    /// while still supporting snapshot functionality.
    /// </summary>
    public interface IWoWActivitySnapshot
    {
        /// <summary>
        /// Gets or sets the timestamp of the snapshot.
        /// </summary>
        uint Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the account name associated with this snapshot.
        /// </summary>
        string AccountName { get; set; }

        /// <summary>
        /// Gets or sets the character name of the logged-in player.
        /// This is set after the character has entered the game world.
        /// </summary>
        string CharacterName { get; set; }

        /// <summary>
        /// Gets or sets the current WoW screen state (e.g. "LoginScreen", "CharacterSelect", "InWorld").
        /// </summary>
        string ScreenState { get; set; }
    }
}
