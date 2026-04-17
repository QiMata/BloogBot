import idaapi
import idautils
import idc


PATTERNS = (
    "CGObject",
    "CGUnit",
    "CGPlayer",
    "vftable",
    "vtbl",
)


def main() -> None:
    for ea, name in idautils.Names():
        if any(pattern.lower() in name.lower() for pattern in PATTERNS):
            print(f"0x{ea:08X}\t{name}")
    idaapi.qexit(0)


if __name__ == "__main__":
    main()
