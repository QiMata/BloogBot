using System;
using System.Reflection;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using GameData.Core.Enums;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.Abstractions;
using WoWSharpClient.Screens;

namespace WoWSharpClient.Tests.Screens;

public class CharacterSelectScreenTests
{
    [Fact]
    public void RefreshCharacterListFromServer_WithoutAuthenticatedWorldClient_DoesNotMarkPending()
    {
        var wowClient = new WoWClient();
        var screen = new CharacterSelectScreen(wowClient);

        screen.RefreshCharacterListFromServer();

        Assert.False(screen.HasRequestedCharacterList);
        Assert.False(screen.HasReceivedCharacterList);
    }

    [Fact]
    public void RefreshCharacterListFromServer_WithAuthenticatedWorldClient_MarksPending()
    {
        var wowClient = new WoWClient();
        var worldClient = new FakeWorldClient { IsConnected = true, IsAuthenticated = true };
        SetWorldClient(wowClient, worldClient);
        var screen = new CharacterSelectScreen(wowClient);

        screen.RefreshCharacterListFromServer();

        Assert.True(screen.HasRequestedCharacterList);
        Assert.False(screen.HasReceivedCharacterList);
        Assert.NotEqual(default, screen.LastCharacterListRequestUtc);
        Assert.Equal(1, worldClient.CharEnumSendCount);
    }

    [Fact]
    public void MarkCharacterListLoaded_ClearsPendingRequest()
    {
        var wowClient = new WoWClient();
        var screen = new CharacterSelectScreen(wowClient)
        {
            HasRequestedCharacterList = true,
            HasReceivedCharacterList = false,
        };

        screen.MarkCharacterListLoaded();

        Assert.False(screen.HasRequestedCharacterList);
        Assert.True(screen.HasReceivedCharacterList);
    }

    [Fact]
    public void ShouldRetryCharacterListRequest_ReturnsTrueWhenExpired()
    {
        var wowClient = new WoWClient();
        var worldClient = new FakeWorldClient { IsConnected = true, IsAuthenticated = true };
        SetWorldClient(wowClient, worldClient);
        var screen = new CharacterSelectScreen(wowClient);
        screen.RefreshCharacterListFromServer();

        var shouldRetry = screen.ShouldRetryCharacterListRequest(
            TimeSpan.FromSeconds(5),
            screen.LastCharacterListRequestUtc.AddSeconds(6));

        Assert.True(shouldRetry);
    }

    [Fact]
    public void CreateCharacter_IncrementsAttemptCountWhenAuthenticated()
    {
        var wowClient = new WoWClient();
        var worldClient = new FakeWorldClient { IsConnected = true, IsAuthenticated = true };
        SetWorldClient(wowClient, worldClient);
        var screen = new CharacterSelectScreen(wowClient);

        screen.CreateCharacter("Testchar", Race.Human, Gender.Female, Class.Warrior, 0, 0, 0, 0, 0, 0);

        Assert.Equal(1, screen.CharacterCreateAttempts);
        Assert.True(screen.IsCharacterCreationPending);
    }

    [Fact]
    public void HandleCharacterCreateResponse_InProgress_KeepsCreationPendingAndRefreshesCharacterList()
    {
        var wowClient = new WoWClient();
        var worldClient = new FakeWorldClient { IsConnected = true, IsAuthenticated = true };
        SetWorldClient(wowClient, worldClient);
        var screen = new CharacterSelectScreen(wowClient);

        screen.CreateCharacter("Testchar", Race.Human, Gender.Female, Class.Warrior, 0, 0, 0, 0, 0, 0);
        screen.HandleCharacterCreateResponse(CreateCharacterResult.InProgress);

        Assert.True(screen.IsCharacterCreationPending);
        Assert.True(screen.HasRequestedCharacterList);
        Assert.False(screen.HasReceivedCharacterList);
        Assert.Equal(1, worldClient.CharEnumSendCount);
    }

