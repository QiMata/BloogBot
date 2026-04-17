# Packet ACK Timing

This note answers P2.3 Q1-Q5 from `docs/WOW_EXE_PACKET_PARITY_PLAN.md` using the P2.1 disassembly set.

## Q1. Knockback ACK timing

WoW.exe does not emit `CMSG_MOVE_KNOCK_BACK_ACK` from the first-stage `SMSG_MOVE_KNOCK_BACK` handler. The inbound path is `0x603F90 -> 0x602780 -> 0x602670 -> 0x617A30 -> 0x6177A0`, and `0x602670` only prepares movement state, reads the four knockback values, and stages slot `0x1C` into the pending movement queue. There is no call to `0x600A30`, `0x60E0A0`, or `0x5AB630` anywhere in that first-stage path. The binary-backed answer is therefore: the ACK is deferred until after the knockback has been staged into the later queue/apply path, not sent synchronously while parsing `SMSG_MOVE_KNOCK_BACK`. Relevant VAs: `0x602670`, `0x617A30`, `0x6177A0`.

## Q2. Speed-change ACK timing

WoW.exe also defers the speed-change ACK family. `SMSG_FORCE_*_SPEED_CHANGE` packets enter through `0x603F90 -> 0x602780`, but the per-opcode leaves immediately hand off to queue helpers (`0x619500`, `0x619590`, `0x619620`, `0x6196B0`, `0x619740`, `0x6197D0`) that package slots `0x14` through `0x19` through `0x6176A0`. The actual local mutation is separated into the nearby apply helpers (`0x619550`, `0x6195E0`, `0x619670`, `0x619700`, `0x619790`, `0x619820`), each gated by `0x618F20` before calling the concrete setter at `0x7C70xx` / `0x7C6FF0`. Because the first-stage handler never reaches a send helper, WoW.exe's answer is "not synchronous in the packet leaf; deferred to the later queued apply/ACK phase." Relevant VAs: `0x602780`, `0x619500`, `0x618F20`.

## Q3. Teleport ACK timing and gate

WoW.exe splits local teleport application from outbound `MSG_MOVE_TELEPORT_ACK`. `MSG_MOVE_TELEPORT` (`0x0C5`) reaches `0x602F90 -> 0x6186B0` and does not send. The outbound `MSG_MOVE_TELEPORT_ACK` path lives in `0x602FB0`: after parsing full movement state, it branches on `0x468570`. When that gate returns zero, WoW.exe runs `0x60E990(1, 0)` and `0x618720`; when it returns nonzero, it builds opcode `0x0C7` at `0x603036` and sends via `0x5AB630` at `0x60308D`. The confirmed answer is therefore: teleport ACK is deferred behind the internal `0x468570` readiness gate and is not emitted from the inbound teleport leaf itself. Relevant VAs: `0x602F90`, `0x602FB0`, `0x60308D`.

## Q4. Worldport ACK timing

`MSG_MOVE_WORLDPORT_ACK` is emitted from the deferred `SMSG_NEW_WORLD` callback, not from the first `SMSG_NEW_WORLD` parse and not from a later physics tick. `0x401B00` stores the new world coordinates and schedules `0x401BC0` through `0x4200A0`; the actual send block is `0x401CA5..0x401CF4` inside `0x401BC0`. The login-verify path (`0x401DE0`) calls the same callback body directly with `edx = 0`, which skips the `0x0DC` send block. The binary-backed answer is: WoW.exe ties `MSG_MOVE_WORLDPORT_ACK` to the scheduled new-world transition callback. Relevant VAs: `0x401B00`, `0x401BC0`, `0x401CA5`, `0x401DE0`.

## Q5. Monster-move / spline ACK behavior

Current binary evidence says WoW.exe does not ACK `SMSG_MONSTER_MOVE`, `SMSG_MONSTER_MOVE_TRANSPORT`, or the `SMSG_SPLINE_*` families. In the static registration map, `SMSG_MONSTER_MOVE(_TRANSPORT)` binds to `0x603F00`, spline speed changes bind to `0x603C10`, and spline flag/root changes bind to `0x603C80`. Those families do not route through `0x603F90 -> 0x602780`, which is the only movement wrapper family that P2.1 tied to outbound ACK-producing queue/apply helpers. There is also no paired outbound `CMSG_*_ACK` registration for monster/spline movement in `opcode_dispatch_table.md`. This is an inference from dispatch separation rather than a dedicated `0x603F00` / `0x603C10` / `0x603C80` wrapper disassembly, but the current answer is "no ACK." Relevant VAs: `0x603F00`, `0x603C10`, `0x603C80`.
