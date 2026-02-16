namespace WWoW.RecordedTests.Shared.Abstractions.I;

public interface ITestLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}