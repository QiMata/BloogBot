using Communication;
using Game;
using Google.Protobuf;
using WoWStateManager.Listeners;
using System.Text.Json;

namespace WoWStateManager.Services
{
    /// <summary>
    /// Service that monitors WoWActivitySnapshots and records movement data.
    /// Recording is toggled when a spell cast is detected (channelSpellId becomes non-zero).
    ///
    /// This service runs in StateManager (outside of WoW process) which is safer
    /// and easier to debug than running inside the game.
    /// </summary>
    public class MovementRecordingService : IDisposable
    {
        private readonly ILogger<MovementRecordingService> _logger;
        private readonly CharacterStateSocketListener _snapshotListener;

        private volatile bool _isRecording;
        private readonly object _recordingLock = new();

        private MovementRecording? _currentRecording;
        private readonly Dictionary<string, uint> _lastKnownSpellId = new();
        private DateTime _lastFrameTime = DateTime.MinValue;
        private readonly TimeSpan _frameInterval = TimeSpan.FromMilliseconds(50); // 20 FPS

        public bool IsRecording => _isRecording;
        public int FrameCount => _currentRecording?.Frames.Count ?? 0;

        public MovementRecordingService(
            CharacterStateSocketListener snapshotListener,
            ILogger<MovementRecordingService> logger)
        {
            _snapshotListener = snapshotListener;
            _logger = logger;

            _logger.LogInformation("MovementRecordingService initialized. Monitoring snapshots for spell casts.");
        }

        /// <summary>
        /// Checks the latest snapshot for spell casting and toggles recording.
        /// Also captures movement frames if recording is active.
        /// Call this periodically from the main StateManager loop.
        /// </summary>
        public void ProcessSnapshots()
        {
            foreach (var kvp in _snapshotListener.CurrentActivityMemberList)
            {
                var accountName = kvp.Key;
                var snapshot = kvp.Value;

                if (snapshot?.Player?.Unit == null)
                    continue;

                // Check for spell cast toggle (channelSpellId changing from 0 to non-zero)
                var currentSpellId = snapshot.Player.Unit.ChannelSpellId;
                var lastSpellId = _lastKnownSpellId.TryGetValue(accountName, out var spellId) ? spellId : 0u;

                // Toggle recording when spell casting starts (transition from 0 to non-zero)
                if (currentSpellId != 0 && lastSpellId == 0)
                {
                    _logger.LogInformation($"Spell cast detected (ID: {currentSpellId}) for {accountName}");
                    ToggleRecording(accountName, snapshot);
                }

                _lastKnownSpellId[accountName] = currentSpellId;

                // Capture frame if recording is active
                if (_isRecording && DateTime.UtcNow - _lastFrameTime >= _frameInterval)
                {
                    CaptureFrame(snapshot);
                    _lastFrameTime = DateTime.UtcNow;
                }
            }
        }

        private void ToggleRecording(string accountName, WoWActivitySnapshot snapshot)
        {
            if (_isRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording(accountName, snapshot);
            }
        }

