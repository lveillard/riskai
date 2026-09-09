#!/usr/bin/env python3
"""Serial Windows player measurements; never builds or opens Unity Editor.

Example (after building, with the Editor and other probes closed):
  python scripts/measure_navigation_ab.py --player Builds/Windows-v0.22/RiskAI.exe --output TestResults/navigation-ab

The controlled cohort isolates sustained land navigation/render load. --advanced
also runs the existing combat/AI probe separately to expose naval planning spikes.
"""
from __future__ import annotations

import argparse
import ctypes
from ctypes import wintypes
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import time

FIELDS = re.compile(r"([A-Za-z_][A-Za-z0-9_.-]*)=(\S+)")
COMPETITORS = {"unity.exe", "unityshadercompiler.exe", "bee_backend.exe", "clang.exe",
               "clang++.exe", "csc.exe", "msbuild.exe", "il2cpp.exe", "riskai.exe",
               "rustc.exe", "cargo.exe", "cl.exe", "link.exe", "lld-link.exe"}


def records(log: str, prefix: str) -> list[dict[str, str]]:
    return [dict(FIELDS.findall(line.split(prefix, 1)[1])) for line in log.splitlines() if prefix in line]


def measurement_window(log: str) -> str:
    lines = []
    measuring = False
    for line in log.splitlines():
        phase = records(line, "RISKAI_PROBE_PHASE ")
        if phase and phase[0].get("phase") == "measurement":
            measuring = True
        if "RISKAI_PROBE_RESULT " in line:
            break
        if measuring:
            lines.append(line)
    return "\n".join(lines)


def compare_runs(runs: list[dict]) -> dict:
    controlled = [run for run in runs if run["scenario"] == "sustained"]
    results = [run.get("result", {}) for run in controlled]
    comparable = len(controlled) >= 2 and all(
        run["exitCode"] == 0 and not run["contentionObserved"] and has_latency_observations(run) and
        result.get("valid") == "True" and result.get("success") == "True" and
        result.get("navBudget") == str(run["budget"]) and
        int(result.get("cohortMinAlive", "0")) >= 800 and int(result.get("movedUnits", "0")) >= 800
        for run, result in zip(controlled, results))
    # A different schedule or fixture invalidates the A/B, even if each arm passed.
    matching = bool(results) and all(results[0].get(key) is not None and
        len({item.get(key) for item in results}) == 1
        for key in ("fixtureHash", "cohort", "rounds", "probeMovesSubmitted"))
    advanced = [run for run in runs if run["scenario"] == "advanced"]
    advanced_valid = bool(advanced) and all(
        run["exitCode"] == 0 and not run["contentionObserved"] and has_latency_observations(run) and
        run.get("result", {}).get("valid") == "True" and
        run["result"].get("success") == "True" and
        run["result"].get("navBudget") == str(run["budget"]) and
        float(run["result"].get("warmupCompletedSimSeconds", "0")) >= 900 and
        float(run["result"].get("measurementElapsedRealSeconds", "0")) >= 90 and
        run["result"].get("rejected") == "0" and run["result"].get("queued") == "0"
        for run in advanced)
    return {"comparable": comparable and matching, "matchingFixtureAndOrders": matching,
            "advancedValidated": advanced_valid,
            "physicalArmValidated": False, "combatSustained800Validated": False,
            "selectionPolicy": "Keep 500 unless a clean matching arm materially reduces route/velocity latency "
                "without regressing frame tails or creating >100ms hitches; confirm a candidate in the advanced combat run. "
                "A single fixed-order sweep is exploratory, not a hardware-wide default certification."}


def has_latency_observations(run: dict) -> bool:
    summary = run.get("latencySummary", {})
    return all(summary.get(key, {}).get("observations", 0) > 0 for key in ("applyToRoute", "submitToVelocity"))


