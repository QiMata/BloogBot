using BloogBot.AI.States;

namespace WWoWBot.AI.Tests.States;

public sealed class MinorStateTests
{
    [Fact]
    public void Constructor_PreservesProperties()
    {
        var state = new MinorState(BotActivity.Combat, "Engaging", "Approaching target");

        Assert.Equal(BotActivity.Combat, state.ParentActivity);
        Assert.Equal("Engaging", state.Name);
        Assert.Equal("Approaching target", state.Description);
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new MinorState(BotActivity.Combat, "", "description"));
    }

    [Fact]
    public void Constructor_WhitespaceName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new MinorState(BotActivity.Combat, "   ", "description"));
    }

    [Fact]
    public void Constructor_NullDescription_DefaultsToEmpty()
    {
        var state = new MinorState(BotActivity.Combat, "Engaging", null!);
        Assert.Equal(string.Empty, state.Description);
    }

    [Fact]
    public void None_CreatesDefaultMinorState()
    {
        var state = MinorState.None(BotActivity.Resting);

        Assert.Equal(BotActivity.Resting, state.ParentActivity);
        Assert.Equal("None", state.Name);
    }

    [Fact]
    public void None_DifferentActivities_CreateDistinctInstances()
    {
        var resting = MinorState.None(BotActivity.Resting);
        var combat = MinorState.None(BotActivity.Combat);

        Assert.NotEqual(resting, combat);
        Assert.Equal(BotActivity.Resting, resting.ParentActivity);
        Assert.Equal(BotActivity.Combat, combat.ParentActivity);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new MinorState(BotActivity.Combat, "Engaging", "Approaching target");
        var b = new MinorState(BotActivity.Combat, "Engaging", "Approaching target");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        var a = new MinorState(BotActivity.Combat, "Engaging", "desc");
        var b = new MinorState(BotActivity.Combat, "Casting", "desc");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentActivity_AreNotEqual()
    {
        var a = new MinorState(BotActivity.Combat, "Engaging", "desc");
        var b = new MinorState(BotActivity.Grinding, "Engaging", "desc");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ToString_IncludesActivityAndName()
    {
        var state = new MinorState(BotActivity.Combat, "Engaging", "desc");
        Assert.Equal("Combat.Engaging", state.ToString());
    }
}
