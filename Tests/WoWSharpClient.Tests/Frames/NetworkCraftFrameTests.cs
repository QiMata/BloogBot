using Moq;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Frames;
using WoWSharpClient.Networking.ClientComponents.I;
using Xunit;

namespace WoWSharpClient.Tests.Frames;

/// <summary>
/// Unit coverage for the S1.16 BG CraftFrame implementation. Stubs
/// <see cref="IProfessionsNetworkClientComponent"/> and asserts the
/// <c>ICraftFrame</c> contract routes through the professions packet path.
/// The wire-up half; LiveValidation CraftParityTests would close the
/// end-to-end half.
/// </summary>
public class NetworkCraftFrameTests
{
    private static NetworkCraftFrame WithAgent(IProfessionsNetworkClientComponent? agent)
        => new(() => agent);

    private static Mock<IProfessionsNetworkClientComponent> MockOpenCrafting()
    {
        var mock = new Mock<IProfessionsNetworkClientComponent>();
        mock.SetupGet(p => p.IsCraftingWindowOpen).Returns(true);
        mock.Setup(p => p.GetRecipeMaterials(It.IsAny<uint>())).Returns(System.Array.Empty<RecipeMaterial>());
        return mock;
    }

    [Fact]
    public void IsOpen_AgentNull_ReturnsFalse()
        => Assert.False(WithAgent(null).IsOpen);

    [Fact]
    public void IsOpen_AgentClosed_ReturnsFalse()
    {
        var mock = new Mock<IProfessionsNetworkClientComponent>();
        mock.SetupGet(p => p.IsCraftingWindowOpen).Returns(false);
        Assert.False(WithAgent(mock.Object).IsOpen);
    }

    [Fact]
    public void IsOpen_AgentOpen_ReturnsTrue()
        => Assert.True(WithAgent(MockOpenCrafting().Object).IsOpen);

    [Fact]
    public void HasMaterialsNeeded_AgentNull_ReturnsFalse()
        => Assert.False(WithAgent(null).HasMaterialsNeeded(0));

    [Fact]
    public void HasMaterialsNeeded_NoMaterialsReported_ReturnsTrue()
    {
        // GetRecipeMaterials stub returns empty -- treat as "nothing missing".
        // Server-side CMSG_CAST_SPELL is the authoritative reagent check.
        var mock = MockOpenCrafting();
        Assert.True(WithAgent(mock.Object).HasMaterialsNeeded(42));
    }

    [Fact]
    public void HasMaterialsNeeded_AllReagentsPresent_ReturnsTrue()
    {
        var mock = MockOpenCrafting();
        mock.Setup(p => p.GetRecipeMaterials(7u)).Returns(new[]
        {
            new RecipeMaterial { ItemId = 1, RequiredQuantity = 2, AvailableQuantity = 5 },
            new RecipeMaterial { ItemId = 2, RequiredQuantity = 1, AvailableQuantity = 1 },
        });
        Assert.True(WithAgent(mock.Object).HasMaterialsNeeded(7));
    }

    [Fact]
    public void HasMaterialsNeeded_ReagentShortfall_ReturnsFalse()
    {
        var mock = MockOpenCrafting();
        mock.Setup(p => p.GetRecipeMaterials(7u)).Returns(new[]
        {
            new RecipeMaterial { ItemId = 1, RequiredQuantity = 2, AvailableQuantity = 5 },
            new RecipeMaterial { ItemId = 2, RequiredQuantity = 4, AvailableQuantity = 1 },
        });
        Assert.False(WithAgent(mock.Object).HasMaterialsNeeded(7));
    }

    [Fact]
    public void Craft_RoutesToCraftItemAsyncWithRecipeId()
    {
        var mock = MockOpenCrafting();
        mock.Setup(p => p.CraftItemAsync(42u, 1u, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        WithAgent(mock.Object).Craft(42);

        mock.Verify(p => p.CraftItemAsync(42u, 1u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Craft_AgentNull_DoesNotThrow()
        => WithAgent(null).Craft(1);

    [Fact]
    public void Craft_NegativeSlot_ClampedToZero()
    {
        var mock = MockOpenCrafting();
        mock.Setup(p => p.CraftItemAsync(0u, 1u, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        WithAgent(mock.Object).Craft(-5);

        mock.Verify(p => p.CraftItemAsync(0u, 1u, It.IsAny<CancellationToken>()), Times.Once);
    }
}