def latency_summary(diagnostics: list[dict], result: dict) -> dict:
    """Combine measured windows by observation count, not by window duration."""
    output = {}
    for label, count_key, metric in (
        ("submitToApply", "commandApplyHuman", "submitApplyHumanActive"),
        ("applyToRoute", "routeReadyHumanObservedCount", "applyRouteReadyHumanActive"),
        ("submitToVelocity", "speedHumanObservedCount", "submitSpeedHumanActive"),
        ("routeToVelocity", "routeReadySpeedHumanPairedCount", "routeReadySpeedHumanActive")):
        samples = [(int(row.get(count_key, "0")), row) for row in diagnostics]
        samples = [(count, row) for count, row in samples if count > 0]
        count = sum(count for count, _ in samples)
        output[label] = {"observations": count,
            "averageMs": round(sum(n * float(row.get(metric + "AvgMs", "0")) for n, row in samples) / count, 3) if count else None,
            "maximumMs": max((float(row.get(metric + "MaxMs", "0")) for _, row in samples), default=None)}
    output["backgroundFrames"] = {key: result.get(key) for key in
        ("measureFrames", "measureFrameAvgMs", "measureFrameMaxMs", "measureOver50ms", "measureOver100ms", "measureOver250ms")}
    return output


class MemoryStatus(ctypes.Structure):
    _fields_ = [("length", wintypes.DWORD), ("load", wintypes.DWORD)] + [
        (name, ctypes.c_ulonglong) for name in
        ("totalPhysical", "availablePhysical", "totalPage", "availablePage", "totalVirtual", "availableVirtual", "extended")]


def environment_sample(player_pid: int | None) -> dict:
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    psapi = ctypes.WinDLL("psapi", use_last_error=True)
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    status = MemoryStatus(); status.length = ctypes.sizeof(status)
    if not kernel.GlobalMemoryStatusEx(ctypes.byref(status)):
        raise ctypes.WinError(ctypes.get_last_error())
    ids = (wintypes.DWORD * 8192)(); needed = wintypes.DWORD()
    if not psapi.EnumProcesses(ids, ctypes.sizeof(ids), ctypes.byref(needed)):
        raise ctypes.WinError(ctypes.get_last_error())
    competing = []
    for pid in ids[:needed.value // ctypes.sizeof(wintypes.DWORD)]:
        if pid == player_pid:
            continue
        handle = kernel.OpenProcess(0x1000, False, pid)
        if not handle:
            continue
        try:
            buffer = ctypes.create_unicode_buffer(32768); length = wintypes.DWORD(len(buffer))
            if kernel.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(length)):
                name = Path(buffer.value).name.lower()
                if name in COMPETITORS or "rift" in name or "probe" in name:
                    competing.append({"pid": pid, "name": name})
        finally:
            kernel.CloseHandle(handle)
    return {"timestamp": time.time(), "freeGiB": status.availablePhysical / 1024 ** 3,
            "competingProcesses": competing}


