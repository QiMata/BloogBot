# CGObject_C / CGUnit_C / CGPlayer_C Vtable Notes

## Primary Evidence
- `0x4686F0` constructor-like initializer
- `0x613B40` destructor-like cleanup
- `find-dword 0x803640`
- binary string scan for `CGObject_C`, `CGUnit_C`, `CGPlayer_C`, and decorated RTTI names

## Confirmed Base Vtable
- `0x468702` pushes RTTI string `0x836ACC`, which resolves to `.?AVCGObject_C@@`.
- `0x46878A` writes vfptr `0x803640` into the newly-built object.
- `0x613B49` writes the same vfptr `0x803640` back during cleanup.
- `find-dword 0x803640` only produced three references in this scan: `0x46878C`, `0x613AB3`, and `0x613B4B`.

## Class-Name Evidence
- Undecorated strings exist for all three names:
  - `CGPlayer_C` at `0x836778`
  - `CGUnit_C` at `0x836784`
  - `CGObject_C` at `0x8367AC`
- Decorated RTTI evidence is asymmetric:
  - `.?AVCGObject_C@@` exists at `0x836ACC`
  - no decorated RTTI string was recovered for `CGUnit_C`
  - no decorated RTTI string was recovered for `CGPlayer_C`

## Working Conclusion
- The binary evidence currently proves one concrete vfptr with confidence: `CGObject_C` uses `0x803640`.
- Distinct `CGUnit_C` and `CGPlayer_C` vfptr writes are not yet proven by this pass.
- That means the original P2.1.5 assumption ("three distinct vtables ready to list") is contradicted by the evidence gathered so far. The safe statement is:
  - do not assume a separate `CGUnit_C` or `CGPlayer_C` vfptr until a real write site is recovered
  - keep the packet-parity work anchored to the confirmed `CGObject_C` base object plus field / type evidence from later object-layout work
