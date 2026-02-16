namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Action button type from SMSG_ACTION_BUTTONS.
    /// Encoded in the top 8 bits of each uint32 entry.
    /// </summary>
    public enum ActionButtonType : byte
    {
        Spell = 0x00,
        Click = 0x01,
        Macro = 0x40,
        ClickMacro = 0x41,
        Item = 0x80
    }

    /// <summary>
    /// A single action bar button entry parsed from SMSG_ACTION_BUTTONS.
    /// </summary>
    public readonly struct ActionButton(uint packedData)
    {
        /// <summary>The raw packed uint32 value from the server.</summary>
        public uint PackedData { get; } = packedData;

        /// <summary>The action ID (spell ID, item ID, or macro ID). Bottom 24 bits.</summary>
        public uint ActionId => PackedData & 0x00FFFFFF;

        /// <summary>The action type. Top 8 bits.</summary>
        public ActionButtonType Type => (ActionButtonType)((PackedData >> 24) & 0xFF);

        /// <summary>Whether this button slot is empty (no action assigned).</summary>
        public bool IsEmpty => PackedData == 0;

        public override string ToString() =>
            IsEmpty ? "Empty" : $"{Type}:{ActionId}";
    }

    /// <summary>
    /// Proficiency data from SMSG_SET_PROFICIENCY.
    /// </summary>
    public readonly struct ProficiencyData(byte itemClass, uint subclassMask)
    {
        /// <summary>Item class (0=Consumable, 2=Weapon, 4=Armor, etc.)</summary>
        public byte ItemClass { get; } = itemClass;

        /// <summary>Bitmask of allowed subclasses for this item class.</summary>
        public uint SubclassMask { get; } = subclassMask;
    }

    /// <summary>
    /// Hearthstone bind point from SMSG_BINDPOINTUPDATE.
    /// </summary>
    public readonly struct BindPointData(float x, float y, float z, uint mapId, uint areaId)
    {
        public float X { get; } = x;
        public float Y { get; } = y;
        public float Z { get; } = z;
        public uint MapId { get; } = mapId;
        public uint AreaId { get; } = areaId;
    }

    /// <summary>
    /// A single faction entry from SMSG_INITIALIZE_FACTIONS.
    /// </summary>
    public readonly struct FactionEntry(byte flags, int standing)
    {
        public byte Flags { get; } = flags;
        public int Standing { get; } = standing;

        /// <summary>Whether this faction is visible in the reputation panel.</summary>
        public bool IsVisible => (Flags & 0x01) != 0;

        /// <summary>Whether the player is at war with this faction.</summary>
        public bool IsAtWar => (Flags & 0x02) != 0;
    }
}
