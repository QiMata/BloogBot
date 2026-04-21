using GameData.Core.Enums;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using WoWSharpClient.Models;

namespace WoWSharpClient
{
    /// <summary>
    /// Battleground area-trigger support for the background protocol client.
    /// The 1.12.1 client emits CMSG_AREATRIGGER on capture-zone entry; BG must do the same.
    /// Current scope is the two known Warsong Gulch flag-capture points backed by live
    /// read-only DB evidence from mangos.areatrigger_template.
    /// </summary>
    public partial class WoWSharpObjectManager
    {
        private readonly object _knownBattlegroundAreaTriggerStateLock = new();
        private readonly Dictionary<uint, bool> _knownBattlegroundAreaTriggerInside = [];
        private uint _knownBattlegroundAreaTriggerMapId = uint.MaxValue;

        private static readonly KnownBattlegroundAreaTrigger[] KnownBattlegroundAreaTriggers =
        [
            // Read-only DB evidence:
            // SELECT * FROM mangos.areatrigger_template WHERE id=3646;
            // -> Warsong Gulch, Silverwing Hold - Alliance Flag Capture Point
            new(3646u, 489u, "Warsong Gulch Alliance Flag Capture Point",
                1539.89f, 1481.36f, 352.659f, 5.0f,
                0.3333f, 0.3333f, 0.3333f, 0f),
            // Read-only DB evidence:
            // SELECT * FROM mangos.areatrigger_template WHERE id=3647;
            // -> Warsong Gulch, Warsong Lumber Mill - Horde Flag Capture Point
            new(3647u, 489u, "Warsong Gulch Horde Flag Capture Point",
                918.496f, 1434.04f, 346.054f, 5.0f,
                0.3333f, 0.3333f, 0.3333f, 0f),
            // Read-only DB evidence:
            // SELECT id,name,map_id,x,y,z,radius FROM mangos.areatrigger_template WHERE id BETWEEN 3806 AND 3815;
            // -> objective-centered Arathi Basin trigger volumes (client emits CMSG_AREATRIGGER on entry).
            new(3806u, 529u, "Arathi Basin Stables Trigger A",
                1182.28f, 1183.18f, -45.2521f, 30.0f,
                0f, 0f, 0f, 0f),
            new(3807u, 529u, "Arathi Basin Stables Trigger B",
                1181.62f, 1183.39f, -45.3294f, 30.0f,
                0f, 0f, 0f, 0f),
            new(3808u, 529u, "Arathi Basin Blacksmith Trigger A",
                997.144f, 1001.16f, -31.4366f, 35.0f,
                0f, 0f, 0f, 0f),
            new(3809u, 529u, "Arathi Basin Blacksmith Trigger B",
                997.092f, 1001.47f, -31.3356f, 35.0f,
                0f, 0f, 0f, 0f),
            new(3810u, 529u, "Arathi Basin Farm Trigger A",
                819.995f, 813.522f, -57.6458f, 35.0f,
                0f, 0f, 0f, 0f),
            new(3811u, 529u, "Arathi Basin Farm Trigger B",
                820.006f, 813.831f, -57.6672f, 35.0f,
                0f, 0f, 0f, 0f),
            new(3812u, 529u, "Arathi Basin Lumber Mill Trigger A",
                819.327f, 1178.43f, 36.3847f, 50.0f,
                0f, 0f, 0f, 0f),
            new(3813u, 529u, "Arathi Basin Lumber Mill Trigger B",
                819.39f, 1178.5f, 36.3851f, 50.0f,
                0f, 0f, 0f, 0f),
            new(3814u, 529u, "Arathi Basin Gold Mine Trigger A",
                1174.0f, 830.033f, -106.574f, 40.0f,
                0f, 0f, 0f, 0f),
            new(3815u, 529u, "Arathi Basin Gold Mine Trigger B",
                1174.14f, 830.089f, -106.553f, 40.0f,
                0f, 0f, 0f, 0f),
        ];

        internal void ResetKnownBattlegroundAreaTriggerState()
        {
            lock (_knownBattlegroundAreaTriggerStateLock)
            {
                _knownBattlegroundAreaTriggerInside.Clear();
                _knownBattlegroundAreaTriggerMapId = uint.MaxValue;
            }
        }

        internal void PollKnownBattlegroundAreaTriggersForLocalPlayer()
        {
            var worldClient = _woWClient?.WorldClient;
            var player = Player as WoWLocalPlayer;
            if (worldClient == null || player == null || HasPendingWorldEntry || _isBeingTeleported)
            {
                return;
            }

            List<KnownBattlegroundAreaTrigger>? enteredTriggers = null;
            lock (_knownBattlegroundAreaTriggerStateLock)
            {
                if (_knownBattlegroundAreaTriggerMapId != player.MapId)
                {
                    _knownBattlegroundAreaTriggerInside.Clear();
                    _knownBattlegroundAreaTriggerMapId = player.MapId;
                }

                for (int i = 0; i < KnownBattlegroundAreaTriggers.Length; i++)
                {
                    var trigger = KnownBattlegroundAreaTriggers[i];
                    if (trigger.MapId != player.MapId)
                    {
                        continue;
                    }

                    bool isInside = trigger.Contains(player.Position);
                    bool wasInside = _knownBattlegroundAreaTriggerInside.TryGetValue(trigger.TriggerId, out bool value) && value;

                    if (isInside && !wasInside)
                    {
                        _knownBattlegroundAreaTriggerInside[trigger.TriggerId] = true;
                        enteredTriggers ??= [];
                        enteredTriggers.Add(trigger);
                    }
                    else if (!isInside && wasInside)
                    {
                        _knownBattlegroundAreaTriggerInside[trigger.TriggerId] = false;
                    }
                }
            }

            if (enteredTriggers == null)
            {
                return;
            }

            foreach (var trigger in enteredTriggers)
            {
                _ = _woWClient.SendAreaTriggerAsync(trigger.TriggerId);
                Log.Information(
                    "[AreaTrigger] Sent CMSG_AREATRIGGER id={TriggerId} name='{Name}' map={MapId} pos=({X:F1},{Y:F1},{Z:F1})",
                    trigger.TriggerId,
                    trigger.Name,
                    player.MapId,
                    player.Position.X,
                    player.Position.Y,
                    player.Position.Z);
            }
        }

        private readonly record struct KnownBattlegroundAreaTrigger(
            uint TriggerId,
            uint MapId,
            string Name,
            float X,
            float Y,
            float Z,
            float Radius,
            float BoxX,
            float BoxY,
            float BoxZ,
            float BoxOrientation)
        {
            public bool Contains(Position position)
            {
                if (Radius > 0f)
                {
                    float dx = position.X - X;
                    float dy = position.Y - Y;
                    float dz = position.Z - Z;
                    return (dx * dx) + (dy * dy) + (dz * dz) <= Radius * Radius;
                }

                if (BoxX <= 0f || BoxY <= 0f || BoxZ <= 0f)
                {
                    return false;
                }

                float translatedX = position.X - X;
                float translatedY = position.Y - Y;
                float cos = MathF.Cos(-BoxOrientation);
                float sin = MathF.Sin(-BoxOrientation);
                float localX = translatedX * cos - translatedY * sin;
                float localY = translatedX * sin + translatedY * cos;
                float localZ = position.Z - Z;
                return MathF.Abs(localX) <= BoxX
                    && MathF.Abs(localY) <= BoxY
                    && MathF.Abs(localZ) <= BoxZ;
            }
        }
    }
}
