using System.Text;
using System.Reactive.Linq;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

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

        /// <summary>
        /// Parses SMSG_GUILD_INVITE: inviterName\0 + guildName\0
        /// </summary>
        private (string Inviter, string GuildName) ParseGuildInvite(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            int offset = 0;
            var inviter = ReadCString(span, ref offset);
            var guild = ReadCString(span, ref offset);
            return (inviter, guild);
        }

        /// <summary>
        /// Overload for byte[] used by HandleServerResponse test hook.
        /// </summary>
        private (string Inviter, string GuildName) ParseGuildInvite(byte[] payload)
            => ParseGuildInvite((ReadOnlyMemory<byte>)payload);

        /// <summary>
        /// Parses SMSG_GUILD_INFO: guildName\0 + day(4) + month(4) + year(4) + memberCount(4) + accountCount(4)
        /// </summary>
        private GuildInfo ParseGuildInfo(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            int offset = 0;
            var name = ReadCString(span, ref offset);

            uint day = 0, month = 0, year = 0, memberCount = 0;
            if (offset + 4 <= span.Length) { day = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4; }
            if (offset + 4 <= span.Length) { month = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4; }
            if (offset + 4 <= span.Length) { year = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4; }
            if (offset + 4 <= span.Length) { memberCount = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4; }
            // accountCount follows but we don't store it

            DateTime creationDate;
            try { creationDate = new DateTime((int)year, (int)month + 1, (int)day + 1, 0, 0, 0, DateTimeKind.Utc); }
            catch { creationDate = DateTime.UnixEpoch; }

            return new GuildInfo
            {
                GuildId = CurrentGuildId ?? 0,
                Name = name,
                MOTD = string.Empty,
                Info = string.Empty,
                MemberCount = memberCount,
                CreationDate = creationDate,
                LeaderName = string.Empty,
                Ranks = Array.Empty<GuildRankInfo>(),
                Emblem = null
            };
        }

        /// <summary>
        /// Parses SMSG_GUILD_ROSTER:
        ///   memberCount(4) + motd\0 + guildinfo\0 + rankCount(4)
        ///   + [rankRights(4)] × rankCount
        ///   + [member entries] × memberCount
        /// Each member entry:
        ///   guid(8) + status(1) + name\0 + rankId(4) + level(1) + class(1) + zoneId(4)
        ///   + [if offline: logoutTime(float)] + publicNote\0 + officerNote\0
        /// </summary>
        private List<GuildMember> ParseGuildRoster(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            int offset = 0;
            var members = new List<GuildMember>();

            if (span.Length < 4) return members;
            uint memberCount = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;

            var motd = ReadCString(span, ref offset);
            var guildInfoText = ReadCString(span, ref offset);

            if (offset + 4 > span.Length) return members;
            uint rankCount = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;

            // Read rank rights (we store them for the GuildInfo cache)
            var rankRights = new uint[rankCount];
            for (int i = 0; i < rankCount && offset + 4 <= span.Length; i++)
            {
                rankRights[i] = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;
            }

            // Read member entries
            for (int i = 0; i < memberCount && offset < span.Length; i++)
            {
                if (offset + 8 > span.Length) break;
                ulong guid = BitConverter.ToUInt64(span.Slice(offset, 8)); offset += 8;

                if (offset + 1 > span.Length) break;
                byte status = span[offset]; offset += 1;
                bool isOnline = (status & 1) != 0;

                string name = ReadCString(span, ref offset);

                if (offset + 4 > span.Length) break;
                uint rankId = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;

                if (offset + 2 > span.Length) break;
                byte level = span[offset]; offset += 1;
                byte classId = span[offset]; offset += 1;

                if (offset + 4 > span.Length) break;
                uint zoneId = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;

                DateTime? lastLogon = null;
                if (!isOnline)
                {
                    if (offset + 4 > span.Length) break;
                    float logoutTime = BitConverter.ToSingle(span.Slice(offset, 4)); offset += 4;
                    lastLogon = DateTime.UtcNow.AddSeconds(-logoutTime);
                }

                string publicNote = ReadCString(span, ref offset);
                string officerNote = ReadCString(span, ref offset);

                members.Add(new GuildMember
                {
                    Guid = guid,
                    Name = name,
                    Rank = rankId,
                    Class = classId,
                    Level = level,
                    Zone = zoneId.ToString(),
                    IsOnline = isOnline,
                    Status = isOnline ? GuildMemberStatus.Online : GuildMemberStatus.Offline,
                    PublicNote = publicNote,
                    OfficerNote = officerNote,
                    LastLogon = lastLogon
                });
            }

            // Update cached guild info with MOTD and info text from roster
            lock (_stateLock)
            {
                if (_guildInfo != null)
                {
                    _guildInfo.MOTD = motd;
                    _guildInfo.Info = guildInfoText;
                }
            }

            return members;
        }

        /// <summary>
        /// Parses SMSG_GUILD_COMMAND_RESULT: commandType(4) + name\0 + errorCode(4)
        /// MaNGOS command types: CREATE_S=0, INVITE_S=1, QUIT_S=3, FOUNDER_S=0x0E
        /// </summary>
        private GuildCommandResult ParseGuildCommandResult(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            int offset = 0;

            if (span.Length < 4) return new GuildCommandResult("Unknown", false, 0xFFFFFFFF);
            uint commandType = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4;

            // Skip the name CString (not used in result but must be read to reach error code)
            ReadCString(span, ref offset);

            uint errorCode = 0;
            if (offset + 4 <= span.Length) { errorCode = BitConverter.ToUInt32(span.Slice(offset, 4)); offset += 4; }

            var operationName = commandType switch
            {
                0x00 => "Create",
                0x01 => "Invite",
                0x03 => "Quit",
                0x0E => "Founder",
                _ => $"Unknown({commandType:X})"
            };

            return new GuildCommandResult(operationName, errorCode == 0, errorCode);
        }

        /// <summary>
        /// Parses SMSG_GUILD_EVENT: eventType(1) + strCount(1) + [strings\0] + [optional guid(8)]
        /// GE_SIGNED_ON = 0x0C, GE_SIGNED_OFF = 0x0D
        /// </summary>
        private IEnumerable<GuildMemberStatusChange> ParseGuildEvents(ReadOnlyMemory<byte> payload)
        {
            var span = payload.Span;
            if (span.Length < 2) yield break;

            int offset = 0;
            byte eventType = span[offset]; offset += 1;
            byte strCount = span[offset]; offset += 1;

            var strings = new List<string>();
            for (int i = 0; i < strCount && offset < span.Length; i++)
                strings.Add(ReadCString(span, ref offset));

            // GE_SIGNED_ON = 0x0C, GE_SIGNED_OFF = 0x0D
            if (eventType == 0x0C && strings.Count > 0)
                yield return new GuildMemberStatusChange(strings[0], true);
            else if (eventType == 0x0D && strings.Count > 0)
                yield return new GuildMemberStatusChange(strings[0], false);
        }

        private static string ReadCString(ReadOnlySpan<byte> span, ref int offset)
        {
            int start = offset;
            while (offset < span.Length && span[offset] != 0)
                offset++;
            var result = offset > start ? Encoding.UTF8.GetString(span.Slice(start, offset - start)) : string.Empty;
            if (offset < span.Length) offset++; // skip null terminator
            return result;
        }
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