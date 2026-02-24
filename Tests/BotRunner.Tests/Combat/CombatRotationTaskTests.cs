using BotRunner.Constants;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Moq;

namespace BotRunner.Tests.Combat;

/// <summary>
/// Concrete subclass for testing abstract CombatRotationTask.
/// </summary>
public class TestCombatRotation : CombatRotationTask
{
    public TestCombatRotation(IBotContext context) : base(context) { }

    public override void PerformCombatRotation() { }

    // Expose protected methods for testing
    public bool CallUpdate(float attackDistance) => Update(attackDistance);
    public bool CallTryCastSpell(string name, bool condition, bool castOnSelf = false)
        => TryCastSpell(name, condition, castOnSelf);
    public bool CallTryCastSpell(string name, int minRange = 0, int maxRange = int.MaxValue,
        bool condition = true, bool castOnSelf = false, Action? callback = null)
        => TryCastSpell(name, minRange, maxRange, condition, castOnSelf, callback);
    public bool CallTryUseAbility(string name, bool condition)
        => TryUseAbility(name, condition);
    public bool CallTryUseAbility(string name, int cost = 0, bool condition = true)
        => TryUseAbility(name, cost, condition);
    public bool CallTryUseAbility(string name, int cost, bool condition, Action callback)
        => TryUseAbility(name, cost, condition, callback);
    public bool CallTryUseAbilityById(string name, int spellId, int cost = 0, bool condition = true)
        => TryUseAbilityById(name, spellId, cost, condition);
    public bool CallTryUseHealthPotion() => TryUseHealthPotion();
    public bool CallTryUseManaPotion() => TryUseManaPotion();
    public bool GetTargetMovingTowardPlayer() => TargetMovingTowardPlayer;
    public void CallAssignDPSTarget() => AssignDPSTarget();
    public bool CallEnsureTarget() => EnsureTarget();
    public bool CallMoveBehindTarget(float distance) => MoveBehindTarget(distance);
    public bool CallMoveBehindTankSpot(float distance) => MoveBehindTankSpot(distance);
    public bool GetIsKiting() => IsKiting;
    public void CallStartKite(int durationMs) => StartKite(durationMs);
    public void CallStopKiting() => StopKiting();
}

public class CombatRotationTaskTests : IDisposable
{
    private readonly Mock<IBotContext> _ctx;
    private readonly Mock<IObjectManager> _om;
    private readonly Mock<IWoWLocalPlayer> _player;
    private readonly Mock<IWoWEventHandler> _eventHandler;
    private readonly TestCombatRotation _sut;

    public CombatRotationTaskTests()
    {
        _ctx = new Mock<IBotContext>();
        _om = new Mock<IObjectManager>();
        _player = new Mock<IWoWLocalPlayer>();
        _eventHandler = new Mock<IWoWEventHandler>();

        _ctx.Setup(c => c.ObjectManager).Returns(_om.Object);
        _ctx.Setup(c => c.Config).Returns(new BotBehaviorConfig());
        _ctx.Setup(c => c.EventHandler).Returns(_eventHandler.Object);
        _ctx.Setup(c => c.BotTasks).Returns(new Stack<IBotTask>());

        _om.Setup(o => o.Player).Returns(_player.Object);
        _player.Setup(p => p.Position).Returns(new Position(0, 0, 0));
        _player.Setup(p => p.Guid).Returns(100UL);

        _sut = new TestCombatRotation(_ctx.Object);
    }

