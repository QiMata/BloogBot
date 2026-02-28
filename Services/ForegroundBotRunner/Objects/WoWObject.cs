using ForegroundBotRunner.Mem;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System.Runtime.InteropServices;
using Serilog;
using System;
using System.Collections.Generic;

namespace ForegroundBotRunner.Objects
{
    public unsafe abstract class WoWObject(nint pointer, HighGuid guid, WoWObjectType objectType) : IWoWObject
    {
        public nint Pointer { get; } = pointer;
        // Right-click interaction delegates for Vanilla 1.12.1.
        // Units and GameObjects use DIFFERENT native functions with DIFFERENT signatures.
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void RightClickUnitDelegate(nint objectPtr, int autoLoot);

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void RightClickGameObjDelegate(nint objectPtr);

        // 0x60BEA0 = CGUnit_C::OnRightClick(int autoLoot) — for units/NPCs
        private static readonly RightClickUnitDelegate rightClickUnitFunction =
            Marshal.GetDelegateForFunctionPointer<RightClickUnitDelegate>(0x60BEA0);

        // 0x5F8660 = CGGameObject_C::OnRightClick() — for game objects (no extra params)
        private static readonly RightClickGameObjDelegate rightClickGameObjectFunction =
            Marshal.GetDelegateForFunctionPointer<RightClickGameObjDelegate>(0x5F8660);

        public float ScaleX => MemoryManager.ReadFloat(nint.Add(GetDescriptorPtr(), MemoryAddresses.WoWObject_ScaleXOffset));
        public float Height => MemoryManager.ReadFloat(nint.Add(Pointer, MemoryAddresses.WoWObject_HeightOffset));
        public Position Position => GetPosition();

        private Position GetPosition()
        {
            try
            {
                if (ObjectType == WoWObjectType.Unit || ObjectType == WoWObjectType.Player)
                {
                    var x = MemoryManager.ReadFloat(nint.Add(Pointer, 0x9B8));
                    var y = MemoryManager.ReadFloat(nint.Add(Pointer, 0x9BC));
                    var z = MemoryManager.ReadFloat(nint.Add(Pointer, 0x9C0));

                    return new(x, y, z);
                }
                else
                {
                    float x;
                    float y;
                    float z;
                    if (MemoryManager.ReadInt(GetDescriptorPtr() + 0x54) == 3)
                    {
                        x = MemoryManager.ReadFloat(GetDescriptorPtr() + 0x3C);
                        y = MemoryManager.ReadFloat(GetDescriptorPtr() + (0x3C + 4));
                        z = MemoryManager.ReadFloat(GetDescriptorPtr() + (0x3C + 8));
                        return new(x, y, z);
                    }
                    var v2 = MemoryManager.ReadInt(nint.Add(Pointer, 0x210));
                    nint xyzStruct;
                    if (v2 != 0)
                    {
                        var underlyingFuncPtr = MemoryManager.ReadInt(nint.Add(MemoryManager.ReadIntPtr(v2), 0x44));
                        switch (underlyingFuncPtr)
                        {
                            case 0x005F5C10:
                                x = MemoryManager.ReadFloat(v2 + 0x2c);
                                y = MemoryManager.ReadFloat(v2 + 0x2c + 0x4);
                                z = MemoryManager.ReadFloat(v2 + 0x2c + 0x8);
                                return new(x, y, z);
                            case 0x005F3690:
                                v2 = (int)nint.Add(MemoryManager.ReadIntPtr(nint.Add(MemoryManager.ReadIntPtr(v2 + 0x4), 0x110)), 0x24);
                                x = MemoryManager.ReadFloat(v2);
                                y = MemoryManager.ReadFloat(v2 + 0x4);
                                z = MemoryManager.ReadFloat(v2 + 0x8);
                                return new Position(x, y, z);
                        }
                        xyzStruct = v2 + 0x44;
                    }
                    else
                    {
                        xyzStruct = nint.Add(MemoryManager.ReadIntPtr(nint.Add(Pointer, 0x110)), 0x24);
                    }
                    x = MemoryManager.ReadFloat(xyzStruct);
                    y = MemoryManager.ReadFloat(nint.Add(xyzStruct, 0x4));
                    z = MemoryManager.ReadFloat(nint.Add(xyzStruct, 0x8));
                    return new(x, y, z);
                }
            }
            catch (AccessViolationException)
            {
                Log.Error("Access violation on WoWObject.Position. Swallowing.");
            }
            catch (Exception e)
            {
                Log.Error($"[WOW OBJECT]{e.Message} {e.StackTrace}");
            }
            return new(0, 0, 0);
        }

