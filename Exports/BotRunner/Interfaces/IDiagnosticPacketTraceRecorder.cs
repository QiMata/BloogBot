namespace BotRunner.Interfaces;

/// <summary>
/// Optional sidecar recorder for diagnostic packet traces tied to Start/StopPhysicsRecording.
/// Implemented by higher layers that can observe live packet traffic.
/// </summary>
public interface IDiagnosticPacketTraceRecorder
{
    void StartRecording(string accountName);
    void StopRecording(string accountName);
}
