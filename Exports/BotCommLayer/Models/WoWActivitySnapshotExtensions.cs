using GameData.Core.Interfaces;

namespace Communication
{
    /// <summary>
    /// Partial class extension to make the Protobuf-generated WoWActivitySnapshot
    /// implement IWoWActivitySnapshot from GameData.Core.
    /// This enables dependency inversion where GameData.Core defines the contract
    /// and BotCommLayer provides the implementation.
    /// </summary>
    public partial class WoWActivitySnapshot : IWoWActivitySnapshot
    {
        // The Protobuf-generated WoWActivitySnapshot already has Timestamp and AccountName properties,
        // so no additional implementation is needed here.
        // This partial class simply adds the interface implementation.
    }
}