    public void Dispose()
    {
        // Reset static potion cooldown by waiting or using reflection
        // We use a very old DateTime to effectively reset between tests
        typeof(CombatRotationTask)
            .GetField("_lastPotionUsed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.SetValue(null, DateTime.MinValue);
    }

    private Mock<IWoWUnit> CreateTargetAt(float x, float y, float z, ulong guid = 200UL)
    {
        var target = new Mock<IWoWUnit>();
        target.Setup(t => t.Position).Returns(new Position(x, y, z));
        target.Setup(t => t.Guid).Returns(guid);
        target.Setup(t => t.HealthPercent).Returns(50);
        return target;
    }

    // ─── Update ─────────────────────────────────────────────

    [Fact]
    public void Update_NoTarget_ReturnsFalse()
    {
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns((IWoWUnit?)null);

        Assert.False(_sut.CallUpdate(5f));
    }

    [Fact]
    public void Update_TargetInRange_ReturnsFalse()
    {
        var target = CreateTargetAt(3, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);

        Assert.False(_sut.CallUpdate(5f));
    }

    [Fact]
    public void Update_TargetOutOfRange_ReturnsTrue()
    {
        var target = CreateTargetAt(10, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);

        Assert.True(_sut.CallUpdate(5f));
    }

    [Fact]
    public void Update_TargetExactlyAtDistance_ReturnsFalse()
    {
        var target = CreateTargetAt(5, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);

        Assert.False(_sut.CallUpdate(5f));
    }

    // ─── TryCastSpell (condition-only) ──────────────────────

    [Fact]
    public void TryCastSpell_ConditionFalse_ReturnsFalse()
    {
        Assert.False(_sut.CallTryCastSpell("Fireball", false));
    }

    [Fact]
    public void TryCastSpell_NoTarget_ReturnsFalse()
    {
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns((IWoWUnit?)null);

        Assert.False(_sut.CallTryCastSpell("Fireball", true));
    }

    [Fact]
    public void TryCastSpell_SpellNotReady_ReturnsFalse()
    {
        var target = CreateTargetAt(5, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);
        _om.Setup(o => o.IsSpellReady("Fireball")).Returns(false);

        Assert.False(_sut.CallTryCastSpell("Fireball", true));
    }

    [Fact]
    public void TryCastSpell_AllConditionsMet_CastsAndReturnsTrue()
    {
        var target = CreateTargetAt(5, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);
        _om.Setup(o => o.IsSpellReady("Fireball")).Returns(true);

        Assert.True(_sut.CallTryCastSpell("Fireball", true));
        _om.Verify(o => o.CastSpell("Fireball", -1, false), Times.Once);
    }

    [Fact]
    public void TryCastSpell_CastOnSelf_NoTargetRequired()
    {
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns((IWoWUnit?)null);
        _om.Setup(o => o.IsSpellReady("Renew")).Returns(true);

        Assert.True(_sut.CallTryCastSpell("Renew", true, castOnSelf: true));
        _om.Verify(o => o.CastSpell("Renew", -1, true), Times.Once);
    }

    // ─── TryCastSpell (range-based) ─────────────────────────

    [Fact]
    public void TryCastSpell_ConditionFalse_WithRange_ReturnsFalse()
    {
        Assert.False(_sut.CallTryCastSpell("Fireball", 0, 30, condition: false));
    }

    [Fact]
    public void TryCastSpell_TargetTooClose_ReturnsFalse()
    {
        var target = CreateTargetAt(2, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);
        _om.Setup(o => o.IsSpellReady("Fireball")).Returns(true);

        Assert.False(_sut.CallTryCastSpell("Fireball", minRange: 5, maxRange: 30));
    }

    [Fact]
    public void TryCastSpell_TargetTooFar_ReturnsFalse()
    {
        var target = CreateTargetAt(40, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);
        _om.Setup(o => o.IsSpellReady("Fireball")).Returns(true);

        Assert.False(_sut.CallTryCastSpell("Fireball", minRange: 0, maxRange: 30));
    }

    [Fact]
    public void TryCastSpell_TargetInRange_CastsAndReturnsTrue()
    {
        var target = CreateTargetAt(15, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);
        _om.Setup(o => o.IsSpellReady("Fireball")).Returns(true);

        Assert.True(_sut.CallTryCastSpell("Fireball", minRange: 5, maxRange: 30));
        _om.Verify(o => o.CastSpell("Fireball", -1, false), Times.Once);
    }

    [Fact]
    public void TryCastSpell_Callback_InvokedOnSuccess()
    {
        var target = CreateTargetAt(5, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);
        _om.Setup(o => o.IsSpellReady("Fireball")).Returns(true);
        bool callbackInvoked = false;

        _sut.CallTryCastSpell("Fireball", callback: () => callbackInvoked = true);

        Assert.True(callbackInvoked);
    }

    [Fact]
    public void TryCastSpell_Callback_NotInvokedOnFailure()
    {
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns((IWoWUnit?)null);
        bool callbackInvoked = false;

        _sut.CallTryCastSpell("Fireball", callback: () => callbackInvoked = true);

        Assert.False(callbackInvoked);
    }

    // ─── TryUseAbility ──────────────────────────────────────

    [Fact]
    public void TryUseAbility_ConditionFalse_ReturnsFalse()
    {
        Assert.False(_sut.CallTryUseAbility("Execute", false));
    }

    [Fact]
    public void TryUseAbility_NotEnoughEnergy_ReturnsFalse()
    {
        _player.Setup(p => p.Energy).Returns(20u);
        _player.Setup(p => p.Rage).Returns(0u);
        _om.Setup(o => o.IsSpellReady("Sinister Strike")).Returns(true);

        Assert.False(_sut.CallTryUseAbility("Sinister Strike", 40));
    }

    [Fact]
    public void TryUseAbility_EnoughEnergy_CastsAndReturnsTrue()
    {
        _player.Setup(p => p.Energy).Returns(60u);
        _player.Setup(p => p.Rage).Returns(0u);
        _om.Setup(o => o.IsSpellReady("Sinister Strike")).Returns(true);

        Assert.True(_sut.CallTryUseAbility("Sinister Strike", 40));
        _om.Verify(o => o.CastSpell("Sinister Strike", -1, false), Times.Once);
    }

    [Fact]
    public void TryUseAbility_EnoughRage_CastsAndReturnsTrue()
    {
        _player.Setup(p => p.Energy).Returns(0u);
        _player.Setup(p => p.Rage).Returns(30u);
        _om.Setup(o => o.IsSpellReady("Execute")).Returns(true);

        Assert.True(_sut.CallTryUseAbility("Execute", 15));
        _om.Verify(o => o.CastSpell("Execute", -1, false), Times.Once);
    }

    [Fact]
    public void TryUseAbility_SpellNotReady_ReturnsFalse()
    {
        _player.Setup(p => p.Energy).Returns(100u);
        _player.Setup(p => p.Rage).Returns(100u);
        _om.Setup(o => o.IsSpellReady("Execute")).Returns(false);

        Assert.False(_sut.CallTryUseAbility("Execute", 0));
    }

    [Fact]
    public void TryUseAbility_WithCallback_InvokedOnSuccess()
    {
        _player.Setup(p => p.Energy).Returns(0u);
        _player.Setup(p => p.Rage).Returns(50u);
        _om.Setup(o => o.IsSpellReady("Slam")).Returns(true);
        bool callbackInvoked = false;

        _sut.CallTryUseAbility("Slam", 15, true, () => callbackInvoked = true);

        Assert.True(callbackInvoked);
    }

    [Fact]
    public void TryUseAbility_WithCallback_NotInvokedOnFailure()
    {
        _player.Setup(p => p.Energy).Returns(0u);
        _player.Setup(p => p.Rage).Returns(5u);
        bool callbackInvoked = false;

        _sut.CallTryUseAbility("Slam", 15, true, () => callbackInvoked = true);

        Assert.False(callbackInvoked);
    }

    // ─── TryUseAbilityById ──────────────────────────────────

    [Fact]
    public void TryUseAbilityById_ConditionFalse_ReturnsFalse()
    {
        Assert.False(_sut.CallTryUseAbilityById("Execute", 5308, 15, condition: false));
    }

    [Fact]
    public void TryUseAbilityById_NotEnoughResource_ReturnsFalse()
    {
        _player.Setup(p => p.Energy).Returns(0u);
        _player.Setup(p => p.Rage).Returns(5u);

        Assert.False(_sut.CallTryUseAbilityById("Execute", 5308, 15));
    }

    [Fact]
    public void TryUseAbilityById_AllMet_CastsAndReturnsTrue()
    {
        _player.Setup(p => p.Energy).Returns(0u);
        _player.Setup(p => p.Rage).Returns(30u);
        _om.Setup(o => o.IsSpellReady("Execute")).Returns(true);

        Assert.True(_sut.CallTryUseAbilityById("Execute", 5308, 15));
        _om.Verify(o => o.CastSpell("Execute", -1, false), Times.Once);
    }

    // ─── TargetMovingTowardPlayer ───────────────────────────

    [Fact]
    public void TargetMovingTowardPlayer_NoTarget_ReturnsFalse()
    {
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns((IWoWUnit?)null);

        Assert.False(_sut.GetTargetMovingTowardPlayer());
    }

    [Fact]
    public void TargetMovingTowardPlayer_NotInCombat_ReturnsFalse()
    {
        var target = CreateTargetAt(10, 0, 0);
        target.Setup(t => t.IsInCombat).Returns(false);
        target.Setup(t => t.TargetGuid).Returns(100UL);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);

        Assert.False(_sut.GetTargetMovingTowardPlayer());
    }

