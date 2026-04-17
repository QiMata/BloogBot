# 0x537AA0 NetClient::ProcessMessage

## Function Signature
```c
int NetClient::ProcessMessage(NetClient* self, void* packetContext, PacketStream* stream)
```

`packetContext` is passed through unchanged to the registered handler. `stream` is the reader object used to extract the opcode and payload.

## Primary Evidence
- `docs/physics/0x537AA0_disasm.txt`
- `docs/physics/opcode_dispatch_table.md`

## Pseudocode
```c
int NetClient::ProcessMessage(NetClient* self, void* packetContext, PacketStream* stream)
{
    uint16_t opcode;

    ++dword_0xC0D46C;                    // 0x537AA3..0x537AB8
    stream->ReadUInt16(&opcode);         // 0x418DB0 at 0x537ABE

    self->vfunc_40(opcode);              // 0x537AC7..0x537ACC

    HandlerFn handler = self->handlerTable[opcode];      // [self + opcode*4 + 0x74]
    if (handler != nullptr)
    {
        void* context = self->handlerContextTable[opcode]; // [self + opcode*4 + 0xD64]

        // Call convention recovered from registers / stack setup:
        //   ecx = context
        //   edx = opcode
        //   push packetContext
        //   push stream
        handler(context, opcode, packetContext, stream);
        return 1;
    }

    stream->vfunc_18();                  // discard / skip unknown payload
    return 1;
}
```

## Key Findings
- `0x537AA0` does not contain a switch or jump table. The dispatch is table-driven through two parallel arrays.
- `0x537ACF` proves the handler pointer table lives at `self + 0x74 + opcode*4`.
- `0x537ADC` proves the handler context table lives at `self + 0xD64 + opcode*4`.
- The registered handler receives `ecx=context` and `edx=opcode`, with `packetContext` and `stream` pushed on the stack at `0x537ADA..0x537AE5`.
- If no handler is registered, the fallback is `stream->vfunc[0x18/4]` at `0x537AEE..0x537AF2`. There is no synthetic default handler in `ProcessMessage` itself.
- The high-level movement registrations in `opcode_dispatch_table.md` show that `0x603BB0` and `0x603F90` are wrapper handlers. The real per-opcode work happens below them in `0x601580` and `0x602780`.
