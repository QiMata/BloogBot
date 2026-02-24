using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using WoWSharpClient;

namespace WoWSharpClient.Tests
{
    public class WoWSharpEventEmitterTests : IDisposable
    {
        private readonly WoWSharpEventEmitter _emitter;

        public WoWSharpEventEmitterTests()
        {
            _emitter = WoWSharpEventEmitter.Instance;
            _emitter.Reset(); // Clean state for each test
        }

        public void Dispose()
        {
            _emitter.Reset();
        }

        // ======== Singleton ========

        [Fact]
        public void Instance_ReturnsSameObject()
        {
            var a = WoWSharpEventEmitter.Instance;
            var b = WoWSharpEventEmitter.Instance;
            Assert.Same(a, b);
        }

        // ======== Reset ========

        [Fact]
        public void Reset_ClearsEventHandlers()
        {
            bool fired = false;
            _emitter.OnLoginConnect += (s, e) => fired = true;

            _emitter.Reset();
            _emitter.FireOnLoginConnect();

            Assert.False(fired);
        }

        [Fact]
        public void Reset_ClearsMultipleEvents()
        {
            int count = 0;
            _emitter.OnLoginConnect += (s, e) => count++;
            _emitter.OnDisconnect += (s, e) => count++;
            _emitter.OnDeath += (s, e) => count++;

            _emitter.Reset();
            _emitter.FireOnLoginConnect();
            _emitter.FireOnDisconnect();
            _emitter.FireOnDeath();

            Assert.Equal(0, count);
        }

        // ======== Fire methods - no subscribers (should not throw) ========

        [Fact]
        public void Fire_NoSubscribers_DoesNotThrow()
        {
            // All fire methods should be safe with no subscribers
            _emitter.FireOnLoginConnect();
            _emitter.FireOnLoginSuccess();
            _emitter.FireOnLoginFailure();
            _emitter.FireOnHandshakeBegin();
            _emitter.FireOnChooseRealm();
            _emitter.FireInServerQueue();
            _emitter.FireOnWorldSessionStart();
            _emitter.FireOnWorldSessionEnd();
            _emitter.FireOnCharacterListLoaded();
            _emitter.FireOnDisconnect();
            _emitter.FireLevelUp();
            _emitter.FireOnFightStart();
            _emitter.FireOnFightStop();
            _emitter.FireOnUnitKilled();
            _emitter.FireOnDeath();
            _emitter.FireOnResurrect();
            _emitter.FireOnCorpseInRange();
            _emitter.FireOnCorpseOutOfRange();
            _emitter.FireOnLootOpened();
            _emitter.FireOnLootClosed();
            _emitter.FireOnGossipShow();
            _emitter.FireOnGossipClosed();
            _emitter.FireOnMerchantShow();
            _emitter.FireOnMerchantClosed();
            _emitter.FireOnTaxiShow();
            _emitter.FireOnTaxiClosed();
            _emitter.FireOnTrainerShow();
            _emitter.FireOnTrainerClosed();
            _emitter.FireOnPlayerInit();
            _emitter.FireOnInitialSpellsLoaded();
            _emitter.FireOnTradeShow();
            _emitter.FireOnMoneyChange();
            _emitter.FireOnTargetChange();
            _emitter.FireOnQuestComplete();
            _emitter.FireOnQuestObjectiveComplete();
            _emitter.FireOnQuestFrameOpen();
            _emitter.FireOnQuestFrameClosed();
            _emitter.FireOnQuestGreetingFrameOpen();
            _emitter.FireOnQuestGreetingFrameClosed();
            _emitter.FireOnQuestFailed();
            _emitter.FireOnQuestProgress();
            _emitter.FireOnMailboxOpen();
            _emitter.FireOnMailboxClosed();
            _emitter.FireOnBankFrameOpen();
            _emitter.FireOnBankFrameClosed();
            _emitter.FireOnSetRestStart();
            _emitter.FireOnBlockParryDodge();
            _emitter.FireOnParry();
            _emitter.FireOnSlamReady();
        }

        // ======== Parameterized Fire methods - no subscribers ========

