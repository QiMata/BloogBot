# Raw Position / Flight ACKs

## Conclusion

`MSG_MOVE_SET_RAW_POSITION_ACK (0x00E0)` and `CMSG_MOVE_FLIGHT_ACK (0x0340)` are not active WoW.exe 1.12.1 ACK surfaces.

Close P2.7 `G6` and `G7` as **not applicable in WoW.exe 1.12.1** rather than implementing synthetic sends in BG or recording them in the FG ACK corpus.

## Static Evidence

- `docs/physics/opcode_dispatch_table.md` already records **no static registration** for:
  - `0x00E0` `MSG_MOVE_SET_RAW_POSITION_ACK`
  - `0x00E1` `CMSG_MOVE_SET_RAW_POSITION`
  - `0x033E` `SMSG_MOVE_SET_FLIGHT`
  - `0x033F` `SMSG_MOVE_UNSET_FLIGHT`
  - `0x0340` `CMSG_MOVE_FLIGHT_ACK`
- A `0x520000..0x620000` immediate sweep found:
  - no `push 0x340`
  - no `push 0x33e`
  - one `push 0x33f` at `0x604999`
  - three `push 0xe0` sites at `0x5F34B9`, `0x5F34D8`, and `0x60C121`
- The only `0x33F` immediate site is the local wrapper at `0x604990`, which calls `0x468460` and then `0x60ABE0`. No send helper such as `0x600A30`, `0x60E0A0`, or `0x5AB630` appears in that block.
- The `0x00E0` sites at `0x5F34B9`, `0x5F34D8`, and `0x60C121` all call `0x496720`, not the outbound movement send helpers.

Inference: the scanned `0x00E0` and `0x033F` callsites are not an outbound ACK path, and there is no matching static registration for the supposed inbound or outbound opcode family in `NetClient::ProcessMessage`.

## Live PacketLogger Evidence (2026-04-17)

All three probes used the existing FG ACK harness with `WWOW_ENABLE_RECORDING_ARTIFACTS=1`, `WWOW_CAPTURE_ACK_CORPUS=1`, and `.debug send opcode` on the live Docker `mangosd` server.

| Probe | Server confirmation | PacketLogger result | ACK result |
| --- | --- | --- | --- |
| `224` / `0x00E0` | `foreground_bot_debug.log` at `17:04:43.676`: `Opcode 224 sent to Gargandurooj` | `packet_logger.log` at `17:04:43.671`: inbound `0x00E0 size=8` | no new fixture, no outbound `0x00E0` line |
| `831` / `0x033F` | `foreground_bot_debug.log` at `17:14:53.422`: `Opcode 831 sent to Gargandurooj` | `packet_logger.log` at `17:14:53.417`: inbound `0x033F size=8` | no new fixture, no outbound `0x0340` line |
| `830` / `0x033E` | `foreground_bot_debug.log` at `17:18:40.852`: `Opcode 830 sent to Gargandurooj` | `packet_logger.log` at `17:18:40.848`: inbound `0x033E size=8` | no new fixture, no outbound `0x0340` line |

For each probe, `AckCaptureTests.Foreground_GmCommand_CapturesConfiguredAckCorpusWhenEnabled` failed its "new ACK fixture appeared" assertion because the FG recorder never saw a matching outbound ACK packet from `WoW.exe NetClient::Send (0x005379A0)`.

After the flight probes, scanning `packet_logger.log` for `0x0340` returned no matches.

## Parity Decision

- Do **not** add BG implementations for `MSG_MOVE_SET_RAW_POSITION_ACK` or `CMSG_MOVE_FLIGHT_ACK`.
- Do **not** treat `0x00E0` or `0x0340` as expected FG ACK-corpus opcodes.
- Keep the parity surface at the 14 ACK families that are actually observed and documented elsewhere in `docs/physics/`.
