using FluentAssertions;
using WWoW.RecordedTests.Shared;

namespace WWoW.RecordedTests.Shared.Tests;

public class GmCommandExecutionResultTests
{
    [Fact]
    public void Succeeded_HasSuccessTrue()
    {
        GmCommandExecutionResult.Succeeded.Success.Should().BeTrue();
    }

    [Fact]
    public void Succeeded_HasNullErrorMessage()
    {
        GmCommandExecutionResult.Succeeded.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Failed_WithMessage_HasSuccessFalse()
    {
        var result = GmCommandExecutionResult.Failed("Something went wrong");
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Failed_WithMessage_PreservesMessage()
    {
        var result = GmCommandExecutionResult.Failed("Something went wrong");
        result.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public void Failed_WithNull_UsesDefaultMessage()
    {
        var result = GmCommandExecutionResult.Failed(null);
        result.ErrorMessage.Should().Be("GM command execution failed.");
    }

    [Fact]
    public void Failed_WithEmptyString_UsesDefaultMessage()
    {
        var result = GmCommandExecutionResult.Failed("");
        result.ErrorMessage.Should().Be("GM command execution failed.");
    }

    [Fact]
    public void Failed_WithWhitespace_UsesDefaultMessage()
    {
        var result = GmCommandExecutionResult.Failed("   ");
        result.ErrorMessage.Should().Be("GM command execution failed.");
    }

    [Fact]
    public void Failed_NoArgs_UsesDefaultMessage()
    {
        var result = GmCommandExecutionResult.Failed();
        result.ErrorMessage.Should().Be("GM command execution failed.");
    }

    [Fact]
    public void DefaultConstructor_HasSuccessFalse()
    {
        var result = new GmCommandExecutionResult();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Succeeded_Equality_IsConsistent()
    {
        var a = GmCommandExecutionResult.Succeeded;
        var b = GmCommandExecutionResult.Succeeded;
        a.Should().Be(b);
    }

    [Fact]
    public void Failed_DifferentMessages_AreNotEqual()
    {
        var a = GmCommandExecutionResult.Failed("error A");
        var b = GmCommandExecutionResult.Failed("error B");
        a.Should().NotBe(b);
    }
}