        private void StartRecording(string accountName, WoWActivitySnapshot snapshot)
        {
            if (_isRecording)
            {
                _logger.LogWarning("Recording already in progress");
                return;
            }

            lock (_recordingLock)
            {
                var characterName = snapshot.CharacterName ?? accountName;
                var mapId = snapshot.Player?.Unit?.GameObject?.Base?.MapId ?? 0;
                var zoneName = "Unknown"; // Would need zone lookup

                _currentRecording = new MovementRecording
                {
                    CharacterName = characterName,
                    MapId = mapId,
                    ZoneName = zoneName,
                    StartTimestampUtc = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    FrameIntervalMs = (uint)_frameInterval.TotalMilliseconds,
                    Description = $"Recording started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                };

                _isRecording = true;
                _lastFrameTime = DateTime.UtcNow;

                _logger.LogInformation($"RECORDING_STARTED: Character='{characterName}', MapId={mapId}");
            }
        }

        private void StopRecording()
        {
            if (!_isRecording)
            {
                _logger.LogWarning("No recording in progress");
                return;
            }

            lock (_recordingLock)
            {
                _isRecording = false;

                if (_currentRecording == null || _currentRecording.Frames.Count == 0)
                {
                    _logger.LogWarning("RECORDING_STOPPED: No frames captured");
                    _currentRecording = null;
                    return;
                }

                var frameCount = _currentRecording.Frames.Count;
                var durationMs = _currentRecording.Frames.Count > 0
                    ? _currentRecording.Frames[^1].FrameTimestamp
                    : 0;

                // Save the recording
                var filePath = SaveRecording(_currentRecording);

                _logger.LogInformation($"RECORDING_STOPPED: {frameCount} frames, {durationMs}ms duration");
                _logger.LogInformation($"Saved recording to: {filePath}");

                _currentRecording = null;
            }
        }

        private void CaptureFrame(WoWActivitySnapshot snapshot)
        {
            if (_currentRecording == null || snapshot.Player?.Unit == null)
                return;

            var unit = snapshot.Player.Unit;
            var gameObject = unit.GameObject;
            var baseObject = gameObject?.Base;

            if (baseObject == null)
                return;

            var elapsedMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() -
                           (long)_currentRecording.StartTimestampUtc;

            var frame = new MovementData
            {
                FrameTimestamp = (ulong)elapsedMs,
                MovementFlags = unit.MovementFlags,
                Position = new Position
                {
                    X = baseObject.Position?.X ?? 0,
                    Y = baseObject.Position?.Y ?? 0,
                    Z = baseObject.Position?.Z ?? 0
                },
                Facing = baseObject.Facing,
                // These fields would need to be added to the snapshot if needed
                FallTime = 0,
                WalkSpeed = 2.5f, // Default values - would need to be read from memory
                RunSpeed = 7.0f,
                RunBackSpeed = 4.5f,
                SwimSpeed = 4.722f,
                SwimBackSpeed = 2.5f,
                TurnRate = 3.1415f,
                JumpVerticalSpeed = 0,
                JumpSinAngle = 0,
                JumpCosAngle = 0,
                JumpHorizontalSpeed = 0,
                SwimPitch = 0
            };

            lock (_recordingLock)
            {
                _currentRecording.Frames.Add(frame);

                // Log progress every 100 frames
                if (_currentRecording.Frames.Count % 100 == 0)
                {
                    _logger.LogDebug($"Recording: {_currentRecording.Frames.Count} frames captured");
                }
            }
        }

        private string SaveRecording(MovementRecording recording)
        {
            // Create recordings directory in Documents
            var recordingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "BloogBot",
                "MovementRecordings"
            );
            Directory.CreateDirectory(recordingsDir);

            // Generate filename
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string safeName = new string(recording.CharacterName.Where(c => char.IsLetterOrDigit(c)).ToArray());
            if (string.IsNullOrEmpty(safeName)) safeName = "Unknown";
            string baseFileName = $"{safeName}_{recording.ZoneName.Replace(' ', '_')}_{timestamp}";

            // Save as JSON (human-readable)
            string jsonPath = Path.Combine(recordingsDir, $"{baseFileName}.json");
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Convert to a serializable format
            var serializableRecording = new
            {
                recording.CharacterName,
                recording.MapId,
                recording.ZoneName,
                recording.StartTimestampUtc,
                recording.FrameIntervalMs,
                recording.Description,
                FrameCount = recording.Frames.Count,
                DurationMs = recording.Frames.Count > 0 ? recording.Frames[^1].FrameTimestamp : 0,
                Frames = recording.Frames.Select(f => new
                {
                    f.FrameTimestamp,
                    f.MovementFlags,
                    MovementFlagsHex = $"0x{f.MovementFlags:X8}",
                    Position = new { f.Position.X, f.Position.Y, f.Position.Z },
                    f.Facing,
                    f.FallTime,
                    f.WalkSpeed,
                    f.RunSpeed,
                    f.RunBackSpeed,
                    f.SwimSpeed,
                    f.SwimBackSpeed,
                    f.TurnRate,
                    f.JumpVerticalSpeed,
                    f.JumpSinAngle,
                    f.JumpCosAngle,
                    f.JumpHorizontalSpeed,
                    f.SwimPitch
                })
            };

            File.WriteAllText(jsonPath, JsonSerializer.Serialize(serializableRecording, jsonOptions));

            // Also save as binary protobuf (compact, for physics engine consumption)
            string protoPath = Path.Combine(recordingsDir, $"{baseFileName}.bin");
            using (var output = File.Create(protoPath))
            {
                recording.WriteTo(output);
            }

            _logger.LogInformation($"Saved recording:\n  JSON: {jsonPath}\n  Binary: {protoPath}");

            return jsonPath;
        }

        /// <summary>
        /// Force stop any active recording (for cleanup)
        /// </summary>
        public void ForceStopRecording()
        {
            if (_isRecording)
            {
                StopRecording();
            }
        }

        public void Dispose()
        {
            ForceStopRecording();
        }
    }
}
