using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Gossip network client component backed purely by opcode observables (no Subjects/events).
    /// </summary>
    public class GossipNetworkClientComponent : NetworkClientComponent, IGossipNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<GossipNetworkClientComponent> _logger;
        private readonly object _stateLock = new();

        // Internal gossip state
        private bool _isGossipWindowOpen;
        private ulong? _currentNpcGuid;
        private GossipMenuState _menuState = GossipMenuState.Closed;
        private GossipMenuData? _currentMenu;
        private bool _disposed;

        // Opcode backed streams
        private readonly IObservable<GossipMenuData> _gossipMenus;
        private readonly IObservable<GossipMenuData> _gossipMenuOpened;
        private readonly IObservable<GossipMenuData> _gossipMenuClosed;
        private readonly IObservable<GossipOptionData> _selectedOptions;      // not derivable without subjects yet -> Never
        private readonly IObservable<GossipErrorData> _gossipErrors;
        private readonly IObservable<GossipServiceData> _serviceDiscovered;

        public GossipNetworkClientComponent(IWorldClient worldClient, ILogger<GossipNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Base gossip menu stream
            _gossipMenus = SafeOpcodeStream(Opcode.SMSG_GOSSIP_MESSAGE)
                .Select(ParseGossipMenu)
                .Do(menu =>
                {
                    lock (_stateLock)
                    {
                        _currentMenu = menu;
                        _currentNpcGuid = menu.NpcGuid;
                        _isGossipWindowOpen = true;
                        _menuState = GossipMenuState.Open;
                    }
                    _logger.LogDebug("Gossip menu received NPC={NpcGuid:X} Options={OptCount} Quests={QuestCount}", menu.NpcGuid, menu.Options.Count, menu.QuestOptions.Count);
                })
                .Publish()
                .RefCount();

            // Completion stream updates state; emission for closed menus comes from projection
            var completion = SafeOpcodeStream(Opcode.SMSG_GOSSIP_COMPLETE)
                .Do(_ =>
                {
                    lock (_stateLock)
                    {
                        _isGossipWindowOpen = false;
                        _menuState = GossipMenuState.Closed;
                        _currentMenu = null;
                        _currentNpcGuid = null;
                    }
                    _logger.LogDebug("Gossip session complete");
                })
                .Publish()
                .RefCount();

            // NPC text updates modify current menu then re-emit updated snapshot
            var textUpdates = SafeOpcodeStream(Opcode.SMSG_NPC_TEXT_UPDATE)
                .Select(ParseNpcTextUpdate)
                .Do(update =>
                {
                    lock (_stateLock)
                    {
                        if (_currentMenu != null && _currentMenu.NpcGuid == update.NpcGuid)
                        {
                            _currentMenu = new GossipMenuData(_currentMenu.NpcGuid, _currentMenu.MenuId, update.TextId)
                            {
                                Options = _currentMenu.Options,
                                QuestOptions = _currentMenu.QuestOptions,
                                GossipText = update.Text,
                                HasMultiplePages = _currentMenu.HasMultiplePages
                            };
                        }
                    }
                })
                .Select(_ => _currentMenu!)
                .Where(m => m is not null)
                .Publish()
                .RefCount();

            // Merge text updates so consumers see content refresh
            _gossipMenus = _gossipMenus.Merge(textUpdates);

            // Open / close filtered streams
            _gossipMenuOpened = _gossipMenus.Where(_ => _isGossipWindowOpen);
            _gossipMenuClosed = completion.Select(_ => _currentMenu ?? new GossipMenuData(0,0,0));

            // Selected options (not observable from server directly without extra tracking) => Never
            _selectedOptions = Observable.Never<GossipOptionData>();

            // Errors: NPC won't talk + unexpected completion while waiting
            var wontTalk = SafeOpcodeStream(Opcode.SMSG_NPC_WONT_TALK)
                .Select(_ => new GossipErrorData("NPC will not talk", _currentNpcGuid, GossipOperationType.Greet));

            var prematureClose = completion
                .Where(_ => _menuState == GossipMenuState.Waiting) // state prior to completion updated earlier
                .Select(_ => new GossipErrorData("Gossip closed while waiting for option result", _currentNpcGuid, GossipOperationType.SelectOption));

            _gossipErrors = wontTalk.Merge(prematureClose).Publish().RefCount();

            // Service discovery: emit each non-gossip option on every menu snapshot
            _serviceDiscovered = _gossipMenus
                .SelectMany(m => m.Options)
                .Where(o => o.ServiceType != GossipServiceType.Gossip)
                .Select(o => new GossipServiceData(_currentNpcGuid ?? 0, o.ServiceType, o))
                .Publish()
                .RefCount();
        }

        #region IGossipNetworkClientComponent Properties
        public bool IsGossipWindowOpen => _isGossipWindowOpen;
        public ulong? CurrentNpcGuid => _currentNpcGuid;
        public GossipMenuState MenuState => _menuState;
        public IObservable<GossipMenuData> GossipMenus => _gossipMenus;
        public IObservable<GossipOptionData> SelectedOptions => _selectedOptions;
        public IObservable<GossipErrorData> GossipErrors => _gossipErrors;
        public IObservable<GossipMenuData> GossipMenuOpened => _gossipMenuOpened;
        public IObservable<GossipMenuData> GossipMenuClosed => _gossipMenuClosed;
        public IObservable<GossipServiceData> ServiceDiscovered => _serviceDiscovered;
        #endregion

        #region Basic Operations
        public async Task GreetNpcAsync(ulong npcGuid, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            SetOperationInProgress(true);
            try
            {
                lock (_stateLock)
                {
                    _menuState = GossipMenuState.Opening;
                    _currentNpcGuid = npcGuid;
                }
                var payload = new byte[8];
                BitConverter.GetBytes(npcGuid).CopyTo(payload, 0);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);
            }
            finally
            {
                SetOperationInProgress(false);
            }
        }

        public async Task SelectGossipOptionAsync(uint optionIndex, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!_isGossipWindowOpen || _currentNpcGuid == null)
                throw new InvalidOperationException("No gossip window open");
            SetOperationInProgress(true);
            try
            {
                lock (_stateLock) _menuState = GossipMenuState.Waiting;
                var payload = new byte[12];
                BitConverter.GetBytes(_currentNpcGuid.Value).CopyTo(payload, 0);
                BitConverter.GetBytes(optionIndex).CopyTo(payload, 8);
                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_SELECT_OPTION, payload, cancellationToken);
            }
            finally { SetOperationInProgress(false); }
        }

        public async Task QueryNpcTextAsync(uint textId, ulong npcGuid, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var payload = new byte[12];
            BitConverter.GetBytes(textId).CopyTo(payload, 0);
            BitConverter.GetBytes(npcGuid).CopyTo(payload, 4);
            await _worldClient.SendOpcodeAsync(Opcode.CMSG_NPC_TEXT_QUERY, payload, cancellationToken);
        }

        public async Task CloseGossipAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await Task.CompletedTask; // nothing to send; client just updates state
            lock (_stateLock)
            {
                _isGossipWindowOpen = false;
                _currentNpcGuid = null;
                _currentMenu = null;
                _menuState = GossipMenuState.Closed;
            }
        }
        #endregion

        #region Advanced / Quest Operations
        public async Task NavigateToServiceAsync(GossipServiceType serviceType, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var menu = _currentMenu ?? throw new InvalidOperationException("No gossip menu open");
            var opt = menu.Options.FirstOrDefault(o => o.ServiceType == serviceType);
            if (opt == null) throw new InvalidOperationException($"Service {serviceType} not present");
            await SelectGossipOptionAsync(opt.Index, cancellationToken);
        }

        public async Task HandleMultiStepConversationAsync(GossipNavigationStrategy strategy, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            switch (strategy)
            {
                case GossipNavigationStrategy.FindQuests:
                    if (_currentMenu?.HasQuestOptions == true) return; break;
                case GossipNavigationStrategy.FindVendor:
                    await NavigateToServiceAsync(GossipServiceType.Vendor, cancellationToken); break;
                case GossipNavigationStrategy.FindTrainer:
                    await NavigateToServiceAsync(GossipServiceType.Trainer, cancellationToken); break;
                case GossipNavigationStrategy.FindTaxi:
                    await NavigateToServiceAsync(GossipServiceType.Taxi, cancellationToken); break;
                case GossipNavigationStrategy.FindBanker:
                    await NavigateToServiceAsync(GossipServiceType.Banker, cancellationToken); break;
                case GossipNavigationStrategy.FindAnyService:
                    if (_currentMenu != null)
                    {
                        var svc = _currentMenu.Options.FirstOrDefault(o => o.ServiceType != GossipServiceType.Gossip);
                        if (svc != null) await SelectGossipOptionAsync(svc.Index, cancellationToken);
                    }
                    break;
                case GossipNavigationStrategy.ExploreAll:
                    if (_currentMenu != null)
                    {
                        foreach (var o in _currentMenu.Options.Take(3))
                        {
                            try { await SelectGossipOptionAsync(o.Index, cancellationToken); await Task.Delay(300, cancellationToken); }
                            catch { }
                        }
                    }
                    break;
                case GossipNavigationStrategy.Custom:
                    _logger.LogDebug("Custom navigation strategy selected; callers must handle navigation externally");
                    break;
            }
        }

        public async Task<IReadOnlyList<GossipServiceType>> DiscoverAvailableServicesAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_currentMenu == null) return Array.Empty<GossipServiceType>();
            var set = new HashSet<GossipServiceType>();
            foreach (var o in _currentMenu.Options) if (o.ServiceType != GossipServiceType.Gossip) set.Add(o.ServiceType);
            if (_currentMenu.HasQuestOptions) set.Add(GossipServiceType.QuestGiver);
            return await Task.FromResult(set.ToList().AsReadOnly());
        }

        public async Task SelectOptimalQuestRewardAsync(QuestRewardSelectionStrategy strategy, CancellationToken cancellationToken = default)
        {
            // Placeholder � select first reward (index 0)
            await SelectGossipOptionAsync(0, cancellationToken);
        }

        public async Task AcceptAllAvailableQuestsAsync(QuestAcceptanceFilter? filter = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_currentMenu == null) throw new InvalidOperationException("No gossip menu open");
            foreach (var q in _currentMenu.QuestOptions)
            {
                if (q.State != QuestGossipState.Available) continue;
                if (filter != null && !filter.ShouldAcceptQuest(q)) continue;
                try { await SelectGossipOptionAsync(q.Index, cancellationToken); await Task.Delay(100, cancellationToken); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to accept quest {QuestId}", q.QuestId); }
            }
        }

        public async Task<IReadOnlyList<GossipQuestOption>> GetAvailableQuestOptionsAsync()
        {
            ThrowIfDisposed();
            return await Task.FromResult(_currentMenu?.QuestOptions ?? Array.Empty<GossipQuestOption>());
        }
        #endregion

        #region Server Response Compatibility (state only)
        public void HandleGossipMenuReceived(GossipMenuData menuData)
        {
            if (_disposed) return;
            lock (_stateLock)
            {
                _currentMenu = menuData;
                _currentNpcGuid = menuData.NpcGuid;
                _isGossipWindowOpen = true;
                _menuState = GossipMenuState.Open;
            }
        }
        public void HandleGossipOptionResult(GossipOptionResult result) { /* no-op (opcode driven streams) */ }
        public void HandleNpcTextUpdate(string npcText, uint textId)
        {
            if (_disposed) return;
            lock (_stateLock)
            {
                if (_currentMenu != null)
                {
                    _currentMenu = new GossipMenuData(_currentMenu.NpcGuid, _currentMenu.MenuId, textId)
                    {
                        Options = _currentMenu.Options,
                        QuestOptions = _currentMenu.QuestOptions,
                        GossipText = npcText,
                        HasMultiplePages = _currentMenu.HasMultiplePages
                    };
                }
            }
        }
        public void HandleGossipSessionComplete()
        {
            if (_disposed) return;
            lock (_stateLock)
            {
                _isGossipWindowOpen = false;
                _menuState = GossipMenuState.Closed;
                _currentNpcGuid = null;
                _currentMenu = null;
            }
        }
        public void HandleGossipError(string errorMessage, ulong? npcGuid = null)
        {
            if (_disposed) return;
            _logger.LogError("Gossip error: {Error} (NPC={NpcGuid:X})", errorMessage, npcGuid ?? _currentNpcGuid ?? 0);
            lock (_stateLock) _menuState = GossipMenuState.Error;
        }
        #endregion

        #region Validation / Helpers
        public bool CanPerformGossipOperation(GossipOperationType operationType)
        {
            return operationType switch
            {
                GossipOperationType.Greet => !_isGossipWindowOpen,
                GossipOperationType.SelectOption => _isGossipWindowOpen && _menuState == GossipMenuState.Open,
                GossipOperationType.QueryText => _isGossipWindowOpen,
                GossipOperationType.Close => _isGossipWindowOpen,
                GossipOperationType.NavigateToService => _isGossipWindowOpen && _currentMenu != null,
                GossipOperationType.AcceptQuest => _isGossipWindowOpen && _currentMenu?.HasQuestOptions == true,
                GossipOperationType.SelectQuestReward => _isGossipWindowOpen,
                _ => false
            };
        }
        public GossipMenuData? GetCurrentGossipMenu() => _currentMenu;
        public bool IsServiceAvailable(GossipServiceType serviceType)
        {
            if (!_isGossipWindowOpen || _currentMenu == null) return false;
            return _currentMenu.Options.Any(o => o.ServiceType == serviceType) || (serviceType == GossipServiceType.QuestGiver && _currentMenu.HasQuestOptions);
        }

        private IObservable<ReadOnlyMemory<byte>> SafeOpcodeStream(Opcode opcode)
            => _worldClient.RegisterOpcodeHandler(opcode) ?? Observable.Empty<ReadOnlyMemory<byte>>();

        private GossipMenuData ParseGossipMenu(ReadOnlyMemory<byte> payload)
        {
            // SMSG_GOSSIP_MESSAGE format (MaNGOS 1.12.1):
            // ObjectGuid npcGuid (8)
            // uint32 textId (4)
            // uint32 menuId (4)
            // uint32 gossipOptionsCount (4)
            // [per gossip option]: uint32 index (4), uint8 icon (1), uint8 coded (1),
            //                      uint32 boxMoney (4), string text (null-term), string boxText (null-term)
            // uint32 questOptionsCount (4)
            // [per quest option]: uint32 questId (4), uint32 icon (4), uint32 questLevel (4), string title (null-term)
            var span = payload.Span;
            if (span.Length < 20)
            {
                ulong g = span.Length >= 8 ? BinaryPrimitives.ReadUInt64LittleEndian(span) : 0UL;
                return new GossipMenuData(g, 0, 0);
            }

            int offset = 0;
            ulong npcGuid = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)); offset += 8;
            uint textId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
            uint menuId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
            uint gossipCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;

            var gossipOptions = new List<GossipOptionData>();
            for (uint i = 0; i < gossipCount && offset + 10 <= span.Length; i++)
            {
                uint optIndex = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                byte icon = span[offset++];
                byte coded = span[offset++];
                uint boxMoney = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                string message = ReadCString(span, ref offset);
                string boxText = ReadCString(span, ref offset);

                var gossipType = icon <= 10 ? (GossipTypes)icon : GossipTypes.Gossip;
                gossipOptions.Add(new GossipOptionData(optIndex, message, gossipType)
                {
                    RequiresPayment = coded != 0,
                    Cost = boxMoney
                });
            }

            var questOptions = new List<GossipQuestOption>();
            if (offset + 4 <= span.Length)
            {
                uint questCount = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                for (uint i = 0; i < questCount && offset + 12 <= span.Length; i++)
                {
                    uint questId = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                    uint questIcon = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                    uint questLevel = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)); offset += 4;
                    string questTitle = ReadCString(span, ref offset);

                    var state = questIcon switch
                    {
                        0 or 2 => QuestGossipState.Available,
                        1 or 3 => QuestGossipState.InProgress,
                        4 => QuestGossipState.Completable,
                        _ => QuestGossipState.Available
                    };
                    questOptions.Add(new GossipQuestOption(questId, questTitle, questLevel, state, i));
                }
            }

            return new GossipMenuData(npcGuid, menuId, textId)
            {
                Options = gossipOptions,
                QuestOptions = questOptions,
                GossipText = string.Empty,
                HasMultiplePages = false
            };
        }

        private (ulong NpcGuid, uint TextId, string Text) ParseNpcTextUpdate(ReadOnlyMemory<byte> payload)
        {
            // SMSG_NPC_TEXT_UPDATE format (MaNGOS 1.12.1):
            // textId(4) + 8 × [probability(float4) + text0\0 + text1\0 + lang(4) + 3×[emoteDelay(4)+emote(4)]]
            // No GUID in this packet — NPC context comes from the active gossip session.
            var span = payload.Span;
            uint textId = span.Length >= 4 ? BinaryPrimitives.ReadUInt32LittleEndian(span[..4]) : 0U;
            ulong npcGuid = _currentNpcGuid ?? 0UL;

            // Parse first text variant: skip probability(4), read text0
            int offset = 4;
            string text = $"NPC Text {textId}";
            if (offset + 4 <= span.Length)
            {
                offset += 4; // skip probability float
                if (offset < span.Length)
                    text = ReadCString(span, ref offset);
            }
            return (npcGuid, textId, text);
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

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}