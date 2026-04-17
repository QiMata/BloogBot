from __future__ import annotations

import argparse
import re
import struct
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from capstone import Cs, CS_ARCH_X86, CS_MODE_32


IMAGE_BASE = 0x400000
DEFAULT_WOW_EXE = Path(r"D:\World of Warcraft\WoW.exe")
REGISTER_DIRECT = 0x537A60
CLEAR_DIRECT = 0x537A80
REGISTER_WRAPPER = 0x5AB650
CLEAR_WRAPPER = 0x5AB670


@dataclass(frozen=True)
class Section:
    name: str
    virtual_address: int
    virtual_size: int
    raw_pointer: int
    raw_size: int


@dataclass(frozen=True)
class Instruction:
    address: int
    mnemonic: str
    op_str: str
    size: int
    bytes_: bytes

    @property
    def text(self) -> str:
        if self.op_str:
            return f"{self.mnemonic} {self.op_str}"
        return self.mnemonic


class WowExe:
    def __init__(self, image: bytes, sections: list[Section]) -> None:
        self.image = image
        self.sections = sections
        self.md = Cs(CS_ARCH_X86, CS_MODE_32)
        self.md.detail = False

    @classmethod
    def load(cls, path: Path) -> "WowExe":
        image = path.read_bytes()
        pe_header_offset = struct.unpack_from("<I", image, 0x3C)[0]
        section_count = struct.unpack_from("<H", image, pe_header_offset + 6)[0]
        optional_header_size = struct.unpack_from("<H", image, pe_header_offset + 20)[0]
        section_table_offset = pe_header_offset + 24 + optional_header_size
        sections: list[Section] = []
        for i in range(section_count):
            offset = section_table_offset + (40 * i)
            name = image[offset:offset + 8].rstrip(b"\0").decode("ascii")
            virtual_size, virtual_address, raw_size, raw_pointer = struct.unpack_from("<IIII", image, offset + 8)
            sections.append(Section(name, virtual_address, virtual_size, raw_pointer, raw_size))
        return cls(image, sections)

    def _find_section(self, virtual_address: int) -> Section:
        rva = virtual_address - IMAGE_BASE
        for section in self.sections:
            span = max(section.virtual_size, section.raw_size)
            if section.virtual_address <= rva < section.virtual_address + span:
                return section
        raise ValueError(f"VA 0x{virtual_address:08X} is outside every section")

    def read_bytes(self, virtual_address: int, count: int) -> bytes:
        section = self._find_section(virtual_address)
        rva = virtual_address - IMAGE_BASE
        virtual_offset = rva - section.virtual_address
        data = bytearray()
        for i in range(count):
            current_offset = virtual_offset + i
            if current_offset >= section.virtual_size:
                break
            if current_offset >= section.raw_size:
                data.append(0)
                continue
            data.append(self.image[section.raw_pointer + current_offset])
        return bytes(data)

    def disasm_range(self, start: int, end: int) -> list[Instruction]:
        data = self.read_bytes(start, end - start)
        instructions: list[Instruction] = []
        for insn in self.md.disasm(data, start):
            if insn.address >= end:
                break
            instructions.append(
                Instruction(
                    address=insn.address,
                    mnemonic=insn.mnemonic,
                    op_str=insn.op_str,
                    size=insn.size,
                    bytes_=bytes(insn.bytes),
                )
            )
        return instructions

    def call_sites(self, target: int) -> list[int]:
        hits: list[int] = []
        limit = len(self.image) - 5
        for offset in range(limit):
            if self.image[offset] != 0xE8:
                continue
            rel = struct.unpack_from("<i", self.image, offset + 1)[0]
            va = IMAGE_BASE + offset
            dest = (va + 5 + rel) & 0xFFFFFFFF
            if dest == target:
                hits.append(va)
        return hits

    def find_bytes(self, needle: bytes) -> list[int]:
        hits: list[int] = []
        start = 0
        while True:
            offset = self.image.find(needle, start)
            if offset < 0:
                return hits
            hits.append(IMAGE_BASE + offset)
            start = offset + 1

    def section_for_va(self, virtual_address: int) -> Section:
        return self._find_section(virtual_address)

    def is_text_va(self, virtual_address: int) -> bool:
        try:
            return self.section_for_va(virtual_address).name == ".text"
        except ValueError:
            return False


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--wow-exe", type=Path, default=DEFAULT_WOW_EXE)
    subparsers = parser.add_subparsers(dest="command", required=True)

    disasm_parser = subparsers.add_parser("disasm")
    disasm_parser.add_argument("--start", type=lambda value: int(value, 0), required=True)
    disasm_parser.add_argument("--end", type=lambda value: int(value, 0), required=True)
    disasm_parser.add_argument("--show-bytes", action="store_true")

    subparsers.add_parser("scan-registrations")

    find_string_parser = subparsers.add_parser("find-string")
    find_string_parser.add_argument("--text", required=True)

    find_dword_parser = subparsers.add_parser("find-dword")
    find_dword_parser.add_argument("--value", type=lambda value: int(value, 0), required=True)
    return parser.parse_args()


