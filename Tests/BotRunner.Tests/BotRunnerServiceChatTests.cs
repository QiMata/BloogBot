using BotRunner;
using GameData.Core.Interfaces;
using Moq;

namespace BotRunner.Tests;

public class BotRunnerServiceChatTests
{
    [Fact]
    public void SendChatThroughBestAvailablePath_UsesDirectGmCapture_ForDotCommandsWhenSupported()
    {
        var objectManager = new Mock<IObjectManager>(MockBehavior.Strict);
        objectManager.SetupGet(x => x.SupportsDirectGmCommandCapture).Returns(true);
        objectManager.Setup(x => x.SendGmCommandAsync(".pool spawns 2618", 3000))
            .ReturnsAsync(["spawn-row-1"]);

        var responses = ActionDispatcher.SendChatThroughBestAvailablePath(objectManager.Object, ".pool spawns 2618");

        Assert.Equal(["spawn-row-1"], responses);
        objectManager.Verify(x => x.SendGmCommandAsync(".pool spawns 2618", 3000), Times.Once);
        objectManager.Verify(x => x.SendChatMessage(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SendChatThroughBestAvailablePath_UsesDefaultTimeout_ForNonPoolDotCommands()
    {
        var objectManager = new Mock<IObjectManager>(MockBehavior.Strict);
        objectManager.SetupGet(x => x.SupportsDirectGmCommandCapture).Returns(true);
        objectManager.Setup(x => x.SendGmCommandAsync(".learn 7731", 2000))
            .ReturnsAsync(["learned"]);

        var responses = ActionDispatcher.SendChatThroughBestAvailablePath(objectManager.Object, ".learn 7731");

        Assert.Equal(["learned"], responses);
        objectManager.Verify(x => x.SendGmCommandAsync(".learn 7731", 2000), Times.Once);
        objectManager.Verify(x => x.SendChatMessage(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SendChatThroughBestAvailablePath_UsesRegularChat_ForDotCommandsWhenDirectCaptureUnsupported()
    {
        var objectManager = new Mock<IObjectManager>(MockBehavior.Strict);
        objectManager.SetupGet(x => x.SupportsDirectGmCommandCapture).Returns(false);
        objectManager.Setup(x => x.SendChatMessage(".pool spawns 2618"));

        var responses = ActionDispatcher.SendChatThroughBestAvailablePath(objectManager.Object, ".pool spawns 2618");

        Assert.Empty(responses);
        objectManager.Verify(x => x.SendChatMessage(".pool spawns 2618"), Times.Once);
        objectManager.Verify(x => x.SendGmCommandAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void SendChatThroughBestAvailablePath_UsesRegularChat_ForNonCommandMessages()
    {
        var objectManager = new Mock<IObjectManager>(MockBehavior.Strict);
        objectManager.SetupGet(x => x.SupportsDirectGmCommandCapture).Returns(true);
        objectManager.Setup(x => x.SendChatMessage("hello world"));

        var responses = ActionDispatcher.SendChatThroughBestAvailablePath(objectManager.Object, "hello world");

        Assert.Empty(responses);
        objectManager.Verify(x => x.SendChatMessage("hello world"), Times.Once);
        objectManager.Verify(x => x.SendGmCommandAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
}
