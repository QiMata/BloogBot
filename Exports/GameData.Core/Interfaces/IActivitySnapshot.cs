namespace GameData.Core.Interfaces
{
    /// <summary>
    /// Base activity snapshot contract. Game-agnostic fields shared by all snapshot types.
    /// Extended by <see cref="IWoWActivitySnapshot"/> for WoW-specific identity and screen state.
    /// </summary>
    public interface IActivitySnapshot
    {
        /// <summary>Unix timestamp (seconds) when the snapshot was captured.</summary>
        uint Timestamp { get; set; }

        /// <summary>Account name (e.g. "TESTBOT1") that owns this snapshot.</summary>
        string AccountName { get; set; }
    }
}
