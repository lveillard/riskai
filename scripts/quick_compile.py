"""Fast compile check for the RiskAI assemblies without launching Unity.

Uses the Roslyn compiler bundled with the Unity editor and the references
listed in the Unity-generated .csproj files. Source files are globbed from
each asmdef folder, so new files are picked up without regenerating projects.
Usage: python scripts/quick_compile.py [Core Runtime Tests PlayTests Editor]

The editor is resolved from RiskAI/ProjectSettings/ProjectVersion.txt under the
Unity Hub editor folder; set RISKAI_UNITY_EDITOR to override it. Exit codes:
0 compiled, 1 compile errors, 2 missing editor/.csproj/Library prerequisites.
"""
import json, os, re, subprocess, sys, tempfile, pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent / "RiskAI"
HUB_EDITORS = pathlib.Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Unity" / "Hub" / "Editor"


def editor_version():
    text = (ROOT / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8")
    match = re.search(r"^m_EditorVersion:\s*(\S+)", text, re.MULTILINE)
    if not match:
        sys.exit("quick_compile: m_EditorVersion missing from RiskAI/ProjectSettings/ProjectVersion.txt")
    return match.group(1)


def editor_data():
    """Editor Data folder: RISKAI_UNITY_EDITOR (editor folder, its Data folder or Unity.exe) or the Hub default."""
    override = os.environ.get("RISKAI_UNITY_EDITOR")
    if override:
        path = pathlib.Path(override)
        if path.suffix.lower() == ".exe":
            path = path.parent
        return path if path.name.lower() == "data" else path / "Data"
    return HUB_EDITORS / editor_version() / "Editor" / "Data"


EDITOR = editor_data()
DOTNET = EDITOR / "NetCoreRuntime" / "dotnet.exe"
CSC = EDITOR / "DotNetSdkRoslyn" / "csc.dll"
SCRIPTS = ROOT / "Library" / "ScriptAssemblies"


def check_prerequisites(names):
    problems = []
    for tool in (DOTNET, CSC):
        if not tool.exists():
            problems.append(f"missing {tool} (install the editor from ProjectVersion.txt via Unity Hub, "
                            "or set RISKAI_UNITY_EDITOR to the editor folder)")
    missing = [f"{name}.csproj" for name in names if not (ROOT / f"{name}.csproj").exists()]
    if missing:
        problems.append("missing " + ", ".join(missing) + " in RiskAI/")
    if not SCRIPTS.is_dir():
        problems.append("missing RiskAI/Library/ScriptAssemblies")
    if not problems:
        return
    print("quick_compile: cannot run:", file=sys.stderr)
    for problem in problems:
        print("  - " + problem, file=sys.stderr)
    if missing or not SCRIPTS.is_dir():
        print("  Open the project in Unity once (scripts/Unity.ps1 -Action Open) so it generates Library/ "
              "and the .csproj files (Preferences > External Tools > Generate .csproj files).", file=sys.stderr)
    sys.exit(2)
OUT = pathlib.Path(tempfile.gettempdir()) / f"riskai-quick-compile-{os.getpid()}"

ASSEMBLIES = [
    ("Core", "RiskAI.Core", "Assets/RiskAI/Scripts/Core", []),
    ("Runtime", "RiskAI.Runtime", "Assets/RiskAI/Scripts", ["RiskAI.Core"]),
    ("Tests", "RiskAI.Tests", "Assets/RiskAI/Tests/Editor", ["RiskAI.Core", "RiskAI.Runtime"]),
    ("PlayTests", "RiskAI.PlayTests", "Assets/RiskAI/Tests/PlayMode", ["RiskAI.Core", "RiskAI.Runtime"]),
    ("Editor", "RiskAI.Editor", "Assets/RiskAI/Editor", ["RiskAI.Core", "RiskAI.Runtime"]),
]


def sources(folder: pathlib.Path):
    result = []
    for dirpath, dirnames, filenames in os.walk(folder):
        here = pathlib.Path(dirpath)
        if here != folder and any(f.endswith(".asmdef") for f in os.listdir(here)):
            dirnames.clear()
            continue
        result += [str(here / f) for f in filenames if f.endswith(".cs")]
    return result


def compile_one(short, name, folder, internal_refs):
    csproj = (ROOT / f"{name}.csproj").read_text(encoding="utf-8")
    refs = [h if os.path.isabs(h) else str(ROOT / h) for h in re.findall(r"<HintPath>(.*?)</HintPath>", csproj)]
    asmdef = next((ROOT / folder).glob("*.asmdef"))
    for ref in json.loads(asmdef.read_text(encoding="utf-8")).get("references", []):
        if ref not in internal_refs:
            for candidate in (ref, {"Unity.ugui": "UnityEngine.UI"}.get(ref, ref)):
                if (SCRIPTS / f"{candidate}.dll").exists():
                    refs.append(str(SCRIPTS / f"{candidate}.dll"))
    refs += re.findall(r'<Reference Include="([A-Z]:\\[^"]+\.dll)"', csproj)
    analyzers = re.findall(r'<Analyzer Include="([^"]+)"', csproj)
    for proj in re.findall(r'<ProjectReference Include="([^"]+)\.csproj"', csproj):
        if proj in internal_refs:
            refs.append(str(OUT / f"{proj}.dll"))
        else:
            dll = SCRIPTS / f"{proj}.dll"
            if dll.exists():
                refs.append(str(dll))
    defines = re.search(r"<DefineConstants>(.*?)</DefineConstants>", csproj).group(1)
    lang = re.search(r"<LangVersion>(.*?)</LangVersion>", csproj).group(1)
    OUT.mkdir(exist_ok=True)
    rsp = OUT / f"{name}.rsp"
    lines = ["-target:library", "-nologo", "-nostdlib+", "-unsafe", "-nowarn:1701,1702,0618,0649,0169,0414,0219",
             f"-langversion:{lang}", f"-define:{defines}", f"-out:{OUT / (name + '.dll')}"]
    seen = set()
    for r in refs:
        key = os.path.basename(r).lower()
        if key in seen or not os.path.exists(r):
            continue
        seen.add(key)
        lines.append(f'-r:"{r}"')
    lines += [f'-analyzer:"{a}"' for a in analyzers if os.path.exists(a)]
    lines += [f'"{s}"' for s in sources(ROOT / folder)]
    rsp.write_text("\n".join(lines), encoding="utf-8")
    proc = subprocess.run([str(DOTNET), str(CSC), f"@{rsp}"], capture_output=True, text=True)
    errors = [l for l in proc.stdout.splitlines() if ": error " in l]
    print(f"[{short}] {'OK' if proc.returncode == 0 else 'FAILED'} ({len(errors)} errors)")
    for line in errors[:60]:
        print("  " + line.replace(str(ROOT) + os.sep, ""))
    return proc.returncode == 0


if __name__ == "__main__":
    wanted = set(sys.argv[1:]) or {a[0] for a in ASSEMBLIES}
    unknown = wanted - {a[0] for a in ASSEMBLIES}
    if unknown:
        sys.exit(f"quick_compile: unknown assembly {', '.join(sorted(unknown))}; choose from "
                 + " ".join(a[0] for a in ASSEMBLIES))
    last = max(i for i, a in enumerate(ASSEMBLIES) if a[0] in wanted)
    check_prerequisites([a[1] for a in ASSEMBLIES[:last + 1]])
    ok = True
    for short, name, folder, internal in ASSEMBLIES:
        if short in wanted or any(short == dep.split(".")[-1] for dep in []):
            ok = compile_one(short, name, folder, internal) and ok
        elif any(w in [a[0] for a in ASSEMBLIES[ASSEMBLIES.index((short, name, folder, internal)) + 1:]] for w in wanted):
            # dependencies of a later requested assembly must be rebuilt first
            ok = compile_one(short, name, folder, internal) and ok
    sys.exit(0 if ok else 1)
