using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Serilog;

// ReSharper disable InconsistentNaming

namespace ForegroundBotRunner.Mem.Hooks
{
    /// <summary>
    /// Hooks WoW's NetClient::Send and NetClient::ProcessMessage to capture
    /// both outbound CMSG and inbound SMSG packets.
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
    /// 1.12.1 NetClient::ProcessMessage signature:
    ///   void __thiscall NetClient::ProcessMessage(int tickCount, CDataStore* dataStore)
    ///   Address: found via runtime pattern scan (m_handlers at this+0x74)
    ///   CDataStore buffer starts with 16-bit opcode (SMSG)
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
        private static volatile bool _recvHookInstalled;

        // Diagnostic logging
        private static readonly string DiagnosticLogPath;
        private static readonly object DiagnosticLogLock = new();
        private static int _sendCount;
        private static int _recvCount;
        private static int _recvSuspiciousCount;

        // Keep delegate references alive (prevent GC collection)
        private static SendHookDelegate? _sendHookDelegate;
        private static RecvHookDelegate? _recvHookDelegate;

        // Delegate for the detour callbacks — receives CDataStore pointer
        private delegate void SendHookDelegate(nint dataStorePtr);
        private delegate void RecvHookDelegate(nint dataStorePtr);

        // Store the original bytes for cleanup
        private static byte[]? _originalSendBytes;
        private static nint _sendHookAddress;
        private static byte[]? _originalRecvBytes;
        private static nint _recvHookAddress;

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

            try { InstallRecvHook(); }
            catch (Exception ex) { DiagLog($"InstallRecvHook FAILED: {ex.Message}\n{ex.StackTrace}"); }