        public float Facing
        {
            get
            {
                try
                {
                    if (ObjectType == WoWObjectType.Unit || ObjectType == WoWObjectType.Player)
                    {
                        float facing = MemoryManager.ReadFloat(Pointer + 0x9C4);

                        if (facing < 0)
                        {
                            facing = (float)(Math.PI * 2) + facing;
                        }
                        return facing;
                    }
                    else
                    {
                        return 0;
                    }
                }
                catch (AccessViolationException)
                {
                    Log.Error("Access violation on WoWObject.Facing. Swallowing.");
                    return 0;
                }
            }
        }

        public string Name
        {
            get
            {
                try
                {
                    if (ObjectType == WoWObjectType.Player)
                    {
                        var namePtr = MemoryManager.ReadIntPtr(0xC0E230);
                        // Walk the name cache linked list with safety limits
                        // The list can be empty or the entry might not exist yet (timing issue)
                        const int maxIterations = 1000;
                        for (int i = 0; i < maxIterations && namePtr != nint.Zero; i++)
                        {
                            var nextGuid = MemoryManager.ReadUlong(nint.Add(namePtr, 0xC));

                            if (nextGuid == Guid)
                            {
                                // Found the entry - read the name string at offset 0x14
                                return MemoryManager.ReadString(nint.Add(namePtr, 0x14));
                            }

                            // Move to next entry in the linked list
                            namePtr = MemoryManager.ReadIntPtr(namePtr);
                        }
                        // Name not found in cache yet (timing issue) or cache exhausted
                        return string.Empty;
                    }
                    else if (ObjectType == WoWObjectType.Unit)
                    {
                        var ptr1 = MemoryManager.ReadInt(nint.Add(Pointer, 0xB30));
                        var ptr2 = MemoryManager.ReadInt(ptr1);
                        return MemoryManager.ReadString(ptr2);
                    }
                    else if (ObjectType == WoWObjectType.GameObj)
                    {
                        var ptr1 = MemoryManager.ReadIntPtr(nint.Add(Pointer, 0x214));
                        var ptr2 = MemoryManager.ReadIntPtr(nint.Add(ptr1, 0x8));
                        return MemoryManager.ReadString(ptr2);
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                catch (AccessViolationException)
                {
                    Log.Error("Access violation on WoWObject.Name. Swallowing.");
                    return "";
                }
                catch (Exception e)
                {
                    Log.Error($"Catchall exception {e.Message} {e.StackTrace}");
                    return "";
                }
            }
        }

        // Use constructor parameters via primary constructor syntax
        public ulong Guid => guid.FullGuid;

        public HighGuid HighGuid => guid;

        public WoWObjectType ObjectType => objectType;

        public uint LastUpated => 0;

        public uint Entry => (uint)MemoryManager.ReadInt(nint.Add(GetDescriptorPtr(), 0xC));

        public bool InWorld => true;

        public uint LastUpdated => 0;

        public ulong TransportGuid => 0;

        public Position TransportOffset => new(0, 0, 0);

        public float SwimPitch => 0f;

        public float JumpVerticalSpeed => 0f;

        public float JumpSinAngle => 0f;

        public float JumpCosAngle => 0f;

        public float JumpHorizontalSpeed => 0f;

        public float SplineElevation => 0f;

        public float TransportOrientation => 0f;

        public uint TransportLastUpdated => 0;

        public SplineFlags SplineFlags { get; set; }
        public Position SplineFinalPoint { get; set; } = new(0, 0, 0);
        public ulong SplineTargetGuid { get; set; }
        public float SplineFinalOrientation { get; set; }
        public int SplineTimePassed { get; set; }
        public int SplineDuration { get; set; }
        public uint SplineId { get; set; }
        public List<Position> SplineNodes { get; set; } = [];
        public Position SplineFinalDestination { get; set; } = new(0, 0, 0);

        public void Interact()
        {
            if (ObjectType == WoWObjectType.GameObj)
                rightClickGameObjectFunction(Pointer);  // CGGameObject_C::OnRightClick — no extra params
            else
                rightClickUnitFunction(Pointer, 0);     // CGUnit_C::OnRightClick(int autoLoot)
        }

        public nint GetDescriptorPtr() => MemoryManager.ReadIntPtr(nint.Add(Pointer, MemoryAddresses.WoWObject_DescriptorOffset));
    }
}