def format_disasm(instructions: Iterable[Instruction], show_bytes: bool = False) -> str:
    lines: list[str] = []
    for insn in instructions:
        if show_bytes:
            byte_text = insn.bytes_.hex()
            lines.append(f"0x{insn.address:08X}: {byte_text:<20} {insn.mnemonic:<8} {insn.op_str}".rstrip())
        else:
            lines.append(f"0x{insn.address:08X}: {insn.mnemonic:<8} {insn.op_str}".rstrip())
    return "\n".join(lines)


def parse_push_immediate(text: str) -> int | None:
    match = re.fullmatch(r"push 0x([0-9a-fA-F]+)", text)
    if match:
        return int(match.group(1), 16)
    match = re.fullmatch(r"push ([0-9]+)", text)
    if match:
        return int(match.group(1), 10)
    return None


def value_from_register(history: list[Instruction], register: str) -> str | None:
    register = register.lower()
    for insn in reversed(history):
        text = insn.text.lower()
        if text == f"xor {register}, {register}":
            return "0"
        match = re.fullmatch(rf"mov {register}, (.+)", text)
        if match:
            return match.group(1)
    return None


def candidate_histories(exe: WowExe, call_site: int, max_back: int) -> list[list[Instruction]]:
    candidates: list[list[Instruction]] = []
    for back in range(6, max_back + 1):
        start = max(IMAGE_BASE, call_site - back)
        history = exe.disasm_range(start, call_site)
        if not history:
            continue
        end = history[-1].address + history[-1].size
        if end == call_site:
            candidates.append(history)
    return candidates


def score_wrapper_history(history: list[Instruction]) -> int:
    score = 0
    opcode = value_from_register(history, "ecx")
    handler = value_from_register(history, "edx")
    if opcode and opcode.startswith("0x"):
        score += 10
    if handler and handler.startswith("0x"):
        score += 10
    push_count = 0
    for insn in reversed(history):
        if insn.mnemonic == "push":
            push_count += 1
            if push_count == 1 and parse_push_immediate(insn.text) is not None:
                score += 5
                break
    return score


def score_direct_history(history: list[Instruction]) -> int:
    score = 0
    pushes = 0
    for insn in reversed(history):
        if insn.mnemonic != "push":
            continue
        pushes += 1
        if parse_push_immediate(insn.text) is not None:
            score += 5
        if pushes == 3:
            break
    if pushes == 3:
        score += 10
    return score


def select_history(exe: WowExe, call_site: int, wrapper: bool) -> list[Instruction]:
    candidates = candidate_histories(exe, call_site, max_back=0x80)
    if not candidates:
        return []
    scorer = score_wrapper_history if wrapper else score_direct_history
    return max(candidates, key=scorer)


def infer_registration(call_site: int, history: list[Instruction], wrapper: bool) -> dict[str, str]:
    record: dict[str, str] = {
        "call_site": f"0x{call_site:08X}",
        "kind": "wrapper" if wrapper else "direct",
    }
    if wrapper:
        context = None
        for insn in reversed(history):
            if insn.mnemonic == "push":
                context = insn.op_str
                break
        record["opcode"] = value_from_register(history, "ecx") or "?"
        record["handler"] = value_from_register(history, "edx") or "?"
        record["context"] = context or "?"
    else:
        pushes: list[str] = []
        for insn in reversed(history):
            if insn.mnemonic == "push":
                pushes.append(insn.op_str)
                if len(pushes) == 3:
                    break
        if len(pushes) == 3:
            record["opcode"] = pushes[0]
            record["handler"] = pushes[1]
            record["context"] = pushes[2]
        else:
            record["opcode"] = "?"
            record["handler"] = "?"
            record["context"] = "?"
    return record


def scan_registrations(exe: WowExe) -> str:
    sites: list[tuple[int, bool, str]] = []
    for target, wrapper, action in (
        (REGISTER_DIRECT, False, "register"),
        (REGISTER_WRAPPER, True, "register"),
        (CLEAR_DIRECT, False, "clear"),
        (CLEAR_WRAPPER, True, "clear"),
    ):
        for site in exe.call_sites(target):
            sites.append((site, wrapper, action))
    sites.sort()

    lines = []
    for site, wrapper, action in sites:
        history = select_history(exe, site, wrapper)
        record = infer_registration(site, history, wrapper)
        lines.append(
            "\t".join(
                [
                    action,
                    record["call_site"],
                    record["kind"],
                    record["opcode"],
                    record["handler"],
                    record["context"],
                ]
            )
        )
    header = "action\tcall_site\tkind\topcode\thandler\tcontext"
    return "\n".join([header, *lines])


def main() -> None:
    args = parse_args()
    exe = WowExe.load(args.wow_exe)
    if args.command == "disasm":
        print(format_disasm(exe.disasm_range(args.start, args.end), show_bytes=args.show_bytes))
    elif args.command == "scan-registrations":
        print(scan_registrations(exe))
    elif args.command == "find-string":
        for address in exe.find_bytes(args.text.encode("ascii") + b"\0"):
            print(f"0x{address:08X}")
    elif args.command == "find-dword":
        for address in exe.find_bytes(struct.pack("<I", args.value & 0xFFFFFFFF)):
            print(f"0x{address:08X}")
    else:
        raise ValueError(args.command)


if __name__ == "__main__":
    main()
