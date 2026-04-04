using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using WoWSharpClient.Client;

namespace BackgroundBotRunner.Diagnostics;

/// <summary>
/// P23.2: Records binary packet payloads alongside the CSV opcode trace.
/// Captures full payload bytes for interaction opcodes (AH, bank, mail, vendor, trainer).
/// Saves as sidecar .bin file: one file per recording session, indexed by packet number.
/// Format: [4-byte index][2-byte opcode][4-byte payload length][payload bytes]...
/// </summary>
public sealed class PacketPayloadRecorder : IDisposable
{
    private static readonly string RecordingDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WWoW", "PhysicsRecordings");

    private readonly ILogger _logger;
    private FileStream? _outputStream;
    private BinaryWriter? _writer;
    private string? _accountName;
    private bool _isRecording;
    private int _nextIndex;

    // Interaction opcodes to capture payloads for
    private static readonly ConcurrentDictionary<Opcode, bool> _captureOpcodes = new()
    {
        // Auction House
        [Opcode.CMSG_AUCTION_LIST_ITEMS] = true,
        [Opcode.CMSG_AUCTION_SELL_ITEM] = true,
        [Opcode.CMSG_AUCTION_PLACE_BID] = true,
        [Opcode.CMSG_AUCTION_REMOVE_ITEM] = true,
        [Opcode.SMSG_AUCTION_LIST_RESULT] = true,
        [Opcode.SMSG_AUCTION_COMMAND_RESULT] = true,
        // Bank
        [Opcode.CMSG_BUY_BANK_SLOT] = true,
        [Opcode.SMSG_BUY_BANK_SLOT_RESULT] = true,
        // Mail
        [Opcode.CMSG_SEND_MAIL] = true,
        [Opcode.SMSG_SEND_MAIL_RESULT] = true,
        [Opcode.CMSG_GET_MAIL_LIST] = true,
        [Opcode.SMSG_MAIL_LIST_RESULT] = true,
        // Vendor
        [Opcode.CMSG_BUY_ITEM] = true,
        [Opcode.CMSG_SELL_ITEM] = true,
        [Opcode.SMSG_BUY_ITEM] = true,
        // Trainer
        [Opcode.CMSG_TRAINER_BUY_SPELL] = true,
        [Opcode.SMSG_TRAINER_LIST] = true,
        // Trade
        [Opcode.CMSG_INITIATE_TRADE] = true,
        [Opcode.SMSG_TRADE_STATUS] = true,
        // Quest
        [Opcode.CMSG_QUESTGIVER_ACCEPT_QUEST] = true,
        [Opcode.CMSG_QUESTGIVER_COMPLETE_QUEST] = true,
        [Opcode.SMSG_QUESTGIVER_QUEST_DETAILS] = true,
    };

    public PacketPayloadRecorder(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PacketPayloadRecorder>();
    }

    /// <summary>Check if an opcode should have its payload captured.</summary>
    public static bool ShouldCapture(Opcode opcode) => _captureOpcodes.ContainsKey(opcode);

    /// <summary>Start recording payloads for a session.</summary>
    public void StartRecording(string accountName)
    {
        StopRecording();

        _accountName = accountName;
        Directory.CreateDirectory(RecordingDir);
        var path = Path.Combine(RecordingDir, $"payloads_{accountName}.bin");
        _outputStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new BinaryWriter(_outputStream);
        _isRecording = true;
        _nextIndex = 0;

        _logger.LogInformation("[DIAG] Payload recording started: {Path}", path);
    }

    /// <summary>Record a packet payload if it matches a capture opcode.</summary>
    public void RecordPayload(Opcode opcode, byte[] payload)
    {
        if (!_isRecording || _writer == null) return;
        if (!ShouldCapture(opcode)) return;

        lock (_writer)
        {
            _writer.Write(_nextIndex++);
            _writer.Write((ushort)opcode);
            _writer.Write(payload.Length);
            _writer.Write(payload);
        }
    }

    /// <summary>Stop recording and flush.</summary>
    public void StopRecording()
    {
        if (!_isRecording) return;
        _isRecording = false;

        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;
        _outputStream?.Dispose();
        _outputStream = null;

        if (_nextIndex > 0)
            _logger.LogInformation("[DIAG] Payload recording stopped: {Count} payloads captured", _nextIndex);

        _nextIndex = 0;
    }

    public void Dispose() => StopRecording();
}
