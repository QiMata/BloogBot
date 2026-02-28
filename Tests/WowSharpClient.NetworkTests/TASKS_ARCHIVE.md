# Task Archive

Completed items moved from TASKS.md.

## Completed 2026-02-28

1. [x] `WSCN-TST-001` Add encrypted `PacketPipeline` receive-state tests (fragmented header/body, remainder carry, and header reset).
   - Added `XorHeaderEncryptor` deterministic test helper (non-`NoEncryption` IEncryptor).
   - 6 new tests: single complete encrypted packet, fragmented header, fragmented body, remainder carry (two packets in one segment), header reset between packets, 3-chunk reassembly.
   - Files: `PacketPipelineTests.cs`.

2. [x] `WSCN-TST-002` Add concurrent send serialization tests for `PacketPipeline` lock behavior.
   - 3 new tests: 10 concurrent sends with serialization check, 20 concurrent sends with payload preservation, send-after-dispose throws.
   - Files: `PacketPipelineTests.cs`.

3. [x] `WSCN-TST-003` Make `ConnectionManager` reconnect cancellation/dispose tests deterministic.
   - Replaced `Task.Delay` waits in existing tests with `TaskCompletionSource` + `WaitAsync(timeout)` pattern.
   - 4 new tests: disconnect-cancels-active-reconnect, dispose-during-backoff-stops-immediately, graceful-disconnect-fires-null, error-disconnect-exhausts-policy.
   - Files: `ConnectionManagerTests.cs`.

4. [x] `WSCN-TST-004` Add `TcpConnection` tests for reconnect semantics and duplicate disconnect-emit guard.
   - 4 new tests: reconnect-while-connected emits disconnect+connect, server-close-then-explicit-disconnect, connect-after-dispose throws, dispose-completes-observables.
   - Files: `TcpConnectionReactiveTests.cs`.

5. [x] `WSCN-TST-005` Add `AuthClient` raw parser boundary tests for unknown-opcode resync and fragmented realm list frames.
   - 5 new tests: single unknown opcode resync, multiple unknown opcodes, fragmented realm list, one-byte-then-rest realm list, fragmented failed challenge.
   - Fixed `CreateMockRealmListResponse()` to use correct vanilla 1.12.1 format (padding=4 bytes, numRealms=1 byte at offset 7).
   - Files: `AuthClientTests.cs`.

6. [x] `WSCN-TST-006` Add `WorldClient` bridge coverage tests for critical opcode registration and exception swallow path.
   - 6 new tests: bridge movement opcodes registered, bridge login/object opcodes registered, bridge-handler-throws pipeline continues, multiple-throws never terminates, attack-swing-errors emit to observable, register-opcode-handler returns stream.
   - Discovery: `SMSG_ATTACKSTART`/`SMSG_ATTACKSTOP` reactive handlers are overwritten by bridge handlers — `AttackStateChanged` subject is dead code for those opcodes.
   - Files: `WorldClientTests.cs`.