def run_player(args, budget: int, scenario: str) -> dict:
    log = args.output / f"{scenario}-{budget}.log"
    if log.exists():
        raise FileExistsError(f"Refusing to overwrite evidence: {log}")
    command = [str(args.player), "-logFile", str(log), "-screen-width", "1600", "-screen-height", "900",
               "-screen-fullscreen", "0", "--riskai-play", "--riskai-probe", "--riskai-map", "europe",
               "--riskai-seed", str(args.seed), "--riskai-players", "16", "--riskai-path-budget", str(budget),
               "--riskai-probe-seconds", str(args.seconds)]
    if scenario == "sustained":
        command.append("--riskai-probe-sustained")
    else:
        # Let the human side defend itself during the automated advance to 15 min.
        # The temporary commander is removed before the measured synthetic orders.
        command += ["--riskai-probe-warmup", "900", "--riskai-probe-recruits", "48", "--riskai-probe-warmup-commander"]
    samples = [environment_sample(None)]
    environment_path = args.output / f"{scenario}-{budget}-environment.json"
    # Preserve the actual refusal, including a preflight that starts no player.
    environment_path.write_text(json.dumps(samples, indent=2), encoding="utf-8")
    if samples[0]["competingProcesses"] or samples[0]["freeGiB"] < args.minimum_free_gib:
        raise RuntimeError(f"Measurement not started: competing process or insufficient free memory: {samples[0]}")
    print(f"Starting {scenario}, NavMesh budget {budget}; log {log}", flush=True)
    started = time.monotonic()
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = subprocess.SW_HIDE
    process = subprocess.Popen(command, cwd=args.player.parent, startupinfo=startup)
    try:
        while process.poll() is None:
            if time.monotonic() - started > args.timeout:
                raise TimeoutError(f"Owned player {process.pid} exceeded {args.timeout}s")
            samples.append(environment_sample(process.pid))
            time.sleep(1)
    finally:
        if process.poll() is None:
            process.terminate()
            process.wait(timeout=15)
        environment_path.write_text(json.dumps(samples, indent=2), encoding="utf-8")
    raw = log.read_text(encoding="utf-8", errors="replace")
    result = records(raw, "RISKAI_PROBE_RESULT ")
    contention = any(sample["competingProcesses"] or sample["freeGiB"] < args.minimum_free_gib for sample in samples)
    diagnostics = records(measurement_window(raw), "RuntimeDiagnostics 30s ")
    final = result[-1] if result else {}
    return {"scenario": scenario, "budget": budget, "command": command, "exitCode": process.returncode,
            "windowMode": "hidden-background", "frameSampler": "probe-unscaled-background",
            "log": str(log), "contentionObserved": contention,
            "minimumFreeGiB": min(sample["freeGiB"] for sample in samples),
            "maximumSampleGapSeconds": max((b["timestamp"] - a["timestamp"] for a, b in zip(samples, samples[1:])), default=0),
            "result": final, "startup": records(raw, "RISKAI_STARTUP "),
            "diagnostics": diagnostics, "latencySummary": latency_summary(diagnostics, final),
            "fixture": records(raw, "RISKAI_PROBE_FIXTURE "),
            "cohortSamples": records(raw, "RISKAI_SUSTAINED_SAMPLE ")}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--budgets", type=int, nargs="+", default=[500, 1000, 2000])
    parser.add_argument("--seed", type=int, default=160212)
    parser.add_argument("--seconds", type=int, default=90)
    parser.add_argument("--timeout", type=int, default=900)
    parser.add_argument("--minimum-free-gib", type=float, default=6)
    parser.add_argument("--advanced", action="store_true", help="Also run advanced combat at --advanced-budget after the sweep")
    parser.add_argument("--advanced-only", action="store_true", help="Run only the advanced check without repeating a completed sweep")
    parser.add_argument("--advanced-budget", type=int, default=500)
    args = parser.parse_args()
    if os.name != "nt":
        parser.error("This launcher samples Windows process/memory state; run it on Windows.")
    if args.seconds < 30 or any(b < 100 or b > 2000 for b in args.budgets + [args.advanced_budget]):
        parser.error("Duration must be >=30 and budgets in the engine's 100..2000 range.")
    if (args.advanced or args.advanced_only) and args.seconds < 90:
        parser.error("Advanced validation requires at least 90 measured seconds.")
    if len(set(args.budgets)) != len(args.budgets):
        parser.error("Use unique budgets; use a fresh output directory for repeat sweeps.")
    args.player = args.player.resolve(strict=True); args.output = args.output.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    report_path = args.output / "report.json"
    if report_path.exists():
        parser.error(f"Use a fresh output directory; {report_path} already exists.")
    report = {"schemaVersion": 2, "player": str(args.player),
              "playerExeSha256": hashlib.sha256(args.player.read_bytes()).hexdigest(), "runs": []}
    try:
        for budget in ([] if args.advanced_only else args.budgets):
            report["runs"].append(run_player(args, budget, "sustained"))
            report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
        if args.advanced or args.advanced_only:
            report["runs"].append(run_player(args, args.advanced_budget, "advanced"))
    except (RuntimeError, TimeoutError, OSError) as error:
        report["error"] = {"type": type(error).__name__, "message": str(error)}
    finally:
        report["comparison"] = compare_runs(report["runs"])
        report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    if "error" in report:
        print(json.dumps(report["error"], indent=2))
    print(json.dumps(report["comparison"], indent=2))
    accepted = report["comparison"]["advancedValidated"] if args.advanced_only else report["comparison"]["comparable"]
    if args.advanced:
        accepted = accepted and report["comparison"]["advancedValidated"]
    return 0 if accepted and "error" not in report else 1


if __name__ == "__main__":
    raise SystemExit(main())