            _hooksInitialized = true;
            DiagLog($"InitializeHooks COMPLETED (send={_sendHookInstalled}, recv={_recvHookInstalled})");
        }

        /// <summary>Whether any packet hook is active.</summary>
        public static bool IsActive => _sendHookInstalled || _recvHookInstalled;

        /// <summary>Whether the recv hook is active (direct SMSG capture).</summary>
        public static bool IsRecvActive => _recvHookInstalled;

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

            // Read original first 6 bytes (must end on instruction boundary).
            // NetClient::Send prologue: push ebp(1) + mov ebp,esp(2) + push esi(1) + mov esi,ecx(2) = 6 bytes.
            // Reading only 5 splits the last instruction — the code cave's db would consume the JMP opcode.
            _originalSendBytes = MemoryManager.ReadBytes((nint)sendAddr, 6);
            if (_originalSendBytes == null || _originalSendBytes.Length < 6)
            {
                DiagLog($"InstallSendHook: Failed to read original bytes at 0x{sendAddr:X8}");
                return;
            }
            DiagLog($"InstallSendHook: Original bytes at 0x{sendAddr:X8}: {BitConverter.ToString(_originalSendBytes)}");

            // Build the detour code cave with raw byte emission for the original prologue.
            // After pushfd (4 bytes) + pushad (32 bytes) = 36 bytes pushed onto stack.
            // Original stack: [esp+0] = ret addr, [esp+4] = CDataStore* (__thiscall).
            // After pushes: CDataStore* is at [esp + 36 + 4] = [esp + 0x28].
            var dbLine = "db " + string.Join(",", Array.ConvertAll(_originalSendBytes, b => $"0x{b:X2}"));
            uint returnAddr = sendAddr + 6; // +6 to land on the next complete instruction

            // Get SafeCallback1 from FastCall.dll for SEH protection.
            // .NET 8 can't catch AccessViolationException — only C++ __try/__except can.
            var safeCallback1Addr = NativeLibraryHelper.GetFastCallExport("_SafeCallback1@8", "SafeCallback1");

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

            var fullDetourAddr = MemoryManager.InjectAssembly("PacketSendDetour", fullInstructions);

            // Install the hook: overwrite first 6 bytes of NetClientSend with JMP + NOP.
            // The original prologue is 6 bytes; we replicate all 6 in the code cave.
            MemoryManager.InjectAssembly("PacketSendHook", sendAddr, $"jmp 0x{(uint)fullDetourAddr:X}\nnop");

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
        // RECV HOOK — Detour NetClient::ProcessMessage (pattern-scanned)
        // ===================================================================
        //
        // NetClient::ProcessMessage is __thiscall:
        //   ECX = this (NetClient ptr)
        //   [esp+0x04] = tickCount (int)
        //   [esp+0x08] = CDataStore* dataStore
        //
        // The CDataStore buffer starts with a 16-bit SMSG opcode.
        // ProcessMessage looks up m_handlers[opcode] at this+0x74 and dispatches.
        //
        // We find ProcessMessage at runtime by scanning for __thiscall functions
        // near NetClient::Send that access offset 0x74 (the m_handlers array).

        private static void InstallRecvHook()
        {
            var processMessageAddr = FindProcessMessageByPattern();
            if (processMessageAddr == nint.Zero)
            {
                DiagLog("InstallRecvHook: ProcessMessage address not found — recv hook NOT installed");
                DiagLog("InstallRecvHook: Falling back to ContinentId-based inbound inference");
                return;
            }

            _recvHookDelegate = new RecvHookDelegate(OnRecvPacket);
            var managedAddr = (uint)(nint)Marshal.GetFunctionPointerForDelegate(_recvHookDelegate);
            uint recvAddr = (uint)processMessageAddr;

            // Read original prologue bytes (6 bytes — typical __thiscall prologue).
            _originalRecvBytes = MemoryManager.ReadBytes(processMessageAddr, 6);
            if (_originalRecvBytes == null || _originalRecvBytes.Length < 6)
            {
                DiagLog($"InstallRecvHook: Failed to read original bytes at 0x{recvAddr:X8}");
                return;
            }
            DiagLog($"InstallRecvHook: Original bytes at 0x{recvAddr:X8}: {BitConverter.ToString(_originalRecvBytes)}");

            // Verify prologue starts with push ebp (0x55) — sanity check
            if (_originalRecvBytes[0] != 0x55)
            {
                DiagLog($"InstallRecvHook: Unexpected prologue byte 0x{_originalRecvBytes[0]:X2} (expected 0x55 push ebp) — aborting");
                return;
            }

            var dbLine = "db " + string.Join(",", Array.ConvertAll(_originalRecvBytes, b => $"0x{b:X2}"));
            uint returnAddr = recvAddr + 6;

            // ProcessMessage has 2 params after this: tickCount + CDataStore*.
            // After pushfd(4) + pushad(32) = 36 bytes pushed.
            // Original: [esp+0] = ret, [esp+4] = tickCount, [esp+8] = CDataStore*
            // After pushes: CDataStore* at [esp + 36 + 8] = [esp + 0x2C]
            var safeCallback1Addr = NativeLibraryHelper.GetFastCallExport("_SafeCallback1@8", "SafeCallback1");

            string[] fullInstructions;
            if (safeCallback1Addr != nint.Zero)
            {
                DiagLog($"SafeCallback1 at 0x{(uint)safeCallback1Addr:X8} — using SEH-protected recv hook");
                fullInstructions =
                [
                    "pushfd",
                    "pushad",
                    "mov eax, [esp+0x2C]",                   // CDataStore* (2nd param after this)
                    "push eax",                               // arg1: CDataStore*
                    $"push 0x{managedAddr:X}",                // parCallbackPtr (managed delegate)
                    $"call 0x{(uint)safeCallback1Addr:X}",    // SafeCallback1 — SEH-protected
                    "popad",
                    "popfd",
                    dbLine,
                    $"jmp 0x{returnAddr:X}"
                ];
            }
            else
            {
                DiagLog("WARNING: SafeCallback1 NOT found — falling back to unprotected recv hook");
                fullInstructions =
                [
                    "pushfd",
                    "pushad",
                    "mov eax, [esp+0x2C]",
                    "push eax",
                    $"call 0x{managedAddr:X}",
                    "popad",
                    "popfd",
                    dbLine,
                    $"jmp 0x{returnAddr:X}"
                ];
            }

            var fullDetourAddr = MemoryManager.InjectAssembly("PacketRecvDetour", fullInstructions);

            // Install: overwrite first 6 bytes with JMP + NOP (same as send hook)
            MemoryManager.InjectAssembly("PacketRecvHook", recvAddr, $"jmp 0x{(uint)fullDetourAddr:X}\nnop");

            // Verify
            var hookBytes = MemoryManager.ReadBytes(processMessageAddr, 5);
            if (hookBytes != null && hookBytes[0] == 0xE9)
            {
                _recvHookInstalled = true;
                _recvHookAddress = processMessageAddr;
                DiagLog($"PacketRecv hook INSTALLED at 0x{recvAddr:X8} → detour 0x{(uint)fullDetourAddr:X8}");
            }
            else
            {
                DiagLog($"PacketRecv hook FAILED at 0x{recvAddr:X8}: byte[0]=0x{(hookBytes?[0] ?? 0):X2}");
            }
        }

        [HandleProcessCorruptedStateExceptions]
        private static void OnRecvPacket(nint dataStorePtr)
        {
            try
            {
                if (dataStorePtr == nint.Zero) return;

                nint bufferPtr = MemoryManager.ReadIntPtr(dataStorePtr + CDataStore_Buffer);
                int size = MemoryManager.ReadInt(dataStorePtr + CDataStore_Size);
                if (bufferPtr == nint.Zero || size < 2) return;

                // SMSG: first 2 bytes of CDataStore buffer = opcode (uint16 LE)
                uint rawOpcode = MemoryManager.ReadUint(bufferPtr);
                ushort opcode = (ushort)(rawOpcode & 0xFFFF);

                // Sanity check: WoW 1.12.1 has ~828 opcodes (max ~0x033B).
                // Opcodes > 0x0500 likely mean we hooked the wrong function.
                if (opcode > 0x0500)
                {
                    _recvSuspiciousCount++;
                    if (_recvSuspiciousCount <= 5)
                        DiagLog($"OnRecvPacket WARNING: suspicious opcode 0x{opcode:X4} (size={size}) — possible wrong hook target");
                    if (_recvSuspiciousCount == 5)
                        DiagLog($"OnRecvPacket: suppressing further suspicious opcode warnings");
                    return; // Don't record suspicious packets
                }

                _recvCount++;
                RecordPacket(PacketDirection.Recv, opcode, size);
                OnPacketCaptured?.Invoke(PacketDirection.Recv, opcode, size);
            }
            catch (AccessViolationException) { /* stale pointer — skip */ }
            catch (Exception ex) { DiagLog($"OnRecvPacket error: {ex.Message}"); }
        }

        // ===================================================================
        // Pattern scanner — find NetClient::ProcessMessage at runtime
        // ===================================================================

        /// <summary>
        /// Scans the WoW code section near NetClient::Send for the ProcessMessage
        /// function. ProcessMessage is identified by accessing m_handlers at
        /// offset +0x74 from the this pointer, which is a unique signature for
        /// the opcode dispatch function.
        /// </summary>
        private static nint FindProcessMessageByPattern()
        {
            uint sendAddr = (uint)(nint)Offsets.Functions.NetClientSend;

            // Scan a region around NetClientSend — same class methods are adjacent
            uint scanStart = sendAddr - 0x2000;  // 8KB before Send
            uint scanEnd = sendAddr + 0x2000;    // 8KB after Send
            int scanSize = (int)(scanEnd - scanStart);

            DiagLog($"Pattern scan: 0x{scanStart:X8}–0x{scanEnd:X8} ({scanSize} bytes) [Send=0x{sendAddr:X8}]");

            var code = MemoryManager.ReadBytes((nint)scanStart, scanSize);
            if (code == null || code.Length < scanSize)
            {
                DiagLog("Pattern scan: failed to read code region");
                return nint.Zero;
            }

            var candidates = new List<(uint addr, int score)>();

            for (int i = 0; i < code.Length - 120; i++)
            {
                // __thiscall prologue: push ebp (55); mov ebp,esp (8B EC)
                if (code[i] != 0x55 || code[i + 1] != 0x8B || code[i + 2] != 0xEC)
                    continue;

                uint funcAddr = scanStart + (uint)i;
                if (funcAddr == sendAddr) continue;

                // Score the candidate based on ProcessMessage-like patterns
                int score = 0;
                bool has0x74Access = false;

                for (int j = i + 3; j < Math.Min(i + 80, code.Length - 3); j++)
                {
                    // Pattern: mov reg, [reg+0x74] — access to m_handlers
                    // ModR/M: mod=01 (8-bit disp), any reg, displacement = 0x74
                    if (code[j] == 0x8B)
                    {
                        byte modrm = code[j + 1];
                        byte mod = (byte)((modrm >> 6) & 3);
                        byte rm = (byte)(modrm & 7);
                        if (mod == 1 && rm != 4 && code[j + 2] == 0x74)
                        {
                            has0x74Access = true;
                            score += 10;
                        }
                    }

                    // Pattern: movzx reg, word ptr [reg+offset] (0F B7) — 16-bit opcode read
                    if (j < code.Length - 2 && code[j] == 0x0F && code[j + 1] == 0xB7)
                        score += 5;

                    // Pattern: call [reg+reg*4+offset] or indirect call — handler dispatch
                    if (code[j] == 0xFF)
                        score += 2;
                }

                if (has0x74Access)
                {
                    candidates.Add((funcAddr, score));
                    var bytes = BitConverter.ToString(code, i, Math.Min(12, code.Length - i));
                    DiagLog($"  Candidate: 0x{funcAddr:X8} score={score} bytes={bytes}");
                }
            }

            if (candidates.Count == 0)
            {
                DiagLog("Pattern scan: NO candidates found with 0x74 access");
                return nint.Zero;
            }

            if (candidates.Count == 1)
            {
                DiagLog($"Pattern scan: unique match at 0x{candidates[0].addr:X8} (score={candidates[0].score})");
                return (nint)candidates[0].addr;
            }

            // Multiple candidates — pick the highest-scoring one
            var best = candidates.OrderByDescending(c => c.score).First();
            DiagLog($"Pattern scan: {candidates.Count} candidates, selected 0x{best.addr:X8} (score={best.score})");
            return (nint)best.addr;
        }

        // ===================================================================
        // RECV — Manual recording fallback for inbound packets
        // ===================================================================

        /// <summary>
        /// Record an inbound server packet. Called by observers that detect
        /// server-to-client packets (state changes, ContinentId transitions, etc.).
        /// When the recv hook is active, most SMSG packets are captured directly;
        /// this method remains as a fallback for inferred packets.
        /// </summary>
        public static void RecordInboundPacket(ushort opcode, int size = 0)
        {
            // Skip manual recording if recv hook is capturing directly
            if (_recvHookInstalled) return;

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

            // Log first 500 packets + all important opcodes (auth, map transition, combat, spell)
            int total = _sendCount + _recvCount;
            bool isImportant = opcode is
                0x003B   // SMSG_CHAR_ENUM
                or 0x003E // SMSG_NEW_WORLD
                or 0x003F // SMSG_TRANSFER_PENDING
                or 0x0040 // SMSG_TRANSFER_ABORT
                or 0x00A9 // SMSG_UPDATE_OBJECT
                or 0x00DC // MSG_MOVE_WORLDPORT_ACK
                or 0x012E // SMSG_SPELL_START
                or 0x0130 // SMSG_SPELL_GO
                or 0x0131 // SMSG_SPELL_FAILURE
                or 0x013E // SMSG_CHANNEL_START
                or 0x0160 // SMSG_CHANNEL_UPDATE
                or 0x01EE // SMSG_AUTH_RESPONSE
                or 0x0236; // SMSG_LOGIN_VERIFY_WORLD

            if (total <= 500 || isImportant)
            {
                var dir = direction == PacketDirection.Send ? "C→S" : "S→C";
                var name = GetOpcodeName(opcode);
                DiagLog($"[{dir}] 0x{opcode:X4} {name} size={size} (#{total})");
            }
        }

        /// <summary>
        /// Returns a human-readable name for common WoW 1.12.1 opcodes.
        /// Only includes opcodes relevant to debugging (auth, movement, combat, spells, fishing, vendors).
        /// </summary>
        private static string GetOpcodeName(ushort opcode) => opcode switch
        {
            // Auth & login
            0x001 => "CMSG_CHAR_ENUM",
            0x003B => "SMSG_CHAR_ENUM",
            0x003D => "CMSG_PLAYER_LOGIN",
            0x003E => "SMSG_NEW_WORLD",
            0x003F => "SMSG_TRANSFER_PENDING",
            0x0040 => "SMSG_TRANSFER_ABORT",
            0x0041 => "CMSG_CHARACTER_CREATE",
            0x01ED => "CMSG_AUTH_SESSION",
            0x01EE => "SMSG_AUTH_RESPONSE",
            0x0236 => "SMSG_LOGIN_VERIFY_WORLD",

            // Movement
            0x00B5 => "MSG_MOVE_START_FORWARD",
            0x00B6 => "MSG_MOVE_START_BACKWARD",
            0x00B7 => "MSG_MOVE_STOP",
            0x00B9 => "MSG_MOVE_START_STRAFE_LEFT",
            0x00BA => "MSG_MOVE_START_STRAFE_RIGHT",
            0x00BB => "MSG_MOVE_STOP_STRAFE",
            0x00DA => "MSG_MOVE_HEARTBEAT",
            0x00DC => "MSG_MOVE_WORLDPORT_ACK",
            0x00EE => "MSG_MOVE_SET_FACING",
            0x00C9 => "MSG_MOVE_TELEPORT_ACK",

            // Update objects
            0x00A9 => "SMSG_UPDATE_OBJECT",
            0x00AA => "SMSG_DESTROY_OBJECT",

            // Combat & spells
            0x012E => "SMSG_SPELL_START",
            0x0130 => "SMSG_SPELL_GO",
            0x0131 => "SMSG_SPELL_FAILURE",
            0x012F => "CMSG_CAST_SPELL",
            0x012B => "CMSG_CANCEL_CAST",
            0x013E => "SMSG_CHANNEL_START",
            0x0160 => "SMSG_CHANNEL_UPDATE",
            0x013F => "SMSG_CHANNEL_NOTIFY",
            0x01A1 => "SMSG_ATTACKSTART",
            0x01A2 => "SMSG_ATTACKSTOP",
            0x01A3 => "CMSG_ATTACKSWING",
            0x01A4 => "CMSG_ATTACKSTOP",
            0x033B => "SMSG_ATTACKERSTATEUPDATE",

            // Target
            0x013D => "CMSG_SET_SELECTION",

            // Interaction & vendors
            0x01B2 => "CMSG_GOSSIP_HELLO",
            0x01B3 => "SMSG_GOSSIP_MESSAGE",
            0x01B7 => "CMSG_GOSSIP_SELECT_OPTION",
            0x01D0 => "CMSG_LIST_INVENTORY",
            0x01D1 => "SMSG_LIST_INVENTORY",
            0x01D2 => "CMSG_SELL_ITEM",
            0x01D3 => "SMSG_SELL_ITEM",
            0x01D4 => "SMSG_BUY_ITEM",
            0x01D5 => "CMSG_BUY_ITEM",

            // Loot
            0x015D => "CMSG_LOOT",
            0x015E => "CMSG_LOOT_MONEY",
            0x015F => "CMSG_LOOT_RELEASE",
            0x0161 => "SMSG_LOOT_RELEASE_RESPONSE",

            // Chat
            0x0095 => "CMSG_MESSAGECHAT",
            0x0096 => "SMSG_MESSAGECHAT",

            // Logout
            0x004B => "CMSG_LOGOUT_REQUEST",
            0x004C => "SMSG_LOGOUT_RESPONSE",
            0x004D => "SMSG_LOGOUT_COMPLETE",

            _ => ""
        };

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
            var dir = Direction == PacketDirection.Send ? "C→S" : "S→C";
            return $"[{Timestamp:HH:mm:ss.fff}] [{dir}] 0x{Opcode:X4} size={Size}";
        }
    }
}
