using System.Text.Json.Serialization;

namespace LoggingMCPServer.Services;

public class LogEvent
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("level")]
    public string Level { get; set; } = "Info";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("component")]
    public string Component { get; set; } = "Unknown";

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = "";

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    // Additional properties for compatibility
    public string Source { get; set; } = "";
    public string Category { get; set; } = "";
    public Exception? Exception { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public int ProcessId { get; set; }
    public int ThreadId { get; set; }
}
