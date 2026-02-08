# Recorded Tests End-to-End Checklist

To run the recorded integration tests end-to-end, make sure to supply the following pieces alongside the `WWoW.RecordedTests.Shared` library:

1. **Bot runner factories** that create your foreground and background runners (`IBotRunner`). Reuse the default runners from `Services` or wrap custom implementations with `DelegateBotRunnerFactory`.
2. **A screen recorder factory** that returns an `IScreenRecorder` connected to OBS, ffmpeg, or other tooling instead of the stub recorder.
3. **Server desired states** describing how to prepare the realm before the scenario and how to reset it afterward via `IServerDesiredState`. Use `GmCommandServerDesiredState` when the setup can be expressed as GM commands, or plug in `DelegateServerDesiredState` for custom logic.
4. **Server discovery configuration** listing the TrueNAS (or substitute) releases to poll so the orchestrator knows when the realm is available.
5. **Orchestration options & secrets** such as artifact directories, timeouts, and credentials (TrueNAS API key, recorder access) surfaced via configuration or environment variables.
6. **Automation host & storage** to schedule runs (CI job, agent, etc.) and persist artifacts/logs for review. `FileSystemRecordedTestStorage` copies each run into a durable folder hierarchy and writes metadata for downstream systems.

`RecordedTestRunner` can combine these dependencies via `RecordedTestE2EConfiguration`, simplifying orchestration code once you have real factories, recorder integrations, and server discovery.

Refer to `README.md` in this directory for detailed setup guidance and sample orchestration code.
