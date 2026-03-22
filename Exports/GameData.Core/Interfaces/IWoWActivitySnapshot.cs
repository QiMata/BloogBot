namespace GameData.Core.Interfaces
{
    /// <summary>
    /// WoW-specific activity snapshot. Extends <see cref="IActivitySnapshot"/> with
    /// character identity and screen state fields available after login.
    ///
    /// Hierarchy:
    ///   <see cref="IActivitySnapshot"/> — base contract (Timestamp, AccountName).
    ///   <see cref="IWoWActivitySnapshot"/> — adds CharacterName, ScreenState.
    ///   BotCommLayer's <c>WoWActivitySnapshot</c> (protobuf-generated) implements this via partial class.
    /// </summary>
    public interface IWoWActivitySnapshot : IActivitySnapshot
    {
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
