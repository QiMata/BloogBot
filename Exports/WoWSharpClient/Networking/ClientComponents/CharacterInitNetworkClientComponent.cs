using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Handles login-time initialization packets sent by the server when a character enters the world.
    /// Parses: SMSG_ACTION_BUTTONS, SMSG_SET_PROFICIENCY, SMSG_BINDPOINTUPDATE,
    /// SMSG_INITIALIZE_FACTIONS, SMSG_TUTORIAL_FLAGS.
    /// </summary>
    public class CharacterInitNetworkClientComponent : NetworkClientComponent, ICharacterInitNetworkClientComponent
    {
        private const int MAX_ACTION_BUTTONS = 120;

        private readonly IWorldClient _worldClient;
        private readonly ILogger<CharacterInitNetworkClientComponent> _logger;

        private ActionButton[] _actionButtons = Array.Empty<ActionButton>();
        private readonly Dictionary<byte, uint> _proficiencies = new();
        private BindPointData? _bindPoint;
        private FactionEntry[] _factions = Array.Empty<FactionEntry>();
        private uint[] _tutorialFlags = Array.Empty<uint>();
        private bool _isInitialized;

        // Manual streams for publishing parsed events
        private readonly Subject<IReadOnlyList<ActionButton>> _actionButtonSubject = new();
        private readonly Subject<ProficiencyData> _proficiencySubject = new();
        private readonly Subject<BindPointData> _bindPointSubject = new();

        // Subscriptions for cleanup
        private readonly List<IDisposable> _subscriptions = new();

        public CharacterInitNetworkClientComponent(IWorldClient worldClient, ILogger<CharacterInitNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Subscribe to login initialization opcodes
            SubscribeToOpcode(Opcode.SMSG_ACTION_BUTTONS, HandleActionButtons);
            SubscribeToOpcode(Opcode.SMSG_SET_PROFICIENCY, HandleSetProficiency);
            SubscribeToOpcode(Opcode.SMSG_BINDPOINTUPDATE, HandleBindPointUpdate);
            SubscribeToOpcode(Opcode.SMSG_INITIALIZE_FACTIONS, HandleInitializeFactions);
            SubscribeToOpcode(Opcode.SMSG_TUTORIAL_FLAGS, HandleTutorialFlags);
        }

        private void SubscribeToOpcode(Opcode opcode, Action<ReadOnlyMemory<byte>> handler)
        {
            var stream = _worldClient.RegisterOpcodeHandler(opcode);
            if (stream != null)
            {
                _subscriptions.Add(stream.Subscribe(handler));
            }
        }

        #region Properties

        public bool IsInitialized => _isInitialized;
        public IReadOnlyList<ActionButton> ActionButtons => _actionButtons;
        public IReadOnlyDictionary<byte, uint> Proficiencies => _proficiencies;
        public BindPointData? BindPoint => _bindPoint;
        public IReadOnlyList<FactionEntry> Factions => _factions;
        public IReadOnlyList<uint> TutorialFlags => _tutorialFlags;

        public IObservable<IReadOnlyList<ActionButton>> ActionButtonUpdates => _actionButtonSubject.AsObservable();
        public IObservable<ProficiencyData> ProficiencyUpdates => _proficiencySubject.AsObservable();
        public IObservable<BindPointData> BindPointUpdates => _bindPointSubject.AsObservable();

        #endregion

        #region Action Button Lookup

        /// <inheritdoc />
        public uint? GetSpellIdForActionBarSlot(byte slot)
        {
            if (slot >= _actionButtons.Length)
                return null;

            var button = _actionButtons[slot];
            if (button.IsEmpty)
                return null;

            if (button.Type == ActionButtonType.Spell)
                return button.ActionId;

            return null;
        }

        #endregion

        #region SMSG Handlers

        /// <summary>
        /// SMSG_ACTION_BUTTONS (0x129): 120 × uint32 = 480 bytes.
        /// Each uint32: bits 0-23 = action ID, bits 24-31 = action type.
        /// </summary>
        private void HandleActionButtons(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            int expectedSize = MAX_ACTION_BUTTONS * 4;

            if (span.Length < expectedSize)
            {
                _logger.LogWarning("SMSG_ACTION_BUTTONS payload too small: {Length} bytes (expected {Expected})",
                    span.Length, expectedSize);
                return;
            }

            var buttons = new ActionButton[MAX_ACTION_BUTTONS];
            int nonEmpty = 0;

            for (int i = 0; i < MAX_ACTION_BUTTONS; i++)
            {
                uint packed = BitConverter.ToUInt32(span.Slice(i * 4, 4));
                buttons[i] = new ActionButton(packed);
                if (!buttons[i].IsEmpty) nonEmpty++;
            }

            _actionButtons = buttons;
            _isInitialized = true;

            _logger.LogInformation("Received SMSG_ACTION_BUTTONS: {NonEmpty}/{Total} slots populated",
                nonEmpty, MAX_ACTION_BUTTONS);

            _actionButtonSubject.OnNext(buttons);
        }

        /// <summary>
        /// SMSG_SET_PROFICIENCY (0x127): uint8 itemClass + uint32 subclassMask = 5 bytes.
        /// </summary>
        private void HandleSetProficiency(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 5)
            {
                _logger.LogWarning("SMSG_SET_PROFICIENCY payload too small: {Length} bytes", span.Length);
                return;
            }

            byte itemClass = span[0];
            uint subclassMask = BitConverter.ToUInt32(span.Slice(1, 4));

            _proficiencies[itemClass] = subclassMask;

            var data = new ProficiencyData(itemClass, subclassMask);
            _logger.LogDebug("Received SMSG_SET_PROFICIENCY: itemClass={ItemClass}, mask=0x{Mask:X8}",
                itemClass, subclassMask);

            _proficiencySubject.OnNext(data);
        }

        /// <summary>
        /// SMSG_BINDPOINTUPDATE (0x155): float x + float y + float z + uint32 mapId + uint32 areaId = 20 bytes.
        /// </summary>
        private void HandleBindPointUpdate(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 20)
            {
                _logger.LogWarning("SMSG_BINDPOINTUPDATE payload too small: {Length} bytes", span.Length);
                return;
            }

            float x = BitConverter.ToSingle(span.Slice(0, 4));
            float y = BitConverter.ToSingle(span.Slice(4, 4));
            float z = BitConverter.ToSingle(span.Slice(8, 4));
            uint mapId = BitConverter.ToUInt32(span.Slice(12, 4));
            uint areaId = BitConverter.ToUInt32(span.Slice(16, 4));

            var data = new BindPointData(x, y, z, mapId, areaId);
            _bindPoint = data;

            _logger.LogInformation("Received SMSG_BINDPOINTUPDATE: ({X:F1}, {Y:F1}, {Z:F1}) map={MapId} area={AreaId}",
                x, y, z, mapId, areaId);

            _bindPointSubject.OnNext(data);
        }

        /// <summary>
        /// SMSG_INITIALIZE_FACTIONS (0x122): uint32 count (64) + 64 × (uint8 flags + uint32 standing) = 324 bytes.
        /// </summary>
        private void HandleInitializeFactions(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 4)
            {
                _logger.LogWarning("SMSG_INITIALIZE_FACTIONS payload too small: {Length} bytes", span.Length);
                return;
            }

            uint count = BitConverter.ToUInt32(span.Slice(0, 4));
            int offset = 4;

            var factions = new FactionEntry[count];
            int visible = 0;

            for (int i = 0; i < count && offset + 5 <= span.Length; i++)
            {
                byte flags = span[offset];
                int standing = BitConverter.ToInt32(span.Slice(offset + 1, 4));
                factions[i] = new FactionEntry(flags, standing);
                if (factions[i].IsVisible) visible++;
                offset += 5;
            }

            _factions = factions;

            _logger.LogDebug("Received SMSG_INITIALIZE_FACTIONS: {Count} factions ({Visible} visible)",
                count, visible);
        }

        /// <summary>
        /// SMSG_TUTORIAL_FLAGS (0xFD): 8 × uint32 = 32 bytes.
        /// </summary>
        private void HandleTutorialFlags(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 32)
            {
                _logger.LogWarning("SMSG_TUTORIAL_FLAGS payload too small: {Length} bytes", span.Length);
                return;
            }

            var flags = new uint[8];
            for (int i = 0; i < 8; i++)
            {
                flags[i] = BitConverter.ToUInt32(span.Slice(i * 4, 4));
            }

            _tutorialFlags = flags;

            _logger.LogDebug("Received SMSG_TUTORIAL_FLAGS");
        }

        #endregion

        #region Dispose

        public override void Dispose()
        {
            foreach (var sub in _subscriptions)
            {
                sub.Dispose();
            }
            _subscriptions.Clear();

            _actionButtonSubject.Dispose();
            _proficiencySubject.Dispose();
            _bindPointSubject.Dispose();

            base.Dispose();
        }

        #endregion
    }
}
