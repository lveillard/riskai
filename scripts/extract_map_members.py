"""Extract selected Warcraft III map members with the bundled StormLib DLL."""
from __future__ import annotations

import argparse
import ctypes
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / ".tools/stormlib/x64/StormLib.dll"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("map", type=Path)
    ap.add_argument("output", type=Path)
    args = ap.parse_args()
    storm = ctypes.WinDLL(str(DLL))
    Handle = ctypes.c_void_p
    storm.SFileOpenArchive.argtypes = [ctypes.c_wchar_p, ctypes.c_uint, ctypes.c_uint, ctypes.POINTER(Handle)]
    storm.SFileOpenArchive.restype = ctypes.c_bool
    storm.SFileOpenFileEx.argtypes = [Handle, ctypes.c_char_p, ctypes.c_uint, ctypes.POINTER(Handle)]
    storm.SFileOpenFileEx.restype = ctypes.c_bool
    storm.SFileGetFileSize.argtypes = [Handle, ctypes.POINTER(ctypes.c_uint)]
    storm.SFileGetFileSize.restype = ctypes.c_uint
    storm.SFileReadFile.argtypes = [Handle, ctypes.c_void_p, ctypes.c_uint, ctypes.POINTER(ctypes.c_uint), ctypes.c_void_p]
    storm.SFileReadFile.restype = ctypes.c_bool
    storm.SFileCloseFile.argtypes = [Handle]
    storm.SFileCloseArchive.argtypes = [Handle]
    storm.SFileCloseArchive.restype = ctypes.c_bool
    archive = Handle()
    if not storm.SFileOpenArchive(str(args.map), 0, 0x100, ctypes.byref(archive)):
        raise SystemExit(f"StormLib could not open {args.map}")
    args.output.mkdir(parents=True, exist_ok=True)
    try:
        for name in ("war3mapUnits.doo", "war3map.j", "scripts\\war3map.j", "war3map.wts", "war3map.w3i", "(listfile)"):
            handle = Handle()
            if not storm.SFileOpenFileEx(archive, name.encode(), 0, ctypes.byref(handle)):
                continue
            high = ctypes.c_uint()
            size = storm.SFileGetFileSize(handle, ctypes.byref(high))
            data = ctypes.create_string_buffer(size)
            read = ctypes.c_uint()
            if storm.SFileReadFile(handle, data, size, ctypes.byref(read), None):
                (args.output / Path(name).name).write_bytes(data.raw[: read.value])
                print("extracted", name, read.value)
            storm.SFileCloseFile(handle)
    finally:
        storm.SFileCloseArchive(archive)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
