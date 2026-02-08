using ForegroundBotRunner.Mem;
using System.Text;

namespace ForegroundBotRunner.Statics
{
    /// <summary>
    /// Monitors all relevant memory addresses during the login process to diagnose connection issues.
    /// Writes detailed state snapshots to a log file.
    /// </summary>
    public static class LoginStateMonitor
    {
        private static readonly string LogPath;
        private static readonly object LogLock = new();
        private static int _snapshotCount = 0;
        private static DateTime _startTime = DateTime.Now;

        static LoginStateMonitor()
        {
            string wowDir;
            try { wowDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? AppContext.BaseDirectory; }
            catch { wowDir = AppContext.BaseDirectory; }
            var logsDir = Path.Combine(wowDir, "WWoWLogs");
            try { Directory.CreateDirectory(logsDir); } catch { }
            LogPath = Path.Combine(logsDir, "login_state_monitor.log");
            try { File.WriteAllText(LogPath, $"=== Login State Monitor Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n\n"); } catch { }
        }

        /// <summary>
        /// Captures and logs a complete snapshot of all relevant memory addresses.
        /// Call this frequently during the login process.
        /// </summary>
        public static void CaptureSnapshot(string phase)
        {
            try
            {
                _snapshotCount++;
                var elapsed = (DateTime.Now - _startTime).TotalMilliseconds;
                var sb = new StringBuilder();

                sb.AppendLine($"=== SNAPSHOT #{_snapshotCount} at +{elapsed:F0}ms | Phase: {phase} ===");
                sb.AppendLine($"Time: {DateTime.Now:HH:mm:ss.fff}");
                sb.AppendLine();

                // === LOGIN/CONNECTION STATE ===
                sb.AppendLine("--- LOGIN/CONNECTION STATE ---");

                // LoginState string
                try
                {
                    var loginState = MemoryManager.ReadString(Offsets.CharacterScreen.LoginState);
                    sb.AppendLine($"LoginState (0xB41478): \"{loginState}\"");
                }
                catch (Exception ex) { sb.AppendLine($"LoginState: ERROR - {ex.Message}"); }

                // ClientConnection pointer
                try
                {
                    var clientConn = MemoryManager.ReadIntPtr(Offsets.Connection.ClientConnection);
                    sb.AppendLine($"ClientConnection (0xB41DA0): 0x{clientConn:X} ({(clientConn != nint.Zero ? "CONNECTED" : "DISCONNECTED")})");
                }
                catch (Exception ex) { sb.AppendLine($"ClientConnection: ERROR - {ex.Message}"); }

                // AntiDc
                try
                {
                    var antiDc = MemoryManager.ReadInt(Offsets.Misc.AntiDc);
                    sb.AppendLine($"AntiDc (0xB41D98): {antiDc} (0x{antiDc:X})");
                }
                catch (Exception ex) { sb.AppendLine($"AntiDc: ERROR - {ex.Message}"); }

                sb.AppendLine();

                // === PLAYER/CHARACTER STATE ===
                sb.AppendLine("--- PLAYER/CHARACTER STATE ---");

                // ObjectManager base and player GUID
                try
                {
                    var managerBase = MemoryManager.ReadIntPtr(Offsets.ObjectManager.ManagerBase);
                    sb.AppendLine($"ManagerBase (0xB41414): 0x{managerBase:X}");

                    if (managerBase != nint.Zero)
                    {
                        var playerGuid = MemoryManager.ReadUlong(nint.Add(managerBase, (int)Offsets.ObjectManager.PlayerGuid));
                        sb.AppendLine($"PlayerGuid (Base+0xC0): {playerGuid} (0x{playerGuid:X})");

                        var firstObj = MemoryManager.ReadIntPtr(nint.Add(managerBase, (int)Offsets.ObjectManager.FirstObj));
                        sb.AppendLine($"FirstObject (Base+0xAC): 0x{firstObj:X}");
                    }
                    else
                    {
                        sb.AppendLine($"PlayerGuid: N/A (ManagerBase is null)");
                    }
                }
                catch (Exception ex) { sb.AppendLine($"ObjectManager: ERROR - {ex.Message}"); }

                // Character count
                try
                {
                    var charCount = MemoryManager.ReadInt(Offsets.Player.CharacterCount);
                    sb.AppendLine($"CharacterCount (0xB42140): {charCount}");
                }
                catch (Exception ex) { sb.AppendLine($"CharacterCount: ERROR - {ex.Message}"); }

                // Character screen pointer
                try
                {
                    var charScreenPtr = MemoryManager.ReadIntPtr(Offsets.CharacterScreen.Pointer);
                    sb.AppendLine($"CharScreenPtr (0xB42144): 0x{charScreenPtr:X}");
                }
                catch (Exception ex) { sb.AppendLine($"CharScreenPtr: ERROR - {ex.Message}"); }

                sb.AppendLine();

                // === WORLD/MAP STATE ===
                sb.AppendLine("--- WORLD/MAP STATE ---");

                // ContinentId
                try
                {
                    var continentId = MemoryManager.ReadUint(Offsets.Map.ContinentId);
                    string continentDesc = continentId switch
                    {
                        0xFFFFFFFF => "CHARACTER_SELECT",
                        0xFF => "LOADING",
                        0 => "Eastern Kingdoms",
                        1 => "Kalimdor",
                        _ => $"Map #{continentId}"
                    };
                    sb.AppendLine($"ContinentId (0x86F694): {continentId} (0x{continentId:X}) = {continentDesc}");
                }
                catch (Exception ex) { sb.AppendLine($"ContinentId: ERROR - {ex.Message}"); }

                // MapId (secondary)
                try
                {
                    var mapId = MemoryManager.ReadInt(Offsets.Misc.MapId);
                    sb.AppendLine($"MapId (0x84C498): {mapId}");
                }
                catch (Exception ex) { sb.AppendLine($"MapId: ERROR - {ex.Message}"); }

                // IsIngame (unreliable but informative)
                try
                {
                    var isIngame = MemoryManager.ReadInt(Offsets.Player.IsIngame);
                    sb.AppendLine($"IsIngame (0xB4B424): {isIngame} (note: unreliable on Elysium)");
                }
                catch (Exception ex) { sb.AppendLine($"IsIngame: ERROR - {ex.Message}"); }

                // IsGhost
                try
                {
                    var isGhost = MemoryManager.ReadInt(Offsets.Player.IsGhost);
                    sb.AppendLine($"IsGhost (0x835A48): {isGhost}");
                }
                catch (Exception ex) { sb.AppendLine($"IsGhost: ERROR - {ex.Message}"); }

                sb.AppendLine();

                // === LUA/GAME STATE ===
                sb.AppendLine("--- LUA/GAME STATE ---");

                // LuaState
                try
                {
                    var luaState = MemoryManager.ReadIntPtr(Offsets.Misc.LuaState);
                    sb.AppendLine($"LuaState (0xCEEF74): 0x{luaState:X}");
                }
                catch (Exception ex) { sb.AppendLine($"LuaState: ERROR - {ex.Message}"); }

                // CGInputControlActive
                try
                {
                    var inputActive = MemoryManager.ReadInt(Offsets.Misc.CGInputControlActive);
                    sb.AppendLine($"CGInputControlActive (0xBE1148): {inputActive}");
                }
                catch (Exception ex) { sb.AppendLine($"CGInputControlActive: ERROR - {ex.Message}"); }

                // IsCasting
                try
                {
                    var isCasting = MemoryManager.ReadInt(Offsets.Player.IsCasting);
                    sb.AppendLine($"IsCasting (0xCECA88): {isCasting}");
                }
                catch (Exception ex) { sb.AppendLine($"IsCasting: ERROR - {ex.Message}"); }

                // RealmName
                try
                {
                    var realmName = MemoryManager.ReadString(Offsets.Misc.RealmName);
                    sb.AppendLine($"RealmName (0xC27FC1): \"{realmName}\"");
                }
                catch (Exception ex) { sb.AppendLine($"RealmName: ERROR - {ex.Message}"); }

                sb.AppendLine();

                // === WARDEN (ANTI-CHEAT) STATE ===
                sb.AppendLine("--- WARDEN STATE ---");

                try
                {
                    var wardenPtr = MemoryManager.ReadIntPtr(Offsets.Warden.WardenPtr1);
                    sb.AppendLine($"WardenPtr1 (0xCE8978): 0x{wardenPtr:X} ({(wardenPtr != nint.Zero ? "ACTIVE" : "INACTIVE")})");

                    if (wardenPtr != nint.Zero)
                    {
                        // Try to read some Warden internals
                        try
                        {
                            var scanStart = MemoryManager.ReadIntPtr(nint.Add(wardenPtr, (int)Offsets.Warden.WardenMemScanStart));
                            var pageScan = MemoryManager.ReadIntPtr(nint.Add(wardenPtr, (int)Offsets.Warden.WardenPageScan));
                            sb.AppendLine($"  MemScanStart: 0x{scanStart:X}");
                            sb.AppendLine($"  PageScan: 0x{pageScan:X}");
                        }
                        catch { sb.AppendLine($"  (Could not read Warden internals)"); }
                    }
                }
                catch (Exception ex) { sb.AppendLine($"Warden: ERROR - {ex.Message}"); }

                sb.AppendLine();

                // === LAST HARDWARE ACTION ===
                try
                {
                    var lastHardwareAction = MemoryManager.ReadUint(Offsets.Functions.LastHardwareAction);
                    sb.AppendLine($"LastHardwareAction (0xCF0BC8): {lastHardwareAction}");
                }
                catch (Exception ex) { sb.AppendLine($"LastHardwareAction: ERROR - {ex.Message}"); }

                sb.AppendLine();
                sb.AppendLine("========================================");
                sb.AppendLine();

                // Write to log
                lock (LogLock)
                {
                    File.AppendAllText(LogPath, sb.ToString());
                }
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(LogPath, $"SNAPSHOT ERROR: {ex}\n"); } catch { }
            }
        }

        /// <summary>
        /// Resets the snapshot counter and start time for a new monitoring session.
        /// </summary>
        public static void Reset()
        {
            _snapshotCount = 0;
            _startTime = DateTime.Now;
            try { File.WriteAllText(LogPath, $"=== Login State Monitor RESET at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n\n"); } catch { }
        }

        /// <summary>
        /// Log a simple message without a full snapshot.
        /// </summary>
        public static void Log(string message)
        {
            try
            {
                var elapsed = (DateTime.Now - _startTime).TotalMilliseconds;
                lock (LogLock)
                {
                    File.AppendAllText(LogPath, $"[+{elapsed:F0}ms] {message}\n");
                }
            }
            catch { }
        }
    }
}