    [Fact]
    public void TargetMovingTowardPlayer_NotTargetingPlayer_ReturnsFalse()
    {
        var target = CreateTargetAt(10, 0, 0);
        target.Setup(t => t.IsInCombat).Returns(true);
        target.Setup(t => t.TargetGuid).Returns(999UL); // targeting someone else
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);

        Assert.False(_sut.GetTargetMovingTowardPlayer());
    }

    [Fact]
    public void TargetMovingTowardPlayer_TooClose_ReturnsFalse()
    {
        var target = CreateTargetAt(2, 0, 0); // within 3y
        target.Setup(t => t.IsInCombat).Returns(true);
        target.Setup(t => t.TargetGuid).Returns(100UL);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);

        Assert.False(_sut.GetTargetMovingTowardPlayer());
    }

    [Fact]
    public void TargetMovingTowardPlayer_AllConditionsMet_ReturnsTrue()
    {
        var target = CreateTargetAt(10, 0, 0);
        target.Setup(t => t.IsInCombat).Returns(true);
        target.Setup(t => t.TargetGuid).Returns(100UL);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);

        Assert.True(_sut.GetTargetMovingTowardPlayer());
    }

    // ─── AssignDPSTarget ────────────────────────────────────

    [Fact]
    public void AssignDPSTarget_NoAggressors_DoesNotSetTarget()
    {
        _om.Setup(o => o.Aggressors).Returns(Enumerable.Empty<IWoWUnit>());

        _sut.CallAssignDPSTarget();

        _om.Verify(o => o.SetTarget(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public void AssignDPSTarget_SingleAggressor_TargetsIt()
    {
        var mob = new Mock<IWoWUnit>();
        mob.Setup(m => m.HealthPercent).Returns(50u);
        mob.Setup(m => m.Guid).Returns(300UL);
        _om.Setup(o => o.Aggressors).Returns(new[] { mob.Object });

        _sut.CallAssignDPSTarget();

        _om.Verify(o => o.SetTarget(300UL), Times.Once);
    }

    [Fact]
    public void AssignDPSTarget_MultipleAggressors_TargetsLowestHealth()
    {
        var mob1 = new Mock<IWoWUnit>();
        mob1.Setup(m => m.HealthPercent).Returns(80u);
        mob1.Setup(m => m.Guid).Returns(301UL);

        var mob2 = new Mock<IWoWUnit>();
        mob2.Setup(m => m.HealthPercent).Returns(20u);
        mob2.Setup(m => m.Guid).Returns(302UL);

        var mob3 = new Mock<IWoWUnit>();
        mob3.Setup(m => m.HealthPercent).Returns(50u);
        mob3.Setup(m => m.Guid).Returns(303UL);

        _om.Setup(o => o.Aggressors).Returns(new[] { mob1.Object, mob2.Object, mob3.Object });

        _sut.CallAssignDPSTarget();

        _om.Verify(o => o.SetTarget(302UL), Times.Once);
    }

    // ─── MoveBehindTarget ───────────────────────────────────

    [Fact]
    public void MoveBehindTarget_NoTarget_ReturnsFalse()
    {
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns((IWoWUnit?)null);

        Assert.False(_sut.CallMoveBehindTarget(5f));
    }

    [Fact]
    public void MoveBehindTarget_InRange_ReturnsFalse()
    {
        var target = CreateTargetAt(3, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);

        Assert.False(_sut.CallMoveBehindTarget(5f));
    }

    [Fact]
    public void MoveBehindTarget_OutOfRange_ReturnsTrue()
    {
        var target = CreateTargetAt(10, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);

        Assert.True(_sut.CallMoveBehindTarget(5f));
    }

    // ─── MoveBehindTankSpot ─────────────────────────────────

    [Fact]
    public void MoveBehindTankSpot_NoPartyLeader_ReturnsFalse()
    {
        _om.Setup(o => o.PartyLeader).Returns((IWoWPlayer?)null);

        Assert.False(_sut.CallMoveBehindTankSpot(5f));
    }

    [Fact]
    public void MoveBehindTankSpot_InRange_ReturnsFalse()
    {
        var leader = new Mock<IWoWPlayer>();
        leader.Setup(l => l.Position).Returns(new Position(3, 0, 0));
        _om.Setup(o => o.PartyLeader).Returns(leader.Object);

        Assert.False(_sut.CallMoveBehindTankSpot(5f));
    }

    [Fact]
    public void MoveBehindTankSpot_OutOfRange_ReturnsTrue()
    {
        var leader = new Mock<IWoWPlayer>();
        leader.Setup(l => l.Position).Returns(new Position(20, 0, 0));
        _om.Setup(o => o.PartyLeader).Returns(leader.Object);

        Assert.True(_sut.CallMoveBehindTankSpot(5f));
    }

    // ─── TryUseHealthPotion ─────────────────────────────────

    [Fact]
    public void TryUseHealthPotion_HealthAboveThreshold_ReturnsFalse()
    {
        _player.Setup(p => p.HealthPercent).Returns(80u);

        Assert.False(_sut.CallTryUseHealthPotion());
    }

    [Fact]
    public void TryUseHealthPotion_NoPotionsInBags_ReturnsFalse()
    {
        _player.Setup(p => p.HealthPercent).Returns(20u);
        _om.Setup(o => o.GetContainedItems()).Returns(Enumerable.Empty<IWoWItem>());

        Assert.False(_sut.CallTryUseHealthPotion());
    }

    [Fact]
    public void TryUseHealthPotion_HasPotion_UsesItAndReturnsTrue()
    {
        _player.Setup(p => p.HealthPercent).Returns(20u);

        var potion = new Mock<IWoWItem>();
        potion.Setup(i => i.Name).Returns("Greater Healing Potion");
        potion.Setup(i => i.Guid).Returns(500UL);

        _om.Setup(o => o.GetContainedItems()).Returns(new[] { potion.Object });
        _om.Setup(o => o.GetContainedItem(0, 0)).Returns(potion.Object);

        Assert.True(_sut.CallTryUseHealthPotion());
        _om.Verify(o => o.UseItem(0, 0, 0), Times.Once);
    }

    [Fact]
    public void TryUseHealthPotion_OnCooldown_ReturnsFalse()
    {
        _player.Setup(p => p.HealthPercent).Returns(20u);

        var potion = new Mock<IWoWItem>();
        potion.Setup(i => i.Name).Returns("Healing Potion");
        potion.Setup(i => i.Guid).Returns(500UL);
        _om.Setup(o => o.GetContainedItems()).Returns(new[] { potion.Object });
        _om.Setup(o => o.GetContainedItem(0, 0)).Returns(potion.Object);

        // Use a potion to trigger cooldown
        Assert.True(_sut.CallTryUseHealthPotion());

        // Second attempt should fail due to cooldown
        Assert.False(_sut.CallTryUseHealthPotion());
    }

    // ─── TryUseManaPotion ───────────────────────────────────

    [Fact]
    public void TryUseManaPotion_NoManaClass_ReturnsFalse()
    {
        _player.Setup(p => p.MaxMana).Returns(0u);

        Assert.False(_sut.CallTryUseManaPotion());
    }

    [Fact]
    public void TryUseManaPotion_ManaAboveThreshold_ReturnsFalse()
    {
        _player.Setup(p => p.MaxMana).Returns(1000u);
        _player.Setup(p => p.ManaPercent).Returns(50u);

        Assert.False(_sut.CallTryUseManaPotion());
    }

    [Fact]
    public void TryUseManaPotion_HasManaPotion_UsesItAndReturnsTrue()
    {
        _player.Setup(p => p.MaxMana).Returns(1000u);
        _player.Setup(p => p.ManaPercent).Returns(10u);
        _player.Setup(p => p.HealthPercent).Returns(100u); // prevent health potion path

        var potion = new Mock<IWoWItem>();
        potion.Setup(i => i.Name).Returns("Greater Mana Potion");
        potion.Setup(i => i.Guid).Returns(501UL);

        _om.Setup(o => o.GetContainedItems()).Returns(new[] { potion.Object });
        _om.Setup(o => o.GetContainedItem(0, 0)).Returns(potion.Object);

        Assert.True(_sut.CallTryUseManaPotion());
        _om.Verify(o => o.UseItem(0, 0, 0), Times.Once);
    }

    // ─── Potion name matching (via TryUseHealthPotion/TryUseManaPotion) ─

    [Theory]
    [InlineData("Minor Healing Potion")]
    [InlineData("Lesser Healing Potion")]
    [InlineData("Healing Potion")]
    [InlineData("Greater Healing Potion")]
    [InlineData("Superior Healing Potion")]
    [InlineData("Major Healing Potion")]
    public void TryUseHealthPotion_RecognizesAllHealthPotionNames(string potionName)
    {
        _player.Setup(p => p.HealthPercent).Returns(20u);

        var potion = new Mock<IWoWItem>();
        potion.Setup(i => i.Name).Returns(potionName);
        potion.Setup(i => i.Guid).Returns(500UL);

        _om.Setup(o => o.GetContainedItems()).Returns(new[] { potion.Object });
        _om.Setup(o => o.GetContainedItem(0, 0)).Returns(potion.Object);

        Assert.True(_sut.CallTryUseHealthPotion());
    }

    [Theory]
    [InlineData("Minor Mana Potion")]
    [InlineData("Lesser Mana Potion")]
    [InlineData("Mana Potion")]
    [InlineData("Greater Mana Potion")]
    [InlineData("Superior Mana Potion")]
    [InlineData("Major Mana Potion")]
    public void TryUseManaPotion_RecognizesAllManaPotionNames(string potionName)
    {
        _player.Setup(p => p.MaxMana).Returns(1000u);
        _player.Setup(p => p.ManaPercent).Returns(5u);

        var potion = new Mock<IWoWItem>();
        potion.Setup(i => i.Name).Returns(potionName);
        potion.Setup(i => i.Guid).Returns(501UL);

        _om.Setup(o => o.GetContainedItems()).Returns(new[] { potion.Object });
        _om.Setup(o => o.GetContainedItem(0, 0)).Returns(potion.Object);

        Assert.True(_sut.CallTryUseManaPotion());
    }

    [Fact]
    public void TryUseHealthPotion_IgnoresNonPotionItems()
    {
        _player.Setup(p => p.HealthPercent).Returns(20u);

        var item = new Mock<IWoWItem>();
        item.Setup(i => i.Name).Returns("Linen Bandage");
        item.Setup(i => i.Guid).Returns(500UL);

        _om.Setup(o => o.GetContainedItems()).Returns(new[] { item.Object });

        Assert.False(_sut.CallTryUseHealthPotion());
    }

    [Fact]
    public void TryUseHealthPotion_IgnoresNullNameItems()
    {
        _player.Setup(p => p.HealthPercent).Returns(20u);

        var item = new Mock<IWoWItem>();
        item.Setup(i => i.Name).Returns((string?)null);
        item.Setup(i => i.Guid).Returns(500UL);

        _om.Setup(o => o.GetContainedItems()).Returns(new[] { item.Object });

        Assert.False(_sut.CallTryUseHealthPotion());
    }

    // ─── CastOnSelf with range-based overload ───────────────

    [Fact]
    public void TryCastSpell_CastOnSelf_DistanceIsZero()
    {
        // When castOnSelf is true, distance should be calculated as 0
        // so minRange=0 is always valid
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns((IWoWUnit?)null);
        _om.Setup(o => o.IsSpellReady("Rejuvenation")).Returns(true);

        Assert.True(_sut.CallTryCastSpell("Rejuvenation", 0, 30, true, castOnSelf: true));
        _om.Verify(o => o.CastSpell("Rejuvenation", -1, true), Times.Once);
    }

    // ─── Update calls potion checks ─────────────────────────

    [Fact]
    public void Update_CallsPotionChecks()
    {
        _player.Setup(p => p.HealthPercent).Returns(100u);
        _player.Setup(p => p.MaxMana).Returns(0u);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns((IWoWUnit?)null);

        // Should not throw; verifies potion checks execute without error
        _sut.CallUpdate(5f);
    }

    // ─── Kiting ───────────────────────────────────────────────

    [Fact]
    public void IsKiting_FalseByDefault()
    {
        Assert.False(_sut.GetIsKiting());
    }

    [Fact]
    public void StartKite_SetsIsKitingTrue()
    {
        _sut.CallStartKite(5000);
        Assert.True(_sut.GetIsKiting());
        _om.Verify(o => o.StartMovement(GameData.Core.Enums.ControlBits.Back), Times.Once);
    }

    [Fact]
    public void StopKiting_ClearsIsKiting()
    {
        _sut.CallStartKite(5000);
        Assert.True(_sut.GetIsKiting());

        _sut.CallStopKiting();
        Assert.False(_sut.GetIsKiting());
        _om.Verify(o => o.StopMovement(GameData.Core.Enums.ControlBits.Back), Times.Once);
    }

    [Fact]
    public void StopKiting_WhenNotKiting_DoesNotCallStopMovement()
    {
        _sut.CallStopKiting();
        _om.Verify(o => o.StopMovement(It.IsAny<GameData.Core.Enums.ControlBits>()), Times.Never);
    }

    [Fact]
    public async Task IsKiting_AutoStopsAfterDuration()
    {
        // Start kiting with 1ms duration — should expire after brief delay
        _sut.CallStartKite(1);
        Assert.True(_sut.GetIsKiting());
        await Task.Delay(20); // Wait for timer to expire
        Assert.False(_sut.GetIsKiting());
        _om.Verify(o => o.StopMovement(GameData.Core.Enums.ControlBits.Back), Times.Once);
    }

    [Fact]
    public void TryCastSpell_KiteCallback_StartsKiting()
    {
        var target = CreateTargetAt(5, 0, 0);
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);
        _om.Setup(o => o.IsSpellReady(It.IsAny<string>())).Returns(true);

        bool result = _sut.CallTryCastSpell("Wing Clip", 0, 5, true, false, () => _sut.CallStartKite(1500));

        Assert.True(result);
        Assert.True(_sut.GetIsKiting());
    }

    // ─── EnsureTarget ─────────────────────────────────────────

    [Fact]
    public void EnsureTarget_NoAggressors_PopsBotTask()
    {
        var tasks = new Stack<IBotTask>();
        tasks.Push(Mock.Of<IBotTask>()); // push a dummy task so Pop() works
        _ctx.Setup(c => c.BotTasks).Returns(tasks);
        _om.Setup(o => o.Aggressors).Returns(Enumerable.Empty<IWoWUnit>());

        bool result = _sut.CallEnsureTarget();

        Assert.False(result);
        Assert.Empty(tasks); // task was popped
    }

    [Fact]
    public void EnsureTarget_HasAggressors_ValidTarget_ReturnsTrue()
    {
        var target = CreateTargetAt(5, 0, 0);
        target.Setup(t => t.HealthPercent).Returns(50u);
        _om.Setup(o => o.Aggressors).Returns(new[] { target.Object });
        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(target.Object);

        bool result = _sut.CallEnsureTarget();

        Assert.True(result);
    }

    [Fact]
    public void EnsureTarget_DeadTarget_SwitchesToAggressor()
    {
        var deadTarget = CreateTargetAt(5, 0, 0);
        deadTarget.Setup(t => t.HealthPercent).Returns(0u);

        var liveAggressor = CreateTargetAt(10, 0, 0, 300UL);
        liveAggressor.Setup(t => t.HealthPercent).Returns(80u);

        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns(deadTarget.Object);
        _om.Setup(o => o.Aggressors).Returns(new[] { liveAggressor.Object });

        bool result = _sut.CallEnsureTarget();

        Assert.True(result);
        _om.Verify(o => o.SetTarget(300UL), Times.Once);
    }

    [Fact]
    public void EnsureTarget_NullTarget_SwitchesToAggressor()
    {
        var aggressor = CreateTargetAt(5, 0, 0);
        aggressor.Setup(t => t.HealthPercent).Returns(50u);

        _om.Setup(o => o.GetTarget(It.IsAny<IWoWUnit>())).Returns((IWoWUnit?)null);
        _om.Setup(o => o.Aggressors).Returns(new[] { aggressor.Object });

        bool result = _sut.CallEnsureTarget();

        Assert.True(result);
        _om.Verify(o => o.SetTarget(It.IsAny<ulong>()), Times.Once);
    }
}
