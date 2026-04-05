using BloogBot.AI.Annotations;
using BloogBot.AI.Semantic;
using BloogBot.AI.States;
using Microsoft.SemanticKernel;

namespace WWoWBot.AI.Tests.Semantic;

public sealed class PluginCatalogTests
{
    [Fact]
    public void For_PluginWithMultipleActivityAttributes_MapsPluginToEachActivity()
    {
        var catalog = new PluginCatalog();

        var combatPlugins = catalog.For(BotActivity.Combat);
        var grindingPlugins = catalog.For(BotActivity.Grinding);

        Assert.Contains(combatPlugins, p => p.Name == nameof(MultiActivityTestPlugin));
        Assert.Contains(grindingPlugins, p => p.Name == nameof(MultiActivityTestPlugin));
    }

    [Fact]
    public void For_PluginWithoutPublicParameterlessConstructor_SkipsPluginRegistration()
    {
        var catalog = new PluginCatalog();

        var restingPlugins = catalog.For(BotActivity.Resting);

        Assert.DoesNotContain(restingPlugins, p => p.Name == nameof(NoDefaultConstructorTestPlugin));
    }
}

[ActivityPlugin(BotActivity.Combat)]
[ActivityPlugin(BotActivity.Grinding)]
public sealed class MultiActivityTestPlugin
{
    [KernelFunction("ping")]
    public string Ping() => "pong";
}

[ActivityPlugin(BotActivity.Resting)]
public sealed class NoDefaultConstructorTestPlugin
{
    public NoDefaultConstructorTestPlugin(string value)
    {
        _ = value;
    }

    [KernelFunction("noop")]
    public string Noop() => "noop";
}
