using RecordedTests.Shared.Abstractions.I;
using System;

namespace RecordedTests.Shared.Abstractions;

public sealed class NullTestLogger : ITestLogger
{
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message, Exception? ex = null) { }
}