using FluentAssertions;
using System;
using System.IO;

namespace RecordedTests.PathingTests.Tests;

public class ConsoleTestLoggerTests
{
    [Fact]
    public void Info_WritesToConsoleOutput()
    {
        // Arrange
        var logger = new ConsoleTestLogger();
        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            // Act
            logger.Info("Test info message");

            // Assert
            var output = writer.ToString();
            output.Should().Contain("[INFO]");
            output.Should().Contain("Test info message");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Warn_WritesToConsoleOutput()
    {
        // Arrange
        var logger = new ConsoleTestLogger();
        var originalOut = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            // Act
            logger.Warn("Test warning message");

            // Assert
            var output = writer.ToString();
            output.Should().Contain("[WARN]");
            output.Should().Contain("Test warning message");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Error_WritesToConsoleError()
    {
        // Arrange
        var logger = new ConsoleTestLogger();
        var originalError = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            // Act
            logger.Error("Test error message");

            // Assert
            var output = writer.ToString();
            output.Should().Contain("[ERROR]");
            output.Should().Contain("Test error message");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Error_WithException_WritesExceptionDetails()
    {
        // Arrange
        var logger = new ConsoleTestLogger();
        var originalError = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var exception = new InvalidOperationException("Test exception");

            // Act
            logger.Error("Test error message", exception);

            // Assert
            var output = writer.ToString();
            output.Should().Contain("[ERROR]");
            output.Should().Contain("Test error message");
            output.Should().Contain("InvalidOperationException");
            output.Should().Contain("Test exception");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