        [Fact]
        public void FireWithArgs_NoSubscribers_DoesNotThrow()
        {
            _emitter.FireOnLoot(1234, "Test Item", 1);
            _emitter.FireOnErrorMessage("error");
            _emitter.FireOnUiMessage("ui");
            _emitter.FireOnSystemMessage("system");
            _emitter.FireOnSkillMessage("skill");
            _emitter.FireOnPartyInvite("Player");
            _emitter.FireOnXpGain(100);
            _emitter.FireOnLootMoney(5000);
            _emitter.FireAuraChanged("player");
            _emitter.FireOnDuelRequest("Dueler");
            _emitter.FireOnGuildInvite("Inviter", "MyGuild");
            _emitter.FireOnEvent("TEST_EVENT", []);
            _emitter.FireOnCtm(new Position(0, 0, 0), 1);
            _emitter.FireOnLoginVerifyWorld(new WorldInfo());
            _emitter.FireOnStandStateUpdate(0);
            _emitter.FireOnCharacterCreateResponse(new CharCreateResponse(CreateCharacterResult.Success));
            _emitter.FireOnCharacterDeleteResponse(new CharDeleteResponse(DeleteCharacterResult.Success));
            _emitter.FireOnSpellGo(1, 1, 1);
            _emitter.FireOnSpellStart(1, 1, 1, 1000);
            _emitter.FireOnSpellLogMiss(1, 1, 1, 0);
            _emitter.FireOnAttackerStateUpdate(0, 1, 2, 100, 0, 0);
        }

        // ======== Event subscription and firing ========

        [Fact]
        public void OnLoginConnect_Fires()
        {
            bool fired = false;
            _emitter.OnLoginConnect += (s, e) => fired = true;
            _emitter.FireOnLoginConnect();
            Assert.True(fired);
        }

        [Fact]
        public void OnDisconnect_Fires()
        {
            bool fired = false;
            _emitter.OnDisconnect += (s, e) => fired = true;
            _emitter.FireOnDisconnect();
            Assert.True(fired);
        }

        [Fact]
        public void OnDeath_Fires()
        {
            bool fired = false;
            _emitter.OnDeath += (s, e) => fired = true;
            _emitter.FireOnDeath();
            Assert.True(fired);
        }

        [Fact]
        public void LevelUp_Fires()
        {
            bool fired = false;
            _emitter.LevelUp += (s, e) => fired = true;
            _emitter.FireLevelUp();
            Assert.True(fired);
        }

        [Fact]
        public void OnXpGain_PassesAmount()
        {
            int received = 0;
            _emitter.OnXpGain += (s, e) => received = e.Xp;
            _emitter.FireOnXpGain(150);
            Assert.Equal(150, received);
        }

        [Fact]
        public void OnLootMoney_PassesAmount()
        {
            int received = 0;
            _emitter.OnLootMoney += (s, e) => received = e.CopperAmount;
            _emitter.FireOnLootMoney(9999);
            Assert.Equal(9999, received);
        }

        [Fact]
        public void OnLoot_PassesItemData()
        {
            int itemId = 0;
            string itemName = null;
            int count = 0;
            _emitter.OnLoot += (s, e) =>
            {
                itemId = e.ItemId;
                itemName = e.ItemName;
                count = e.Count;
            };
            _emitter.FireOnLoot(12345, "Linen Cloth", 3);
            Assert.Equal(12345, itemId);
            Assert.Equal("Linen Cloth", itemName);
            Assert.Equal(3, count);
        }

        [Fact]
        public void OnPartyInvite_PassesPlayerName()
        {
            string player = null;
            _emitter.OnPartyInvite += (s, e) => player = e.Player;
            _emitter.FireOnPartyInvite("Thrall");
            Assert.Equal("Thrall", player);
        }

        [Fact]
        public void OnErrorMessage_PassesMessage()
        {
            string msg = null;
            _emitter.OnErrorMessage += (s, e) => msg = e.Message;
            _emitter.FireOnErrorMessage("Not enough mana");
            Assert.Equal("Not enough mana", msg);
        }

        [Fact]
        public void OnGuildInvite_Fires()
        {
            bool fired = false;
            _emitter.OnGuildInvite += (s, e) => fired = true;
            _emitter.FireOnGuildInvite("Inviter", "StormwindGuard");
            Assert.True(fired);
        }

        [Fact]
        public void OnCharacterCreateResponse_PassesResult()
        {
            CreateCharacterResult received = default;
            _emitter.OnCharacterCreateResponse += (s, e) => received = e.Result;
            _emitter.FireOnCharacterCreateResponse(new CharCreateResponse(CreateCharacterResult.NameInUse));
            Assert.Equal(CreateCharacterResult.NameInUse, received);
        }

        // ======== Multiple subscribers ========

        [Fact]
        public void MultipleSubscribers_AllFire()
        {
            int count = 0;
            _emitter.OnFightStart += (s, e) => count++;
            _emitter.OnFightStart += (s, e) => count++;
            _emitter.OnFightStart += (s, e) => count++;

            _emitter.FireOnFightStart();
            Assert.Equal(3, count);
        }

        // ======== Sender is emitter ========

        [Fact]
        public void Fire_SenderIsEmitter()
        {
            object sender = null;
            _emitter.OnLoginConnect += (s, e) => sender = s;
            _emitter.FireOnLoginConnect();
            Assert.Same(_emitter, sender);
        }
    }
}
