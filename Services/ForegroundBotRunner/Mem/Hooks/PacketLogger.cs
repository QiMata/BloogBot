using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Serilog;

// ReSharper disable InconsistentNaming

namespace ForegroundBotRunner.Mem.Hooks
{
    /// <summary>
    /// Hooks WoW's NetClient::Send to capture outbound CMSG opcodes.
    ///
    /// Architecture follows SignalEventManager pattern:
    ///   - Assembly injection detours at known function addresses
    ///   - Managed delegates called from detour code
    ///   - Deferred initialization (after world entry)
    ///   - SEH-safe (managed try/catch + AccessViolation handling)
    ///
    /// 1.12.1 NetClient::Send signature:
    ///   void __thiscall NetClient::Send(CDataStore* packet)
    ///   Address: 0x005379A0
    ///   CDataStore layout: +0x04 = buffer ptr, +0x10 = size
    ///   First 4 bytes of buffer = opcode (uint32, low 16 bits)
    ///
    /// Receive: Inferred from state changes and SignalEvent hooks.
    /// When the exact ProcessMessage dispatch address is confirmed via
    /// disassembly, a direct receive hook can be added using the same pattern.
    /// </summary>
    public static class PacketLogger
    {
        // CDataStore offsets (1.12.1)
        private const int CDataStore_Buffer = 0x04;
        private const int CDataStore_Size = 0x10;

        // Circular buffer of recent packets
        private const int MaxPacketEntries = 1024;
        private static readonly PacketEntry[] _recentPackets = new PacketEntry[MaxPacketEntries];
        private static int _packetIndex;
        private static readonly object _packetLock = new();

        // Event for state machine consumption
        public static event Action<PacketDirection, ushort, int>? OnPacketCaptured;

        // State tracking
        private static volatile bool _hooksInitialized;
        private static volatile bool _sendHookInstalled;

        // Diagnostic logging
        private static readonly string DiagnosticLogPath;
        private static readonly object DiagnosticLogLock = new();
        private static int _sendCount;
        private static int _recvCount;

        // Keep delegate reference alive (prevent GC collection)
        private static SendHookDelegate? _sendHookDelegate;

        // Delegate for the detour callback — receives CDataStore pointer
        private delegate void SendHookDelegate(nint dataStorePtr);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern nint GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern nint GetProcAddress(nint hModule, string procName);

        // Store the original bytes for cleanup
        private static byte[]? _originalSendBytes;
        private static nint _sendHookAddress;

