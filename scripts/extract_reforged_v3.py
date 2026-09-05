"""Extract only rules/metadata from the editable Risk Reforged MPQ using StormLib."""
from pathlib import Path
import ctypes, hashlib

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "references/maps/Risk_Reforged_v3.0_by_Saran_OPEN_SOURCE.w3x"
OUT = ROOT / "references/maps/reforged-v3-source"
ALLOWED = {"war3map.j", "war3map.wts", "war3map.w3i", "war3map.w3e", "war3map.w3r", "war3map.w3u", "war3map.w3t", "war3map.w3a", "war3map.w3b", "war3map.w3h", "war3map.w3q", "war3map.wtg", "war3map.wct", "war3map.doo", "war3mapUnits.doo", "(listfile)"}

def main():
    print("sha256", hashlib.sha256(SRC.read_bytes()).hexdigest())
    dll = ROOT / ".tools/stormlib/x64/StormLib.dll"
    if not dll.exists():
        print("Missing official StormLib DLL:", dll); return 2
    storm = ctypes.WinDLL(str(dll)); Handle = ctypes.c_void_p
    storm.SFileOpenArchive.argtypes=[ctypes.c_wchar_p,ctypes.c_uint,ctypes.c_uint,ctypes.POINTER(Handle)]; storm.SFileOpenArchive.restype=ctypes.c_bool
    storm.SFileOpenFileEx.argtypes=[Handle,ctypes.c_char_p,ctypes.c_uint,ctypes.POINTER(Handle)]; storm.SFileOpenFileEx.restype=ctypes.c_bool
    storm.SFileGetFileSize.argtypes=[Handle,ctypes.POINTER(ctypes.c_uint)]; storm.SFileGetFileSize.restype=ctypes.c_uint
    storm.SFileReadFile.argtypes=[Handle,ctypes.c_void_p,ctypes.c_uint,ctypes.POINTER(ctypes.c_uint),ctypes.c_void_p]; storm.SFileReadFile.restype=ctypes.c_bool
    storm.SFileCloseFile.argtypes=[Handle]
    storm.SFileCloseArchive.argtypes=[Handle]; storm.SFileCloseArchive.restype=ctypes.c_bool
    archive = Handle()
    if not storm.SFileOpenArchive(str(SRC), 0, 0x100, ctypes.byref(archive)):
        print("StormLib could not open archive"); return 2
    OUT.mkdir(parents=True, exist_ok=True)
    for name in sorted(ALLOWED):
        handle = Handle()
        if not storm.SFileOpenFileEx(archive, name.encode(), 0, ctypes.byref(handle)): continue
        high=ctypes.c_uint(); size=storm.SFileGetFileSize(handle,ctypes.byref(high)); data=ctypes.create_string_buffer(size); read=ctypes.c_uint()
        if storm.SFileReadFile(handle,data,size,ctypes.byref(read),None):
            (OUT / Path(name).name).write_bytes(data.raw[:read.value]); print("extracted",name)
        storm.SFileCloseFile(handle)
    storm.SFileCloseArchive(archive)
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