    [Fact]
    public void HandleCharacterCreateResponse_Success_ClearsPendingAndRefreshesCharacterList()
    {
        var wowClient = new WoWClient();
        var worldClient = new FakeWorldClient { IsConnected = true, IsAuthenticated = true };
        SetWorldClient(wowClient, worldClient);
        var screen = new CharacterSelectScreen(wowClient);

        screen.CreateCharacter("Testchar", Race.Human, Gender.Female, Class.Warrior, 0, 0, 0, 0, 0, 0);
        screen.HandleCharacterCreateResponse(CreateCharacterResult.Success);

        Assert.False(screen.IsCharacterCreationPending);
        Assert.True(screen.HasRequestedCharacterList);
        Assert.False(screen.HasReceivedCharacterList);
        Assert.Equal(1, worldClient.CharEnumSendCount);
    }

    [Fact]
    public async Task RefreshCharacterListFromServer_AutoRetriesUntilCharacterListLoads()
    {
        var wowClient = new WoWClient();
        var worldClient = new FakeWorldClient { IsConnected = true, IsAuthenticated = true };
        SetWorldClient(wowClient, worldClient);
        var screen = new CharacterSelectScreen(wowClient);

        var originalDelay = CharacterSelectScreen.CharacterListRetryDelay;
        CharacterSelectScreen.CharacterListRetryDelay = TimeSpan.FromMilliseconds(50);
        try
        {
            screen.RefreshCharacterListFromServer();

            await WaitForConditionAsync(() => worldClient.CharEnumSendCount >= 2, timeoutMs: 1000);

            screen.MarkCharacterListLoaded();
            var sendCountAfterLoad = worldClient.CharEnumSendCount;

            await Task.Delay(150);

            Assert.True(sendCountAfterLoad >= 2);
            Assert.Equal(sendCountAfterLoad, worldClient.CharEnumSendCount);
        }
        finally
        {
            CharacterSelectScreen.CharacterListRetryDelay = originalDelay;
            screen.ResetCharacterListRequest();
        }
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private static void SetWorldClient(WoWClient wowClient, IWorldClient worldClient)
    {
        var field = typeof(WoWClient).GetField("_worldClient", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(wowClient, worldClient);
    }

    private sealed class FakeWorldClient : IWorldClient
    {
        public bool IsConnected { get; set; }
        public bool IsAuthenticated { get; set; }
        public int CharEnumSendCount { get; private set; }

        public event Action<Opcode, int>? PacketSent;
        public event Action<Opcode, int>? PacketReceived;

        public IObservable<Unit> WhenConnected => System.Reactive.Linq.Observable.Empty<Unit>();
        public IObservable<Exception?> WhenDisconnected => System.Reactive.Linq.Observable.Empty<Exception?>();
        public IObservable<Unit> AuthenticationSucceeded => System.Reactive.Linq.Observable.Empty<Unit>();
        public IObservable<byte> AuthenticationFailed => System.Reactive.Linq.Observable.Empty<byte>();
        public IObservable<(ulong Guid, string Name, byte Race, byte Class, byte Gender)> CharacterFound
            => System.Reactive.Linq.Observable.Empty<(ulong Guid, string Name, byte Race, byte Class, byte Gender)>();
        public IObservable<(bool IsAttacking, ulong AttackerGuid, ulong VictimGuid)> AttackStateChanged
            => System.Reactive.Linq.Observable.Empty<(bool IsAttacking, ulong AttackerGuid, ulong VictimGuid)>();
        public IObservable<string> AttackErrors => System.Reactive.Linq.Observable.Empty<string>();
        public IObservable<Unit> LogoutComplete => System.Reactive.Linq.Observable.Empty<Unit>();

        public Task ConnectAsync(string username, string host, byte[] sessionKey, int port = 8085, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
        }

        public IObservable<ReadOnlyMemory<byte>> RegisterOpcodeHandler(Opcode opcode)
            => System.Reactive.Linq.Observable.Empty<ReadOnlyMemory<byte>>();

        public Task SendCharEnumAsync(CancellationToken cancellationToken = default)
        {
            CharEnumSendCount++;
            return Task.CompletedTask;
        }

        public Task SendPingAsync(uint sequence, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendQueryTimeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendPlayerLoginAsync(ulong guid, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendChatMessageAsync(ChatMsg type, Language language, string destinationName, string message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendNameQueryAsync(ulong guid, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendMoveWorldPortAckAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendSetActiveMoverAsync(ulong guid, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendOpcodeAsync(Opcode opcode, byte[] payload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void UpdateEncryptor(IEncryptor newEncryptor)
        {
        }
    }
}