        static PacketLogger()
        {
            string wowDir;
            try
            {
                wowDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
                    ?? AppContext.BaseDirectory;
            }
            catch { wowDir = AppContext.BaseDirectory; }

            var logsDir = Path.Combine(wowDir, "WWoWLogs");
            try { Directory.CreateDirectory(logsDir); } catch { }
            DiagnosticLogPath = Path.Combine(logsDir, "packet_logger.log");
            try
            {
                File.WriteAllText(DiagnosticLogPath,
                    $"=== PacketLogger Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
            }
            catch { }
        }

        /// <summary>
        /// Initialize packet capture hooks. Must be called AFTER world entry.
        /// </summary>
        public static void InitializeHooks()
        {
            if (_hooksInitialized) return;
            DiagLog("InitializeHooks STARTING");

            try { InstallSendHook(); }
            catch (Exception ex) { DiagLog($"InstallSendHook FAILED: {ex.Message}\n{ex.StackTrace}"); }

            _hooksInitialized = true;
            DiagLog($"InitializeHooks COMPLETED (send={_sendHookInstalled})");
        }

        /// <summary>Whether the send hook is active.</summary>
        public static bool IsActive => _sendHookInstalled;

        /// <summary>Total outbound packets captured.</summary>
        public static int SendCount => _sendCount;

        /// <summary>Total inbound packets recorded.</summary>
        public static int RecvCount => _recvCount;

        /// <summary>Returns recent packet entries (newest first), thread-safe.</summary>
        public static PacketEntry[] GetRecentPackets(int count = 50)
        {
            lock (_packetLock)
            {
                count = Math.Min(count, MaxPacketEntries);
                var result = new PacketEntry[count];
                for (int i = 0; i < count; i++)
                {
                    int idx = (_packetIndex - 1 - i + MaxPacketEntries) % MaxPacketEntries;
                    result[i] = _recentPackets[idx];
                }
                return result;
            }
        }

        // ===================================================================
        // SEND HOOK — Detour NetClient::Send (0x005379A0)
        // ===================================================================
        //
        // NetClient::Send is __thiscall:
        //   ECX = this (NetClient ptr)
        //   [esp+0x04] = CDataStore* packet
        //
        // Strategy: We read the first 5 bytes of the function at runtime,
        // save them, then overwrite with a JMP to our detour code cave.
        // The code cave:
        //   1. pushfd / pushad  (save all state)
        //   2. Read CDataStore* from stack (adjusted for our pushes)
        //   3. Call managed delegate
        //   4. popad / popfd  (restore state)
        //   5. Execute the original 5 bytes (copied as raw machine code)
        //   6. JMP back to NetClientSend+5
        //
        // We write the original bytes as raw DB directives into the code cave.

        private static void InstallSendHook()
        {
            _sendHookDelegate = new SendHookDelegate(OnSendPacket);
            var managedAddr = (uint)(nint)Marshal.GetFunctionPointerForDelegate(_sendHookDelegate);
            uint sendAddr = (uint)(nint)Offsets.Functions.NetClientSend;
            _sendHookAddress = (nint)sendAddr;

            // Read original first 5 bytes
            _originalSendBytes = MemoryManager.ReadBytes((nint)sendAddr, 5);
            if (_originalSendBytes == null || _originalSendBytes.Length < 5)
            {
                DiagLog($"InstallSendHook: Failed to read original bytes at 0x{sendAddr:X8}");
                return;
            }
            DiagLog($"InstallSendHook: Original bytes at 0x{sendAddr:X8}: {BitConverter.ToString(_originalSendBytes)}");

            // Build the detour code cave using raw byte emission.
            // We can't use FASM to emit the original bytes directly, so we build
            // the code cave in two parts:
            //   Part 1: pushfd/pushad, read stack arg, call managed, popad/popfd (via FASM)
            //   Part 2: original bytes + jmp back (raw bytes appended)

            // Part 1: FASM-assembled pre-call and post-call wrapper
            // After pushfd (4 bytes) + pushad (32 bytes) = 36 bytes pushed onto stack
            // Original stack at entry had: [esp+0] = ret addr, [esp+4] = CDataStore*
            // After our pushes: CDataStore* is at [esp + 36 + 4] = [esp + 0x28]
            var asmInstructions = new[]
            {
                "pushfd",
                "pushad",
                "mov eax, [esp+0x28]",    // CDataStore* (first __thiscall stack arg)
                "push eax",               // arg for our cdecl callback
                $"call 0x{managedAddr:X}", // call OnSendPacket(dataStorePtr)
                "popad",
                "popfd"
                // Original bytes + jmp back will be appended as raw bytes
            };

            // Allocate the detour
            var detourAddr = MemoryManager.InjectAssembly("PacketSendDetour", asmInstructions);

            // Now we need to append the original 5 bytes + a JMP back.
            // Calculate where the FASM code ends: we need to find the length.
            // InjectAssembly returns the start address. The FASM code is compiled
            // to a fixed location. We'll write the original bytes after it.
            //
            // However, InjectAssembly allocates exactly the FASM output size.
            // We need a different approach: allocate a larger buffer manually.

            // Let's use a different strategy: build everything including the
            // original instruction bytes as DB directives in FASM.
            // FASM supports "db 0x55, 0x8B, ..." for raw byte emission.

            var dbLine = "db " + string.Join(",", Array.ConvertAll(_originalSendBytes, b => $"0x{b:X2}"));
            uint returnAddr = sendAddr + 5;

            // Get SafeCallback1 from FastCall.dll for SEH protection.
            // .NET 8 can't catch AccessViolationException — only C++ __try/__except can.
            var fastCallHandle = GetModuleHandle("FastCall.dll");
            var safeCallback1Addr = fastCallHandle != nint.Zero
                ? GetProcAddress(fastCallHandle, "SafeCallback1")
                : nint.Zero;

            string[] fullInstructions;
            if (safeCallback1Addr != nint.Zero)
            {
                DiagLog($"SafeCallback1 found at 0x{(uint)safeCallback1Addr:X8} — using SEH-protected send hook");
                fullInstructions =
                [
                    "pushfd",
                    "pushad",
                    "mov eax, [esp+0x28]",
                    "push eax",                              // arg1: CDataStore*
                    $"push 0x{managedAddr:X}",               // parCallbackPtr (managed delegate)
                    $"call 0x{(uint)safeCallback1Addr:X}",   // SafeCallback1 — SEH-protected
                    "popad",
                    "popfd",
                    dbLine,
                    $"jmp 0x{returnAddr:X}"
                ];
            }
            else
            {
                DiagLog("WARNING: SafeCallback1 NOT found — falling back to unprotected send hook");
                fullInstructions =
                [
                    "pushfd",
                    "pushad",
                    "mov eax, [esp+0x28]",
                    "push eax",
                    $"call 0x{managedAddr:X}",
                    "popad",
                    "popfd",
                    dbLine,
                    $"jmp 0x{returnAddr:X}"
                ];
            }

            // Re-allocate with full instructions (the first allocation was wasted, but
            // HackManager tracks it and it'll be small — acceptable leak)
            var fullDetourAddr = MemoryManager.InjectAssembly("PacketSendDetourFull", fullInstructions);

            // Install the hook: overwrite first 5 bytes of NetClientSend with JMP to detour
            MemoryManager.InjectAssembly("PacketSendHook", sendAddr, $"jmp 0x{(uint)fullDetourAddr:X}");

            // Verify
            var hookBytes = MemoryManager.ReadBytes((nint)sendAddr, 5);
            if (hookBytes != null && hookBytes[0] == 0xE9)
            {
                _sendHookInstalled = true;
                DiagLog($"PacketSend hook INSTALLED at 0x{sendAddr:X8} → detour 0x{(uint)fullDetourAddr:X8}");
            }
            else
            {
                DiagLog($"PacketSend hook FAILED at 0x{sendAddr:X8}: byte[0]=0x{(hookBytes?[0] ?? 0):X2}");
            }
        }

        [HandleProcessCorruptedStateExceptions]
        private static void OnSendPacket(nint dataStorePtr)
        {
            try
            {
                if (dataStorePtr == nint.Zero) return;

                nint bufferPtr = MemoryManager.ReadIntPtr(dataStorePtr + CDataStore_Buffer);
                int size = MemoryManager.ReadInt(dataStorePtr + CDataStore_Size);
                if (bufferPtr == nint.Zero || size < 4) return;

                uint rawOpcode = MemoryManager.ReadUint(bufferPtr);
                ushort opcode = (ushort)(rawOpcode & 0xFFFF);

                _sendCount++;
                RecordPacket(PacketDirection.Send, opcode, size);
                OnPacketCaptured?.Invoke(PacketDirection.Send, opcode, size);
            }
            catch (AccessViolationException) { /* stale pointer — skip */ }
            catch (Exception ex) { DiagLog($"OnSendPacket error: {ex.Message}"); }
        }

        // ===================================================================
        // RECV — Manual recording for inbound packets
        // ===================================================================

        /// <summary>
        /// Record an inbound server packet. Called by observers that detect
        /// server-to-client packets (state changes, ContinentId transitions, etc.).
        /// </summary>
        public static void RecordInboundPacket(ushort opcode, int size = 0)
        {
            _recvCount++;
            RecordPacket(PacketDirection.Recv, opcode, size);
            OnPacketCaptured?.Invoke(PacketDirection.Recv, opcode, size);
        }

        // ===================================================================
        // Packet recording + diagnostics
        // ===================================================================

        private static void RecordPacket(PacketDirection direction, ushort opcode, int size)
        {
            var entry = new PacketEntry
            {
                Timestamp = DateTime.UtcNow,
                Direction = direction,
                Opcode = opcode,
                Size = size
            };

            lock (_packetLock)
            {
                _recentPackets[_packetIndex] = entry;
                _packetIndex = (_packetIndex + 1) % MaxPacketEntries;
            }

            // Log first 200 packets and all map-transition-related opcodes
            int total = _sendCount + _recvCount;
            bool isMapTransition = opcode is
                0x003E   // SMSG_NEW_WORLD
                or 0x003F // SMSG_TRANSFER_PENDING
                or 0x0040 // SMSG_TRANSFER_ABORT
                or 0x00DC // MSG_MOVE_WORLDPORT_ACK
                or 0x0236; // SMSG_LOGIN_VERIFY_WORLD
            bool isAuth = opcode is
                0x01EE   // SMSG_AUTH_RESPONSE
                or 0x003B; // SMSG_CHAR_ENUM

            if (total <= 200 || isMapTransition || isAuth)
            {
                var dir = direction == PacketDirection.Send ? "TX" : "RX";
                DiagLog($"[{dir}] opcode=0x{opcode:X4} size={size} (#{total})");
            }
        }

        private static void DiagLog(string message)
        {
            try
            {
                lock (DiagnosticLogLock)
                {
                    File.AppendAllText(DiagnosticLogPath,
                        $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                }
            }
            catch { }
        }
    }

    /// <summary>Packet direction.</summary>
    public enum PacketDirection : byte
    {
        Send = 0,
        Recv = 1
    }

    /// <summary>Captured packet entry (opcode + metadata, no payload).</summary>
    public struct PacketEntry
    {
        public DateTime Timestamp;
        public PacketDirection Direction;
        public ushort Opcode;
        public int Size;

        public override readonly string ToString()
        {
            var dir = Direction == PacketDirection.Send ? "TX" : "RX";
            return $"[{Timestamp:HH:mm:ss.fff}] [{dir}] 0x{Opcode:X4} size={Size}";
        }
    }
}
