using FluentAssertions;
using WWoW.RecordedTests.Shared;

namespace WWoW.RecordedTests.Shared.Tests;

public class TrueNasAppReleaseTests
{
    [Fact]
    public void HasConnectionInfo_WithHostAndPort_ReturnsTrue()
    {
        var release = new TrueNasAppRelease("test", true, false, "192.168.1.100", 3724, null);
        release.HasConnectionInfo.Should().BeTrue();
    }

    [Fact]
    public void HasConnectionInfo_WithNullHost_ReturnsFalse()
    {
        var release = new TrueNasAppRelease("test", true, false, null, 3724, null);
        release.HasConnectionInfo.Should().BeFalse();
    }

    [Fact]
    public void HasConnectionInfo_WithEmptyHost_ReturnsFalse()
    {
        var release = new TrueNasAppRelease("test", true, false, "", 3724, null);
        release.HasConnectionInfo.Should().BeFalse();
    }

    [Fact]
    public void HasConnectionInfo_WithWhitespaceHost_ReturnsFalse()
    {
        var release = new TrueNasAppRelease("test", true, false, "   ", 3724, null);
        release.HasConnectionInfo.Should().BeFalse();
    }

    [Fact]
    public void HasConnectionInfo_WithNullPort_ReturnsFalse()
    {
        var release = new TrueNasAppRelease("test", true, false, "192.168.1.100", null, null);
        release.HasConnectionInfo.Should().BeFalse();
    }

    [Fact]
    public void HasConnectionInfo_NullHostAndNullPort_ReturnsFalse()
    {
        var release = new TrueNasAppRelease("test", false, false, null, null, null);
        release.HasConnectionInfo.Should().BeFalse();
    }

    [Fact]
    public void Name_IsPreserved()
    {
        var release = new TrueNasAppRelease("my-release", true, false, "host", 3724, "realm1");
        release.Name.Should().Be("my-release");
    }

    [Fact]
    public void IsRunning_IsPreserved()
    {
        var release = new TrueNasAppRelease("test", true, false, null, null, null);
        release.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void IsCheckedOut_IsPreserved()
    {
        var release = new TrueNasAppRelease("test", false, true, null, null, null);
        release.IsCheckedOut.Should().BeTrue();
    }

    [Fact]
    public void Realm_IsPreserved()
    {
        var release = new TrueNasAppRelease("test", false, false, "host", 3724, "MyRealm");
        release.Realm.Should().Be("MyRealm");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new TrueNasAppRelease("test", true, false, "host", 3724, "realm");
        var b = new TrueNasAppRelease("test", true, false, "host", 3724, "realm");
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentName_AreNotEqual()
    {
        var a = new TrueNasAppRelease("test1", true, false, "host", 3724, null);
        var b = new TrueNasAppRelease("test2", true, false, "host", 3724, null);
        a.Should().NotBe(b);
    }
}
