namespace WoWStateManager.Clients
{
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Backward-compatible wrapper around <see cref="BotCommLayer.MangosSOAPClient"/>.
    /// All implementation now lives in BotCommLayer for shared access by UI and services.
    /// </summary>
    public class MangosSOAPClient : BotCommLayer.MangosSOAPClient
    {
        public MangosSOAPClient(string mangosUrl, ILogger<BotCommLayer.MangosSOAPClient> logger, string username = "ADMINISTRATOR", string password = "PASSWORD")
            : base(mangosUrl, logger, username, password)
        {
        }
    }
}
