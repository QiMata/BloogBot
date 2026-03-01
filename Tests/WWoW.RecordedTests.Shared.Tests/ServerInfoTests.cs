using FluentAssertions;
using WWoW.RecordedTests.Shared.Abstractions;

namespace WWoW.RecordedTests.Shared.Tests;

public class ServerInfoTests
{
    [Fact]
    public void Constructor_HostAndPort_PreservesValues()
    {
        var info = new ServerInfo("192.168.1.100", 3724);
        info.Host.Should().Be("192.168.1.100");
        info.Port.Should().Be(3724);
    }

    [Fact]
    public void Constructor_Realm_DefaultsToNull()
    {
        var info = new ServerInfo("localhost", 3724);
        info.Realm.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithRealm_PreservesRealm()
    {
        var info = new ServerInfo("localhost", 3724, "MyRealm");
        info.Realm.Should().Be("MyRealm");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ServerInfo("127.0.0.1", 3724, "Realm1");
        var b = new ServerInfo("127.0.0.1", 3724, "Realm1");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentHost_AreNotEqual()
    {
        var a = new ServerInfo("127.0.0.1", 3724);
        var b = new ServerInfo("10.0.0.1", 3724);
        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_DifferentPort_AreNotEqual()
    {
        var a = new ServerInfo("127.0.0.1", 3724);
        var b = new ServerInfo("127.0.0.1", 8085);
        a.Should().NotBe(b);
    }

    [Fact]
    public void Deconstruct_ReturnsAllComponents()
    {
        var info = new ServerInfo("host", 1234, "realm");
        var (host, port, realm) = info;
        host.Should().Be("host");
        port.Should().Be(1234);
        realm.Should().Be("realm");
    }
}
