namespace GameData.Core.Interfaces
{
    /// <summary>
    /// Interface for activity snapshots used to communicate bot state.
    /// This interface allows GameData.Core to remain independent of BotCommLayer
    /// while still supporting snapshot functionality.
    /// </summary>
    public interface IActivitySnapshot
    {
        /// <summary>
        /// Gets or sets the timestamp of the snapshot.
        /// </summary>
        uint Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the account name associated with this snapshot.
        /// </summary>
        string AccountName { get; set; }
    }
}
