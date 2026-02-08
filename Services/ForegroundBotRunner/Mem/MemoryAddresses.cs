namespace ForegroundBotRunner.Mem
{
    public static class MemoryAddresses
    {
        // Functions
        public static int EnumerateVisibleObjectsFunPtr = 0x00468380;
        public static int GetObjectPtrFunPtr = 0x00464870;
        public static int GetPlayerGuidFunPtr = 0x00468550;
        public static int SetFacingFunPtr = 0x007C6F30;
        public static int SendMovementUpdateFunPtr = 0x00600A30;
        public static int SetControlBitFunPtr = 0x00515090;
        public static int SetControlBitDevicePtr = 0x00BE1148;
        public static int GetCreatureTypeFunPtr = 0x00605570;
        public static int GetCreatureRankFunPtr = 0x00605620;
        public static int GetUnitReactionFunPtr = 0x006061E0;
        public static int LuaCallFunPtr = 0x00704CD0;
        public static int GetTextFunPtr = 0x00703BF0;
        public static int IntersectFunPtr = 0x00672170; // https://www.ownedcore.com/forums/world-of-warcraft/world-of-warcraft-bots-programs/wow-memory-editing/409609-fixed-cworld-intersect-raycasting-1-12-a.html
        public static int SetTargetFunPtr = 0x00493540;
        public static int RetrieveCorpseFunPtr = 0x0048D260;
        public static int ReleaseCorpseFunPtr = 0x005E0AE0;
        public static int GetItemCacheEntryFunPtr = 0x0055BA30;
        public static int ItemCacheEntryBasePtr = 0x00C0E2A0;
        public static int IsSpellOnCooldownFunPtr = 0x006E13E0;
        public static int LootSlotFunPtr = 0x004C2790;
        public static int UseItemFunPtr = 0x005D8D00;
        public static int SellItemByGuidFunPtr = 0x005E1D50;
        public static int BuyVendorItemFunPtr = 0x005E1E90;
        public static int CastAtPositionFunPtr = 0x006E60F0;

        // Statics
        public static int ZoneTextPtr = 0x00B4B404;
        public static int MinimapZoneTextPtr = 0x00B4DA28;
        public static int MapId;
        public static int ServerName;
        public static int LocalPlayerCorpsePositionX = 0x00B4E284;
        public static int LocalPlayerCorpsePositionY = 0x00B4E288;
        public static int LocalPlayerCorpsePositionZ = 0x00B4E28C;
        public static int LocalPlayerFirstExtraBag = 0x00BDD060;
        public static int LocalPlayerClass = 0x00C27E81;
        public static int LastHardwareAction = 0x00CF0BC8;
        public static int LocalPlayerSpellsBase = 0x00B700F0;
        public static int SignalEventFunPtr = 0x00703F76;
        public static int SignalEventNoParamsFunPtr = 0x00703E72;
        public static int WardenLoadHook = 0x006CA22E;
        public static int WardenBase = 0x00CE8978;
        public static int WardenPageScanOffset = 0x00002B21;
        public static int WardenMemScanOffset = 0x00002A7F;
        public static int PartyLeaderGuid = 0x00BC75F8;
        public static int Party1Guid = 0x00BC6F48;
        public static int Party2Guid = 0x00BC6F50;
        public static int Party3Guid = 0x00BC6F58;
        public static int Party4Guid = 0x00BC6F60;

        // Frames
        public static int CoinCountPtr = 0x00B71BA0;
        public static int DialogFrameBase;
        public static int MerchantFrameItemsBasePtr = 0x00BDDFA8;
        public static int MerchantFrameItemPtr = 0x00BDD11C;
        public static int LootFrameItemsBasePtr = 0x00B7196C;
        public static int LootFrameItemOffset = 0x1C;
        public static int MerchantFrameItemOffset = 0x1C;

        // Descriptors
        public static int LocalPlayer_BackpackFirstItemOffset = 0x850;
        public static int WoWItem_ItemIdOffset = 0xC;
        public static int WoWItem_StackCountOffset = 0x38;
        public static int WoWItem_DurabilityOffset = 0xB8;
        public static int WoWItem_ContainerFirstItemOffset = 0xC0;
        public static int WoWPet_SpellsBase;
        public static int WoWUnit_SummonedByGuidOffset = 0x30;
        public static int WoWUnit_TargetGuidOffset = 0x40;
        public static int WoWUnit_HealthOffset = 0x58;
        public static int WoWUnit_ManaOffset = 0x5C;
        public static int WoWUnit_RageOffset = 0x60;
        public static int WoWUnit_EnergyOffset = 0x68;
        public static int WoWUnit_MaxHealthOffset = 0x70;
        public static int WoWUnit_MaxManaOffset = 0x74;
        public static int WoWUnit_LevelOffset = 0x88;
        public static int WoWUnit_FactionIdOffset = 0x8C;
        public static int WoWUnit_UnitFlagsOffset = 0xB8;
        public static int WoWUnit_BoundingRadiusOffset = 0x204;
        public static int WoWUnit_CombatReachOffset = 0x208;
        public static int WoWUnit_DynamicFlagsOffset = 0x23C;
        public static int WoWUnit_CurrentChannelingOffset = 0x240;

        // Offsets
        public static int LocalPlayer_SetFacingOffset = 0x9A8;
        public static int LocalPlayer_EquipmentFirstItemOffset = 0x2508;
        public static int WoWObject_DescriptorOffset = 0x8;
        public static int WoWObject_ScaleXOffset = 0x10;
        public static int WoWObject_GetPositionFunOffset;
        public static int WoWObject_GetFacingFunOffset;
        public static int WoWObject_InteractFunOffset;
        public static int WoWObject_GetNameFunOffset;
        public static int WoWObject_HeightOffset = 0xA5C;
        public static int WoWUnit_BuffsBaseOffset = 0xBC;
        public static int WoWUnit_DebuffsBaseOffset = 0x13C;
        public static int WoWUnit_MovementFlagsOffset = 0x9E8;
        public static int WoWUnit_CurrentSpellcastOffset = 0xC8C;

        // Movement data offsets (from player object base)
        // CMovementInfo base = 0x9A8 (confirmed: SetFacingOffset)
        // Reference: MEM_ADDRESSES.md, cMaNGOS MovementInfo struct
        //
        // Confirmed positions (from WoWObject.cs, verified with recordings):
        //   Position X = Pointer + 0x9B8 (base + 0x10)
        //   Position Y = Pointer + 0x9BC (base + 0x14)
        //   Position Z = Pointer + 0x9C0 (base + 0x18)
        //   Facing     = Pointer + 0x9C4 (base + 0x1C)

        // Swim pitch (CMovementInfo base + 0x20)
        // In network packets, swim pitch is sent when MOVEFLAG_SWIMMING is set.
        // In client memory it's always present at a fixed offset.
        public static int WoWUnit_SwimPitchOffset = 0x9C8;       // base + 0x20: camera pitch while swimming

        // Transport data (CMovementInfo base + 0x28..0x3F)
        // CONFIRMED via zeppelin recording (Orgrimmar-Undercity, Entry 164871):
        //   - TransportGuid at base+0x38 (0x9E0): WORKS — reads correct MoTransport GUID
        //   - Offsets base+0x28..0x34 (0x9D0..0x9DC): NOT transport local coords!
        //     They read sin/cos pair + garbage constant even when standing on ground.
        //   - When TransportGuid != 0, the MAIN Position fields (0x9B8-0x9C0) auto-switch
        //     from world coordinates to transport-local coordinates.
        //   - MOVEFLAG_ONTRANSPORT (0x200) is NEVER SET in vanilla 1.12.1.
        //   - Boarding transition: single frame, flagged with 0x04000000 (TELEPORT_TO_PLANE).
        public static int WoWUnit_Unknown0x9D0 = 0x9D0;            // base + 0x28: NOT transport X — reads sin/cos
        public static int WoWUnit_Unknown0x9D4 = 0x9D4;            // base + 0x2C: NOT transport Y — reads sin/cos
        public static int WoWUnit_Unknown0x9D8 = 0x9D8;            // base + 0x30: NOT transport Z — reads garbage
        public static int WoWUnit_Unknown0x9DC = 0x9DC;            // base + 0x34: NOT transport orientation
        public static int WoWUnit_TransportGuidOffset = 0x9E0;     // base + 0x38: transport GUID (uint64) — CONFIRMED

        // MoveFlags at base + 0x40 = 0x9E8 (confirmed)

        // Jump/fall data (CMovementInfo)
        // Jump angles from MEM_ADDRESSES.md. Values vary continuously (camera-related)
        // when not jumping; only meaningful when MOVEFLAG_JUMPING is set.
        // TODO: Verify with jump test (Task 4)
        public static int WoWUnit_JumpSinAngleOffset = 0xA14;    // base + 0x6C: sin of jump direction
        public static int WoWUnit_JumpCosAngleOffset = 0xA18;    // base + 0x70: cos of jump direction
        public static int WoWUnit_JumpVelocityOffset = 0xA50;    // base + 0xA8: initial jump vertical velocity

        // Fall data (confirmed with recordings)
        public static int WoWUnit_FallStartTimeOffset = 0xA20;   // base + 0x78: tick count when fall began (GetTickCount clock)
        public static int WoWUnit_FallStartHeightOffset = 0xA28; // base + 0x80: Z coordinate at start of fall

        // Speed values (CMovementInfo base + 0x84..0x9C, confirmed with recordings)
        public static int WoWUnit_CurrentSpeedOffset = 0xA2C;    // base + 0x84: active movement speed
        public static int WoWUnit_WalkSpeedOffset = 0xA30;       // base + 0x88: walk speed (default 2.5 yards/sec)
        public static int WoWUnit_RunSpeedOffset = 0xA34;        // base + 0x8C: forward run speed (default 7.0 yards/sec)
        public static int WoWUnit_RunBackSpeedOffset = 0xA38;    // base + 0x90: backward run speed (default 4.5 yards/sec)
        public static int WoWUnit_SwimSpeedOffset = 0xA3C;       // base + 0x94: forward swim speed (default 4.722 yards/sec)
        public static int WoWUnit_SwimBackSpeedOffset = 0xA40;   // base + 0x98: backward swim speed (default 2.5 yards/sec)
        public static int WoWUnit_TurnRateOffset = 0xA44;        // base + 0x9C: turn speed (default π radians/sec)

        // MoveSpline pointer (from unit base, CMovementInfo + 0xA4)
        // CONFIRMED via diff-based scan (3 flights): NULL when standing, heap pointer during flight path.
        // Located right after CMovementInfo speed fields (TurnRate at base+0x9C).
        // Old offset 0xD8 was a linked list node, NOT MoveSpline.
        public static int WoWUnit_MoveSplinePtrOffset = 0xA4C;

        // MoveSpline internal offsets — CONFIRMED via client memory dumps (Orgrimmar↔Crossroads flights)
        // NOTE: Client layout DIFFERS from MaNGOS server C++ struct.
        //
        // Confirmed fields:
        //   +0x00: uint32 nodeCount          (68 for Crossroads↔Orgrimmar)
        //   +0x18: uint32 splineFlags        (0x300 = CATMULLROM|FLYING for flight paths)
        //   +0x20: int32  time_passed        (ms elapsed since spline start — CONFIRMED: 16→133547 over full XR→OG flight)
        //   +0x24: uint32 duration           (total spline duration in ms — CONFIRMED: flight ends when time_passed ≈ this)
        //   +0x28: uint32 unknown_28         (constant during flight, value ~1.1M — NOT duration, purpose unknown)
        //   +0x34: uint32 pointIdx           (constant 26 during XR→OG — NOT current interpolation index, purpose unclear)
        //   +0x38: uint32 pointIdx2          (same value as +0x34)
        //   +0x3C: ptr    pointsDataPtr      (pointer to Vector3[] node array — CONFIRMED valid world coords)
        //   +0x4C: ptr    lengthsDataPtr     (pointer to float[] cumulative arc lengths)
        //   +0x58: Vector3 destination       (endpoint position, e.g. Crossroads coords for OG→XR flight)
        public static int MoveSpline_NodeCountOffset = 0x00;
        public static int MoveSpline_FlagsOffset = 0x18;
        public static int MoveSpline_TimePassedOffset = 0x20;
        public static int MoveSpline_DurationOffset = 0x24;
        public static int MoveSpline_Unknown28Offset = 0x28;
        public static int MoveSpline_PointIdxOffset = 0x34;
        public static int MoveSpline_PointsDataPtrOffset = 0x3C;
        public static int MoveSpline_LengthsDataPtrOffset = 0x4C;
        public static int MoveSpline_DestinationOffset = 0x58;

        // Static addresses for physics values
        public static int FallingSpeedPtr = 0x0087D894;          // Current vertical fall velocity (float, static address)

        public static int WoWItem_ContainerSlotsOffset = 0x6c8;

        public static int WoWUnit_MountDisplayIdOffset = 0x214;
        public static int WoWUnit_NPCFlagsOffset = 0x24C;

        // Game object descriptor offsets (from descriptor pointer)
        // Vanilla 1.12.1 layout: OBJECT_END = 0x6 (6 uint32 fields = 0x18 bytes)
        // Confirmed via existing code: GO type check at +0x54, GO position at +0x3C
        public static int WoWGameObject_CreatedByOffset = 0x18;   // OBJECT_END + 0x0, uint64 (2 fields)
        public static int WoWGameObject_DisplayIdOffset = 0x20;   // OBJECT_END + 0x2
        public static int WoWGameObject_FlagsOffset = 0x24;       // OBJECT_END + 0x3
        public static int WoWGameObject_RotationOffset = 0x28;    // OBJECT_END + 0x4, 4 floats (16 bytes)
        public static int WoWGameObject_StateOffset = 0x38;       // OBJECT_END + 0x8 (GOState enum)
        public static int WoWGameObject_DynFlagsOffset = 0x4C;    // OBJECT_END + 0xD
        public static int WoWGameObject_FactionOffset = 0x50;     // OBJECT_END + 0xE
        public static int WoWGameObject_TypeIdOffset = 0x54;      // OBJECT_END + 0xF (confirmed in WoWObject.cs)
        public static int WoWGameObject_LevelOffset = 0x58;       // OBJECT_END + 0x10
        public static int WoWGameObject_ArtKitOffset = 0x5C;      // OBJECT_END + 0x11
        public static int WoWGameObject_AnimProgressOffset = 0x60; // OBJECT_END + 0x12
    }
}
