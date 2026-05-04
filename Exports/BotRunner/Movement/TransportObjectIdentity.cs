using Pathfinding;

namespace BotRunner.Movement;

public static class TransportObjectIdentity
{
    private const ushort StaticTransportHighGuid = 0xF120;
    private const ushort MovingTransportHighGuid = 0x1FC0;

    public static uint EntryFromGuid(ulong guid)
    {
        var highType = (ushort)(guid >> 48);
        return highType switch
        {
            StaticTransportHighGuid => (uint)((guid >> 24) & 0x00FFFFFFUL),
            MovingTransportHighGuid => (uint)(guid & 0x00FFFFFFUL),
            _ => 0u,
        };
    }

    public static uint ResolveEntry(ulong guid, uint reportedEntry)
    {
        var guidEntry = EntryFromGuid(guid);
        return guidEntry != 0 ? guidEntry : reportedEntry;
    }

    public static uint ResolveEntry(DynamicObjectProto obj)
        => ResolveEntry(obj.Guid, obj.Entry);

    public static bool MatchesTransport(DynamicObjectProto obj, TransportData.TransportDefinition transport)
    {
        var resolvedEntry = ResolveEntry(obj);
        if (transport.GameObjectEntry != 0 && resolvedEntry != 0)
            return resolvedEntry == transport.GameObjectEntry
                && MatchesDisplay(obj.DisplayId, transport.DisplayId);

        if (transport.Type != TransportData.TransportType.Elevator && transport.GameObjectEntry != 0)
            return false;

        return MatchesDisplay(obj.DisplayId, transport.DisplayId);
    }

    public static TransportData.TransportDefinition? FindTransportByGuid(ulong guid)
    {
        var entry = EntryFromGuid(guid);
        return entry == 0 ? null : TransportData.FindByEntry(entry);
    }

    public static string Format(DynamicObjectProto? obj)
    {
        if (obj == null)
            return "none";

        var guidEntry = EntryFromGuid(obj.Guid);
        var resolvedEntry = ResolveEntry(obj);
        var entryText = guidEntry != 0 && obj.Entry != 0 && guidEntry != obj.Entry
            ? $"{resolvedEntry}/reported:{obj.Entry}"
            : resolvedEntry.ToString();

        return $"{entryText}:{obj.DisplayId}:guid=0x{obj.Guid:X}@({obj.X:F1},{obj.Y:F1},{obj.Z:F1})/o={obj.Orientation:F2}";
    }

    private static bool MatchesDisplay(uint actualDisplayId, uint expectedDisplayId)
        => expectedDisplayId == 0 || actualDisplayId == 0 || actualDisplayId == expectedDisplayId;
}
