using System.Text;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of guild network agent that handles guild-related operations in World of Warcraft.
    /// Manages guild invites, guild bank interactions, member management, and guild settings using the Mangos protocol.
    /// </summary>
    public class GuildNetworkClientComponent : IGuildNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<GuildNetworkClientComponent> _logger;

        private bool _isInGuild;
        private uint? _currentGuildId;
        private bool _isGuildWindowOpen;
        private bool _isGuildBankWindowOpen;
        private ulong? _currentGuildBankGuid;
        private uint? _currentGuildRank;

        /// <summary>
        /// Initializes a new instance of the GuildNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public GuildNetworkClientComponent(IWorldClient worldClient, ILogger<GuildNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public event Action<string, string>? OnGuildOperationFailed;

        /// <inheritdoc />
        public event Action<string, string>? GuildInviteReceived;

        /// <inheritdoc />
        public event Action<uint, string>? GuildJoined;

        /// <inheritdoc />
        public event Action<uint, string>? GuildLeft;

        /// <inheritdoc />
        public event Action<string>? GuildMemberOnline;

        /// <inheritdoc />
        public event Action<string>? GuildMemberOffline;

        /// <inheritdoc />
        public event Action<uint>? GuildRosterReceived;

        /// <inheritdoc />
        public event Action<uint, string, string>? GuildInfoReceived;

        /// <inheritdoc />
        public event Action<string>? GuildMOTDReceived;

        /// <inheritdoc />
        public event Action<ulong>? GuildBankWindowOpened;

        /// <inheritdoc />
        public event Action? GuildBankWindowClosed;

        /// <inheritdoc />
        public event Action<uint, uint, byte, byte>? ItemDepositedToGuildBank;

        /// <inheritdoc />
        public event Action<uint, uint, byte, byte>? ItemWithdrawnFromGuildBank;

        /// <inheritdoc />
        public event Action<uint>? MoneyDepositedToGuildBank;

        /// <inheritdoc />
        public event Action<uint>? MoneyWithdrawnFromGuildBank;

        /// <inheritdoc />
        public event Action<string, string>? GuildOperationFailed;

        /// <inheritdoc />
        public bool IsInGuild { get; private set; }

        /// <inheritdoc />
        public uint? CurrentGuildId { get; private set; }

        /// <inheritdoc />
        public string? CurrentGuildName { get; private set; }

        /// <inheritdoc />
        public bool IsGuildWindowOpen { get; private set; }

        /// <inheritdoc />
        public bool IsGuildBankWindowOpen { get; private set; }

        /// <summary>
        /// Backward compatibility property that maps to GuildInviteReceived event.
        /// </summary>
        public event Action<string, string>? OnGuildInviteReceived
        {
            add => GuildInviteReceived += value;
            remove => GuildInviteReceived -= value;
        }

        /// <inheritdoc />
        public async Task AcceptGuildInviteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Accepting guild invite");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_ACCEPT, [], cancellationToken);

                _logger.LogInformation("Guild invite acceptance sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept guild invite");
                GuildOperationFailed?.Invoke("AcceptGuildInvite", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DeclineGuildInviteAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Declining guild invite");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_DECLINE, [], cancellationToken);

                _logger.LogInformation("Guild invite decline sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decline guild invite");
                GuildOperationFailed?.Invoke("DeclineGuildInvite", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task InvitePlayerToGuildAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

                _logger.LogDebug("Inviting player to guild: {PlayerName}", playerName);

                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[nameBytes.Length] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_INVITE, payload, cancellationToken);

                _logger.LogInformation("Guild invite sent to player: {PlayerName}", playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to invite player to guild: {PlayerName}", playerName);
                GuildOperationFailed?.Invoke("InvitePlayerToGuild", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RemovePlayerFromGuildAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

                _logger.LogDebug("Removing player from guild: {PlayerName}", playerName);

                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[nameBytes.Length] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_REMOVE, payload, cancellationToken);

                _logger.LogInformation("Guild removal sent for player: {PlayerName}", playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove player from guild: {PlayerName}", playerName);
                GuildOperationFailed?.Invoke("RemovePlayerFromGuild", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task PromoteGuildMemberAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

                _logger.LogDebug("Promoting guild member: {PlayerName}", playerName);

                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[nameBytes.Length] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_PROMOTE, payload, cancellationToken);

                _logger.LogInformation("Guild promotion sent for player: {PlayerName}", playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to promote guild member: {PlayerName}", playerName);
                GuildOperationFailed?.Invoke("PromoteGuildMember", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DemoteGuildMemberAsync(string playerName, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);

                _logger.LogDebug("Demoting guild member: {PlayerName}", playerName);

                var nameBytes = Encoding.UTF8.GetBytes(playerName);
                var payload = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, payload, nameBytes.Length);
                payload[nameBytes.Length] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_DEMOTE, payload, cancellationToken);

                _logger.LogInformation("Guild demotion sent for player: {PlayerName}", playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to demote guild member: {PlayerName}", playerName);
                GuildOperationFailed?.Invoke("DemoteGuildMember", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LeaveGuildAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Leaving guild");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_LEAVE, [], cancellationToken);

                _logger.LogInformation("Guild leave command sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to leave guild");
                GuildOperationFailed?.Invoke("LeaveGuild", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DisbandGuildAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Disbanding guild");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_DISBAND, [], cancellationToken);

                _logger.LogInformation("Guild disband command sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disband guild");
                GuildOperationFailed?.Invoke("DisbandGuild", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SetGuildMOTDAsync(string motd, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(motd);

                _logger.LogDebug("Setting guild MOTD: {MOTD}", motd);

                var motdBytes = Encoding.UTF8.GetBytes(motd);
                var payload = new byte[motdBytes.Length + 1];
                Array.Copy(motdBytes, payload, motdBytes.Length);
                payload[motdBytes.Length] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_MOTD, payload, cancellationToken);

                _logger.LogInformation("Guild MOTD set: {MOTD}", motd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set guild MOTD: {MOTD}", motd);
                GuildOperationFailed?.Invoke("SetGuildMOTD", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task SetGuildInfoAsync(string guildInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Setting guild info: {GuildInfo}", guildInfo);

                var payload = System.Text.Encoding.UTF8.GetBytes(guildInfo + "\0");
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_INFO_TEXT, payload, cancellationToken);

                _logger.LogInformation("Guild info set successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set guild info");
                GuildOperationFailed?.Invoke("SetGuildInfo", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CreateGuildAsync(string guildName, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Creating guild: {GuildName}", guildName);

                var payload = System.Text.Encoding.UTF8.GetBytes(guildName + "\0");
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_CREATE, payload, cancellationToken);

                _logger.LogInformation("Guild creation request sent for: {GuildName}", guildName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create guild: {GuildName}", guildName);
                GuildOperationFailed?.Invoke("CreateGuild", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task QuickGuildBankOperationAsync(ulong guildBankGuid, Func<Task> operation, CancellationToken cancellationToken = default)
        {
            await OpenGuildBankAsync(guildBankGuid, cancellationToken);
            await operation();
            await CloseGuildBankAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task RequestGuildRosterAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting guild roster");

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_ROSTER, [], cancellationToken);

                _logger.LogInformation("Guild roster request sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request guild roster");
                GuildOperationFailed?.Invoke("RequestGuildRoster", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RequestGuildInfoAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GUILD_INFO, [], cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request guild info");
                GuildOperationFailed?.Invoke("RequestGuildInfo", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task OpenGuildBankAsync(ulong guildBankGuid, CancellationToken cancellationToken = default)
        {
            // Guild bank operations are not supported in this client version
            _logger.LogWarning("Guild bank operations are not supported in this client version");
            GuildOperationFailed?.Invoke("OpenGuildBank", "Guild bank operations are not supported in this client version");
        }

        /// <inheritdoc />
        public async Task CloseGuildBankAsync(CancellationToken cancellationToken = default)
        {
            // Guild bank operations are not supported in this client version
            _logger.LogWarning("Guild bank operations are not supported in this client version");
            GuildOperationFailed?.Invoke("CloseGuildBank", "Guild bank operations are not supported in this client version");
        }

        /// <inheritdoc />
        public async Task DepositItemToGuildBankAsync(byte sourceBagId, byte sourceSlotId, byte destTabIndex, byte destSlotIndex, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            // Guild bank operations are not supported in this client version
            _logger.LogWarning("Guild bank operations are not supported in this client version");
            GuildOperationFailed?.Invoke("DepositItemToGuildBank", "Guild bank operations are not supported in this client version");
        }

        /// <inheritdoc />
        public async Task WithdrawItemFromGuildBankAsync(byte sourceTabIndex, byte sourceSlotIndex, uint quantity = 1, CancellationToken cancellationToken = default)
        {
            // Guild bank operations are not supported in this client version
            _logger.LogWarning("Guild bank operations are not supported in this client version");
            GuildOperationFailed?.Invoke("WithdrawItemFromGuildBank", "Guild bank operations are not supported in this client version");
        }

        /// <inheritdoc />
        public async Task DepositMoneyToGuildBankAsync(uint amount, CancellationToken cancellationToken = default)
        {
            // Guild bank operations are not supported in this client version
            _logger.LogWarning("Guild bank operations are not supported in this client version");
            GuildOperationFailed?.Invoke("DepositMoneyToGuildBank", "Guild bank operations are not supported in this client version");
        }

        /// <inheritdoc />
        public async Task WithdrawMoneyFromGuildBankAsync(uint amount, CancellationToken cancellationToken = default)
        {
            // Guild bank operations are not supported in this client version
            _logger.LogWarning("Guild bank operations are not supported in this client version");
            GuildOperationFailed?.Invoke("WithdrawMoneyFromGuildBank", "Guild bank operations are not supported in this client version");
        }

        /// <inheritdoc />
        public async Task QueryGuildBankTabAsync(byte tabIndex, CancellationToken cancellationToken = default)
        {
            // Guild bank operations are not supported in this client version
            _logger.LogWarning("Guild bank operations are not supported in this client version");
            GuildOperationFailed?.Invoke("QueryGuildBankTab", "Guild bank operations are not supported in this client version");
        }

        /// <inheritdoc />
        public async Task SetGuildMemberNoteAsync(string playerName, string note, bool isOfficerNote = false, CancellationToken cancellationToken = default)
        {
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(playerName);
                ArgumentNullException.ThrowIfNull(note);

                _logger.LogDebug("Setting guild member note for {PlayerName}: {Note} (Officer: {IsOfficer})", playerName, note, isOfficerNote);

                var opcode = isOfficerNote ? Opcode.CMSG_GUILD_SET_OFFICER_NOTE : Opcode.CMSG_GUILD_SET_PUBLIC_NOTE;
                
                var playerNameBytes = Encoding.UTF8.GetBytes(playerName);
                var noteBytes = Encoding.UTF8.GetBytes(note);
                var payload = new byte[playerNameBytes.Length + 1 + noteBytes.Length + 1];

                var offset = 0;
                Array.Copy(playerNameBytes, 0, payload, offset, playerNameBytes.Length);
                offset += playerNameBytes.Length;
                payload[offset++] = 0; // Null terminator

                Array.Copy(noteBytes, 0, payload, offset, noteBytes.Length);
                offset += noteBytes.Length;
                payload[offset] = 0; // Null terminator

                await _worldClient.SendOpcodeAsync(opcode, payload, cancellationToken);

                _logger.LogInformation("Guild member note set for {PlayerName}", playerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set guild member note for {PlayerName}", playerName);
                GuildOperationFailed?.Invoke("SetGuildMemberNote", ex.Message);
                throw;
            }
        }

        /// <inheritdoc />
        public bool IsGuildBankOpen(ulong guildBankGuid)
        {
            // Since guild bank operations are not supported, always return false
            return false;
        }

        /// <inheritdoc />
        public uint? GetCurrentGuildRank()
        {
            // Guild rank tracking not implemented in this version
            return null;
        }

        /// <inheritdoc />
        public bool HasGuildPermission(uint permission)
        {
            // Guild permission system not implemented in this version
            return false;
        }

        #region Server Response Handling

        /// <summary>
        /// Handles server responses for guild-related packets.
        /// </summary>
        /// <param name="opcode">The opcode of the received packet.</param>
        /// <param name="data">The packet data.</param>
        public void HandleServerResponse(Opcode opcode, byte[] data)
        {
            try
            {
                switch (opcode)
                {
                    case Opcode.SMSG_GUILD_INVITE:
                        HandleGuildInvite(data);
                        break;
                    case Opcode.SMSG_GUILD_INFO:
                        HandleGuildInfo(data);
                        break;
                    case Opcode.SMSG_GUILD_ROSTER:
                        HandleGuildRoster(data);
                        break;
                    case Opcode.SMSG_GUILD_EVENT:
                        HandleGuildEvent(data);
                        break;
                    case Opcode.SMSG_GUILD_COMMAND_RESULT:
                        HandleGuildCommandResult(data);
                        break;
                    default:
                        _logger.LogDebug("Unhandled guild-related opcode: {Opcode}", opcode);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling guild server response for opcode: {Opcode}", opcode);
            }
        }

        private void HandleGuildInvite(byte[] data)
        {
            try
            {
                if (data.Length < 16)
                {
                    _logger.LogWarning("Invalid guild invite data length: {Length}", data.Length);
                    return;
                }

                // Parse guild invite data (simplified)
                var inviterName = "Unknown"; // Would parse from data
                var guildName = "Unknown"; // Would parse from data

                _logger.LogInformation("Guild invite received from {Inviter} for guild {Guild}", inviterName, guildName);
                GuildInviteReceived?.Invoke(inviterName, guildName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling guild invite");
            }
        }

        private void HandleGuildInfo(byte[] data)
        {
            try
            {
                // Parse guild info data
                _logger.LogDebug("Guild info received");
                // GuildInfoReceived?.Invoke(guildId, guildName, guildInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling guild info");
            }
        }

        private void HandleGuildRoster(byte[] data)
        {
            try
            {
                // Parse guild roster data
                _logger.LogDebug("Guild roster received");
                // GuildRosterReceived?.Invoke(memberCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling guild roster");
            }
        }

        private void HandleGuildEvent(byte[] data)
        {
            try
            {
                // Parse guild event data
                _logger.LogDebug("Guild event received");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling guild event");
            }
        }

        private void HandleGuildCommandResult(byte[] data)
        {
            try
            {
                if (data.Length < 8)
                {
                    _logger.LogWarning("Invalid guild command result data length: {Length}", data.Length);
                    return;
                }

                var operation = BitConverter.ToUInt32(data, 0);
                var result = BitConverter.ToUInt32(data, 4);

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

                if (result == 0)
                {
                    _logger.LogInformation("Guild operation {Operation} succeeded", operationName);
                }
                else
                {
                    _logger.LogWarning("Guild operation {Operation} failed with result: {Result}", operationName, result);
                    GuildOperationFailed?.Invoke(operationName, $"Operation failed with result: {result}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling guild command result");
            }
        }

        #endregion
    }
}