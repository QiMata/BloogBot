using System;
using System.IO;
using Xunit.Abstractions;

namespace Tests.Infrastructure;

/// <summary>
/// Wraps an <see cref="ITestOutputHelper"/> to write test output to both
/// the xUnit console AND a per-test-class log file on disk.
///
/// Log files are written to TestResults/LiveLogs/{ClassName}.log and are
/// overwritten each time SetOutput is called for that class (i.e. each test run).
/// </summary>
public sealed class DualOutputHelper : ITestOutputHelper, IDisposable
{
    private readonly ITestOutputHelper _inner;
    private readonly StreamWriter? _fileWriter;
    private readonly object _lock = new();

    public DualOutputHelper(ITestOutputHelper inner, string logPath)
    {
        _inner = inner;
        try
        {
            var dir = Path.GetDirectoryName(logPath);
            if (dir != null) Directory.CreateDirectory(dir);
            _fileWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
            _fileWriter.WriteLine($"=== Test log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        }
        catch
        {
            // If file creation fails, continue with xUnit output only
        }
    }

    public void WriteLine(string message)
    {
        _inner.WriteLine(message);
        WriteToFile(message);
    }

    public void WriteLine(string format, params object[] args)
    {
        _inner.WriteLine(format, args);
        WriteToFile(string.Format(format, args));
    }

    private void WriteToFile(string message)
    {
        try
        {
            lock (_lock)
            {
                _fileWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            }
        }
        catch { /* swallow I/O errors */ }
    }

    public void Dispose()
    {
        try { _fileWriter?.Dispose(); } catch { }
    }
}
