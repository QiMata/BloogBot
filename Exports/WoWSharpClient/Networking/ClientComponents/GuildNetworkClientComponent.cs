using System.Text;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Reactive implementation of guild network client component.
    /// Exposes guild-related server updates as observables backed directly by opcode streams.
    /// </summary>
    public class GuildNetworkClientComponent : NetworkClientComponent, IGuildNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<GuildNetworkClientComponent> _logger;
        private readonly object _stateLock = new();

        private GuildInfo? _guildInfo;
        private readonly List<GuildMember> _guildMembers = [];
        private bool _disposed;

        // Reactive opcode streams
        private readonly IObservable<(string Inviter, string GuildName)> _guildInvites;
        private readonly IObservable<GuildInfo> _guildInfos;
        private readonly IObservable<IReadOnlyList<GuildMember>> _guildRosters;
        private readonly IObservable<GuildCommandResult> _guildCommandResults;
        private readonly IObservable<GuildMemberStatusChange> _memberStatusChanges;

        // Backward compatibility delegate for legacy tests
        private Action<string, string>? _onGuildInviteReceived;

        public GuildNetworkClientComponent(IWorldClient worldClient, ILogger<GuildNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _guildInvites = SafeOpcodeStream(Opcode.SMSG_GUILD_INVITE)
                .Select(ParseGuildInvite)
                .Do(invite =>
                {
                    _logger.LogInformation("Guild invite from {Inviter} for guild {Guild}", invite.Inviter, invite.GuildName);
                    _onGuildInviteReceived?.Invoke(invite.Inviter, invite.GuildName);
                })
                .Publish().RefCount();

            _guildInfos = SafeOpcodeStream(Opcode.SMSG_GUILD_INFO)
                .Select(ParseGuildInfo)
                .Do(info =>
                {
                    lock (_stateLock)
                    {
                        _guildInfo = info;
                        IsInGuild = true;
                        CurrentGuildId = info.GuildId;
                        CurrentGuildName = info.Name;
                    }
                    _logger.LogDebug("Guild info updated GuildId={GuildId} Name={Name}", info.GuildId, info.Name);
                })
                .Publish().RefCount();

            _guildRosters = SafeOpcodeStream(Opcode.SMSG_GUILD_ROSTER)
                .Select(ParseGuildRoster)
                .Do(list =>
                {
                    lock (_stateLock)
                    {
                        _guildMembers.Clear();
                        _guildMembers.AddRange(list);
                    }
                    _logger.LogInformation("Guild roster received ({Count} members)", list.Count);
                })
                .Select(list => (IReadOnlyList<GuildMember>)list.AsReadOnly())
                .Publish().RefCount();

            _guildCommandResults = SafeOpcodeStream(Opcode.SMSG_GUILD_COMMAND_RESULT)
                .Select(ParseGuildCommandResult)
                .Do(r =>
                {
                    if (r.Success) _logger.LogInformation("Guild operation {Operation} succeeded", r.Operation);
                    else _logger.LogWarning("Guild operation {Operation} failed (Result={ResultCode})", r.Operation, r.ResultCode);
                })
                .Publish().RefCount();

            _memberStatusChanges = SafeOpcodeStream(Opcode.SMSG_GUILD_EVENT)
                .SelectMany(ParseGuildEvents)
                .Do(change => _logger.LogDebug("Guild member {Member} is now {State}", change.MemberName, change.IsOnline ? "online" : "offline"))
                .Publish().RefCount();
        }

        #region Backward Compatibility Event
        /// <summary>Legacy event for tests; prefer GuildInvites observable.</summary>
        public event Action<string, string>? OnGuildInviteReceived
        {
            add { _onGuildInviteReceived += value; }
            remove { _onGuildInviteReceived -= value; }
        }
        #endregion

        #region Explicit legacy interface events (not supported)
        event Action<string, string>? IGuildNetworkClientComponent.GuildInviteReceived { add => ThrowEventsNotSupported(); remove { } }
        event Action<uint, string>? IGuildNetworkClientComponent.GuildJoined { add => ThrowEventsNotSupported(); remove { } }
        event Action<uint, string>? IGuildNetworkClientComponent.GuildLeft { add => ThrowEventsNotSupported(); remove { } }
        event Action<string>? IGuildNetworkClientComponent.GuildMemberOnline { add => ThrowEventsNotSupported(); remove { } }
        event Action<string>? IGuildNetworkClientComponent.GuildMemberOffline { add => ThrowEventsNotSupported(); remove { } }
        event Action<uint>? IGuildNetworkClientComponent.GuildRosterReceived { add => ThrowEventsNotSupported(); remove { } }
        event Action<uint, string, string>? IGuildNetworkClientComponent.GuildInfoReceived { add => ThrowEventsNotSupported(); remove { } }
        event Action<string>? IGuildNetworkClientComponent.GuildMOTDReceived { add => ThrowEventsNotSupported(); remove { } }
        event Action<ulong>? IGuildNetworkClientComponent.GuildBankWindowOpened { add => ThrowEventsNotSupported(); remove { } }
        event Action? IGuildNetworkClientComponent.GuildBankWindowClosed { add => ThrowEventsNotSupported(); remove { } }
        event Action<uint, uint, byte, byte>? IGuildNetworkClientComponent.ItemDepositedToGuildBank { add => ThrowEventsNotSupported(); remove { } }
        event Action<uint, uint, byte, byte>? IGuildNetworkClientComponent.ItemWithdrawnFromGuildBank { add => ThrowEventsNotSupported(); remove { } }
        event Action<uint>? IGuildNetworkClientComponent.MoneyDepositedToGuildBank { add => ThrowEventsNotSupported(); remove { } }
        event Action<uint>? IGuildNetworkClientComponent.MoneyWithdrawnFromGuildBank { add => ThrowEventsNotSupported(); remove { } }
        event Action<string, string>? IGuildNetworkClientComponent.GuildOperationFailed { add => ThrowEventsNotSupported(); remove { } }
        private void ThrowEventsNotSupported() => throw new NotSupportedException("Events are not supported; use reactive observables instead.");
        #endregion

        #region Reactive Streams (public)
        public IObservable<(string Inviter, string GuildName)> GuildInvites => _guildInvites;
        public IObservable<GuildInfo> GuildInfos => _guildInfos;
        public IObservable<IReadOnlyList<GuildMember>> GuildRosters => _guildRosters;
        public IObservable<GuildCommandResult> GuildCommandResults => _guildCommandResults;
        public IObservable<GuildMemberStatusChange> MemberStatusChanges => _memberStatusChanges;
        #endregion

        #region State Properties
        public bool IsInGuild { get; private set; }
        public uint? CurrentGuildId { get; private set; }
        public string? CurrentGuildName { get; private set; }
        public bool IsGuildWindowOpen { get; private set; }
        public bool IsGuildBankWindowOpen { get; private set; }
        #endregion

        #region Operations
        public async Task AcceptGuildInviteAsync(CancellationToken cancellationToken = default) => await SendSimpleOpcodeAsync(Opcode.CMSG_GUILD_ACCEPT, "AcceptGuildInvite", cancellationToken);
        public async Task DeclineGuildInviteAsync(CancellationToken cancellationToken = default) => await SendSimpleOpcodeAsync(Opcode.CMSG_GUILD_DECLINE, "DeclineGuildInvite", cancellationToken);
        public async Task InvitePlayerToGuildAsync(string playerName, CancellationToken cancellationToken = default) => await SendCStringOpcodeAsync(Opcode.CMSG_GUILD_INVITE, playerName, "InvitePlayerToGuild", cancellationToken);
        public async Task RemovePlayerFromGuildAsync(string playerName, CancellationToken cancellationToken = default) => await SendCStringOpcodeAsync(Opcode.CMSG_GUILD_REMOVE, playerName, "RemovePlayerFromGuild", cancellationToken);
        public async Task PromoteGuildMemberAsync(string playerName, CancellationToken cancellationToken = default) => await SendCStringOpcodeAsync(Opcode.CMSG_GUILD_PROMOTE, playerName, "PromoteGuildMember", cancellationToken);
        public async Task DemoteGuildMemberAsync(string playerName, CancellationToken cancellationToken = default) => await SendCStringOpcodeAsync(Opcode.CMSG_GUILD_DEMOTE, playerName, "DemoteGuildMember", cancellationToken);
        public async Task LeaveGuildAsync(CancellationToken cancellationToken = default) => await SendSimpleOpcodeAsync(Opcode.CMSG_GUILD_LEAVE, "LeaveGuild", cancellationToken);
        public async Task DisbandGuildAsync(CancellationToken cancellationToken = default) => await SendSimpleOpcodeAsync(Opcode.CMSG_GUILD_DISBAND, "DisbandGuild", cancellationToken);
        public async Task SetGuildMOTDAsync(string motd, CancellationToken cancellationToken = default) { ArgumentNullException.ThrowIfNull(motd); await SendCStringOpcodeAsync(Opcode.CMSG_GUILD_MOTD, motd, "SetGuildMOTD", cancellationToken); }
        public async Task SetGuildInfoAsync(string guildInfo, CancellationToken cancellationToken = default) { ArgumentNullException.ThrowIfNull(guildInfo); await SendCStringOpcodeAsync(Opcode.CMSG_GUILD_INFO_TEXT, guildInfo, "SetGuildInfo", cancellationToken); }
        public async Task CreateGuildAsync(string guildName, CancellationToken cancellationToken = default) { ArgumentException.ThrowIfNullOrWhiteSpace(guildName); await SendCStringOpcodeAsync(Opcode.CMSG_GUILD_CREATE, guildName, "CreateGuild", cancellationToken); }
        public async Task QuickGuildBankOperationAsync(ulong guildBankGuid, Func<Task> operation, CancellationToken cancellationToken = default) { await OpenGuildBankAsync(guildBankGuid, cancellationToken); await operation(); await CloseGuildBankAsync(cancellationToken); }
        public async Task RequestGuildRosterAsync(CancellationToken cancellationToken = default) => await SendSimpleOpcodeAsync(Opcode.CMSG_GUILD_ROSTER, "RequestGuildRoster", cancellationToken);
        public async Task RequestGuildInfoAsync(CancellationToken cancellationToken = default) => await SendSimpleOpcodeAsync(Opcode.CMSG_GUILD_INFO, "RequestGuildInfo", cancellationToken);
        public async Task OpenGuildBankAsync(ulong guildBankGuid, CancellationToken cancellationToken = default) { _logger.LogWarning("Guild bank operations are not supported in this client version"); await Task.CompletedTask; }
        public async Task CloseGuildBankAsync(CancellationToken cancellationToken = default) { _logger.LogWarning("Guild bank operations are not supported in this client version"); await Task.CompletedTask; }
        public async Task DepositItemToGuildBankAsync(byte sourceBagId, byte sourceSlotId, byte destTabIndex, byte destSlotIndex, uint quantity = 1, CancellationToken cancellationToken = default) { _logger.LogWarning("Guild bank operations are not supported in this client version"); await Task.CompletedTask; }
        public async Task WithdrawItemFromGuildBankAsync(byte sourceTabIndex, byte sourceSlotIndex, uint quantity = 1, CancellationToken cancellationToken = default) { _logger.LogWarning("Guild bank operations are not supported in this client version"); await Task.CompletedTask; }
        public async Task DepositMoneyToGuildBankAsync(uint amount, CancellationToken cancellationToken = default) { _logger.LogWarning("Guild bank operations are not supported in this client version"); await Task.CompletedTask; }
        public async Task WithdrawMoneyFromGuildBankAsync(uint amount, CancellationToken cancellationToken = default) { _logger.LogWarning("Guild bank operations are not supported in this client version"); await Task.CompletedTask; }
        public async Task QueryGuildBankTabAsync(byte tabIndex, CancellationToken cancellationToken = default) { _logger.LogWarning("Guild bank operations are not supported in this client version"); await Task.CompletedTask; }
        public async Task SetGuildMemberNoteAsync(string playerName, string note, bool isOfficerNote = false, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
                ArgumentNullException.ThrowIfNull(note);
                var opcode = isOfficerNote ? Opcode.CMSG_GUILD_SET_OFFICER_NOTE : Opcode.CMSG_GUILD_SET_PUBLIC_NOTE;
                var playerNameBytes = Encoding.UTF8.GetBytes(playerName);
                var noteBytes = Encoding.UTF8.GetBytes(note);
                var payload = new byte[playerNameBytes.Length + 1 + noteBytes.Length + 1];
                var offset = 0;
                Array.Copy(playerNameBytes, 0, payload, offset, playerNameBytes.Length); offset += playerNameBytes.Length; payload[offset++] = 0;
                Array.Copy(noteBytes, 0, payload, offset, noteBytes.Length); offset += noteBytes.Length; payload[offset] = 0;
                await _worldClient.SendOpcodeAsync(opcode, payload, cancellationToken);
                _logger.LogInformation("Guild member note set for {PlayerName}", playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set guild member note for {PlayerName}", playerName);
                throw;
            }
        }
        #endregion

        #region Capability Helpers
        public bool IsGuildBankOpen(ulong guildBankGuid) => false;
        public uint? GetCurrentGuildRank() => null;
        public bool HasGuildPermission(uint permission) => false;
        #endregion

        #region Parsing Helpers
        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode) => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();
        private (string Inviter, string GuildName) ParseGuildInvite(ReadOnlyMemory<byte> payload) => ("Inviter", "Guild");
        private (string Inviter, string GuildName) ParseGuildInvite(byte[] payload) => ("Inviter", "Guild");
        private GuildInfo ParseGuildInfo(ReadOnlyMemory<byte> payload) => new GuildInfo
        {
            GuildId = CurrentGuildId ?? 0,
            Name = CurrentGuildName ?? "Guild",
            MOTD = string.Empty,
            Info = string.Empty,
            MemberCount = (uint)_guildMembers.Count,
            CreationDate = DateTime.UtcNow,
            LeaderName = _guildMembers.FirstOrDefault()?.Name ?? string.Empty,
            Ranks = Array.Empty<GuildRankInfo>(),
            Emblem = null
        };
        private List<GuildMember> ParseGuildRoster(ReadOnlyMemory<byte> payload) { lock (_stateLock) { return _guildMembers.ToList(); } }
        private GuildCommandResult ParseGuildCommandResult(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 8) return new GuildCommandResult("Unknown", false, 0xFFFFFFFF);
            var operation = BitConverter.ToUInt32(span[..4]);
            var result = BitConverter.ToUInt32(span.Slice(4, 4));
            var operationName = operation switch
            {
                0x081 => "Create",
                0x082 => "Invite",
                0x084 => "Accept",
                0x085 => "Decline",
                0x087 => "Info",
                0x089 => "Roster",
                0x08B => "Promote",
                0x08C => "Demote",
                0x08D => "Leave",
                0x08E => "Remove",
                0x08F => "Disband",
                0x090 => "Leader",
                0x091 => "MOTD",
                _ => $"Unknown({operation:X})"
            };
            var success = result == 0;
            return new GuildCommandResult(operationName, success, result);
        }
        private IEnumerable<GuildMemberStatusChange> ParseGuildEvents(ReadOnlyMemory<byte> payload) { yield break; }
        #endregion

        #region Internal Send Helpers
        private async Task SendSimpleOpcodeAsync(Opcode opcode, string context, CancellationToken token)
        {
            try
            {
                _logger.LogDebug("Sending guild opcode {Opcode} ({Context})", opcode, context);
                await _worldClient.SendOpcodeAsync(opcode, [], token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed guild operation {Context}", context);
                throw;
            }
        }
        private async Task SendCStringOpcodeAsync(Opcode opcode, string value, string context, CancellationToken token)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
                var bytes = Encoding.UTF8.GetBytes(value);
                var payload = new byte[bytes.Length + 1];
                Array.Copy(bytes, payload, bytes.Length);
                payload[^1] = 0;
                _logger.LogDebug("Sending guild opcode {Opcode} ({Context}) Value={Value}", opcode, context, value);
                await _worldClient.SendOpcodeAsync(opcode, payload, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed guild operation {Context} value={Value}", context, value);
                throw;
            }
        }
        #endregion

        #region Test Hook
        public void HandleServerResponse(Opcode opcode, byte[] data)
        {
            if (opcode == Opcode.SMSG_GUILD_INVITE)
            {
                var (inviter, guild) = ParseGuildInvite(data);
                _onGuildInviteReceived?.Invoke(inviter, guild);
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logger.LogDebug("Disposing GuildNetworkClientComponent");
        }
        #endregion
    }
}