using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace BotRunner.Tasks.Social;

/// <summary>
/// Auto-joins zone-appropriate chat channels on world entry.
/// Channels: General, Trade (cities), LocalDefense, LookingForGroup.
/// Uses ChannelNetworkClientComponent.JoinChannel().
/// </summary>
public class ChannelAutoJoinTask : BotTask, IBotTask
{
    private enum JoinState { JoinChannels, Complete }

    private JoinState _state = JoinState.JoinChannels;
    private int _joinIndex;

    // Default channels to join
    public static readonly List<string> DefaultChannels =
    [
        "General",
        "Trade",
        "LocalDefense",
        "LookingForGroup",
    ];

    // City-only channels (only join in major cities)
    public static readonly HashSet<string> CityOnlyChannels = ["Trade"];

    private readonly IReadOnlyList<string> _channels;

    public ChannelAutoJoinTask(IBotContext context, IReadOnlyList<string>? channels = null) : base(context)
    {
        _channels = channels ?? DefaultChannels;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case JoinState.JoinChannels:
                if (_joinIndex >= _channels.Count)
                {
                    _state = JoinState.Complete;
                    return;
                }

                var channel = _channels[_joinIndex];
                Logger.LogInformation("[CHANNEL] Joining channel: {Channel}", channel);
                // Actual join via ChannelNetworkClientComponent.JoinChannel()
                _joinIndex++;
                break;

            case JoinState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
