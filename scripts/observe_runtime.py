#!/usr/bin/env python3
"""Passive reader for RiskAI RuntimeDiagnostics logs. Uses only Python's standard library."""
from __future__ import annotations

import argparse
import json
import math
import os
import re
import sys
import time
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable, List, Optional, Tuple

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_LOG = ROOT / "RiskAI" / "Logs" / "v17-opened.log"
MAX_SNAPSHOT_BYTES = 2 * 1024 * 1024
MAX_POLL_BYTES = 1024 * 1024
MAX_PARTIAL_BYTES = 64 * 1024

DIAGNOSTIC_PREFIX = "RuntimeDiagnostics 30s"
KEY_VALUE_RE = re.compile(r"(?P<key>[A-Za-z_][A-Za-z0-9_.-]*)=(?P<value>\S+)")
REJECT_RE = re.compile(
    r"RISKAI_ORDER_REJECTED: id=(?P<id>\d+) kind=(?P<kind>\w+) "
    r"tick=(?P<tick>\d+) reason=(?P<reason>.*)"
)
ROUTE_RE = re.compile(
    r"RISKAI_ROUTE_BLOCKED: id=(?P<id>\d+) tick=(?P<tick>\d+) "
    r"destination=(?P<destination>.*)"
)


@dataclass
class Diagnostic:
    avg_ms: float
    max_ms: float
    managed_heap_delta_b: int
    gc_gen0: int
    units: int
    sim_time: float
    sim_ticks: int
    commands_pending: int
    commands_applied: int
    commands_rejected: int


@dataclass
class Event:
    kind: str
    detail: dict


def parse_line(line: str) -> Optional[Event]:
    prefix = line.find(DIAGNOSTIC_PREFIX)
    if prefix >= 0:
        fields = {match["key"]: match["value"] for match in KEY_VALUE_RE.finditer(line[prefix + len(DIAGNOSTIC_PREFIX):])}
        try:
            detail = asdict(Diagnostic(
                float(fields.pop("avgMs")), float(fields.pop("maxMs")),
                int(fields.pop("managedHeapDeltaB")), int(fields.pop("gcGen0")),
                int(fields.pop("units")), float(fields.pop("simTime")),
                int(fields.pop("simTicks")), int(fields.pop("commandsPending")),
                int(fields.pop("commandsApplied")), int(fields.pop("commandsRejected")),
            ))
        except (KeyError, ValueError):
            return None
        # RuntimeDiagnostics can append fields without requiring a tool update.
        # Preserve those exact key/value strings in snapshot and JSON Lines output.
        detail.update(fields)
        return Event("diagnostic", detail)
    match = REJECT_RE.search(line)
    if match:
        return Event("order_rejected", match.groupdict())
    match = ROUTE_RE.search(line)
    if match:
        return Event("route_blocked", match.groupdict())
    return None


def decode_log_line(line: bytes) -> str:
    try:
        return line.decode("utf-8")
    except UnicodeDecodeError:
        # Unity Player logs on Windows can contain the active ANSI code page.
        return line.decode("cp1252", errors="replace")


def parse_complete_lines(data: bytes) -> Tuple[List[Event], bytes]:
    """Returns events only for newline-terminated lines, preserving a trailing partial."""
    complete, separator, partial = data.rpartition(b"\n")
    if not separator:
        return [], data
    events: List[Event] = []
    for line in complete.splitlines():
        event = parse_line(decode_log_line(line))
        if event:
            events.append(event)
    return events, partial


def read_tail(path: Path, maximum: int) -> bytes:
    size = path.stat().st_size
    start = max(0, size - maximum)
    with path.open("rb") as handle:
        handle.seek(start)
        data = handle.read(maximum)
    if start:
        newline = data.find(b"\n")
        data = data[newline + 1:] if newline >= 0 else b""
    return data


class LogFollower:
    """Reads appended complete lines and recovers when a writer truncates the log."""

    def __init__(self, path: Path, start_at_end: bool = True):
        self.path = path
        initial = path.stat() if path.exists() else None
        self.offset = initial.st_size if start_at_end and initial else 0
        self.file_identity = self._identity(initial) if initial else None
        self.partial = b""
        self.discarding_partial = False
        self.partial_discarded = False

    @staticmethod
    def _identity(stat_result: os.stat_result) -> Tuple[int, int]:
        return stat_result.st_dev, stat_result.st_ino

    def _consume(self, data: bytes) -> List[Event]:
        """Consume a byte chunk without ever retaining an unbounded line."""
        events: List[Event] = []
        while data:
            if self.discarding_partial:
                newline = data.find(b"\n")
                if newline < 0:
                    return events
                data = data[newline + 1:]
                self.discarding_partial = False

            newline = data.find(b"\n")
            if newline < 0:
                if len(self.partial) + len(data) > MAX_PARTIAL_BYTES:
                    self.partial = b""
                    self.discarding_partial = True
                    self.partial_discarded = True
                else:
                    self.partial += data
                return events

            line = self.partial + data[:newline]
            self.partial = b""
            data = data[newline + 1:]
            if len(line) > MAX_PARTIAL_BYTES:
                self.partial_discarded = True
                continue
            event = parse_line(decode_log_line(line))
            if event:
                events.append(event)
        return events

    def read_new(self) -> Tuple[List[Event], bool, bool]:
        """Returns (events, truncated, capped_append). Missing files are not errors."""
        if not self.path.exists():
            return [], False, False
        stat_result = self.path.stat()
        identity = self._identity(stat_result)
        size = stat_result.st_size
        truncated = size < self.offset or (self.file_identity is not None and identity != self.file_identity)
        if truncated:
            self.offset = 0
            self.partial = b""
            self.discarding_partial = False
        self.file_identity = identity
        self.partial_discarded = False
        unread = size - self.offset
        capped = unread > MAX_POLL_BYTES
        start = size - MAX_POLL_BYTES if capped else self.offset
        with self.path.open("rb") as handle:
            handle.seek(start)
            data = handle.read(MAX_POLL_BYTES if capped else unread)
        self.offset = size
        if capped:
            newline = data.find(b"\n")
            data = data[newline + 1:] if newline >= 0 else b""
            self.partial = b""
            self.discarding_partial = False
        events = self._consume(data)
        return events, truncated, capped


def snapshot(path: Path, last: int) -> Tuple[List[Event], Optional[float], bool]:
    if not path.exists():
        return [], None, False
    data = read_tail(path, MAX_SNAPSHOT_BYTES)
    events, _ = parse_complete_lines(data)
    age = max(0.0, time.time() - path.stat().st_mtime)
    return events, age, path.stat().st_size > MAX_SNAPSHOT_BYTES


def report(path: Path, events: Iterable[Event], age: Optional[float], last: int, capped: bool) -> dict:
    all_events = list(events)
    diagnostics = [event.detail for event in all_events if event.kind == "diagnostic"][-last:]
    rejects = [event.detail for event in all_events if event.kind == "order_rejected"][-last:]
    routes = [event.detail for event in all_events if event.kind == "route_blocked"][-last:]
    return {
        "path": str(path),
        "age_seconds": age,
        "windows": diagnostics,
        "order_rejections": rejects,
        "route_blocks": routes,
        "snapshot_capped": capped,
        "notes": [
            "RuntimeDiagnostics reports 30-second windows. A pending value of zero at a window does not prove there was no queue wait between windows.",
            "The v16 format does not report pause or application focus. Unchanged sim_time can indicate pause, focus loss, startup, or another stopped simulation state.",
        ],
    }


def print_text(payload: dict) -> None:
    age = payload["age_seconds"]
    age_text = "unknown (file missing)" if age is None else f"{age:.1f}s"
    print(f"log: {payload['path']}")
    print(f"file age: {age_text}")
    if payload["snapshot_capped"]:
        print(f"warning: snapshot was limited to the newest {MAX_SNAPSHOT_BYTES // 1024} KiB.")
    windows = payload["windows"]
    if not windows:
        print("no complete RuntimeDiagnostics windows found.")
    else:
        print(f"latest {len(windows)} diagnostic window(s):")
        for item in windows:
            print(
                "  avg={avg_ms:.2f}ms max={max_ms:.2f}ms units={units} sim={sim_time:.1f}s "
                "ticks={sim_ticks} pending={commands_pending} applied={commands_applied} "
                "rejected={commands_rejected} heapDelta={managed_heap_delta_b}B gen0={gc_gen0}".format(**item)
            )
        if len(windows) > 1 and windows[-1]["sim_time"] == windows[0]["sim_time"]:
            print("warning: sim_time did not advance across these windows; pause/focus state is unknown in v16 logs.")
    print(f"recent order rejections: {len(payload['order_rejections'])}; route blocks: {len(payload['route_blocks'])}")
    for item in payload["order_rejections"]:
        print(f"  rejected unit={item['id']} kind={item['kind']} tick={item['tick']}: {item['reason']}")
    for item in payload["route_blocks"]:
        print(f"  route blocked unit={item['id']} tick={item['tick']} destination={item['destination']}")
    print("note: pending=0 at a sampled window does not prove no command waited between windows.")
    print("note: pause/focus state is unknown in the v16 log format.")


def format_event(event: Event) -> str:
    if event.kind == "diagnostic":
        item = event.detail
        return (
            "window avg={avg_ms:.2f}ms max={max_ms:.2f}ms units={units} "
            "sim={sim_time:.1f}s pending={commands_pending} applied={commands_applied} "
            "rejected={commands_rejected}".format(**item)
        )
    if event.kind == "order_rejected":
        item = event.detail
        return f"order rejected unit={item['id']} kind={item['kind']} tick={item['tick']}: {item['reason']}"
    item = event.detail
    return f"route blocked unit={item['id']} tick={item['tick']} destination={item['destination']}"


def follow(path: Path, last: int, duration: Optional[float], as_json: bool) -> int:
    follower = LogFollower(path, start_at_end=True)
    started = time.monotonic()
    latest_events: List[Event] = []
    while True:
        events, truncated, capped = follower.read_new()
        if events:
            latest_events.extend(events)
            latest_events = latest_events[-max(200, last * 8):]
            if as_json:
                for event in events:
                    print(json.dumps({"event": event.kind, **event.detail}, ensure_ascii=False), flush=True)
            else:
                for event in events:
                    print(format_event(event), flush=True)
        if truncated:
            if as_json:
                print(json.dumps({"event": "log_truncated", "path": str(path)}, ensure_ascii=False), flush=True)
            else:
                print(f"warning: log truncated or replaced: {path}", flush=True)
        if capped:
            if as_json:
                print(json.dumps({"event": "append_capped", "bytes": MAX_POLL_BYTES}, ensure_ascii=False), flush=True)
            else:
                print(f"warning: append limited to {MAX_POLL_BYTES // 1024} KiB", flush=True)
        if follower.partial_discarded:
            if as_json:
                print(json.dumps({"event": "partial_line_discarded", "bytes": MAX_PARTIAL_BYTES}, ensure_ascii=False), flush=True)
            else:
                print(f"warning: discarded an unterminated log line over {MAX_PARTIAL_BYTES // 1024} KiB", flush=True)
        if duration is not None and time.monotonic() - started >= duration:
            break
        time.sleep(1.0)
    if not as_json:
        print(f"follow ended after {time.monotonic() - started:.1f}s; retained events={len(latest_events)}")
    return 0


def main(argv: Optional[List[str]] = None) -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description="Passively summarize RiskAI v16 runtime logs.")
    parser.add_argument("path", nargs="?", type=Path, default=DEFAULT_LOG, help=f"log path (default: {DEFAULT_LOG})")
    parser.add_argument("--last", type=int, default=3, help="number of latest diagnostic windows/events to show")
    parser.add_argument("--follow", action="store_true", help="poll appended lines every second; never controls the game")
    parser.add_argument("--duration", type=float, help="stop follow after this many seconds")
    parser.add_argument("--json", action="store_true", help="JSON snapshot; with --follow emits JSON Lines events")
    args = parser.parse_args(argv)
    if args.last < 1:
        parser.error("--last must be at least 1")
    if args.duration is not None and (not math.isfinite(args.duration) or args.duration < 0):
        parser.error("--duration must be a finite non-negative number")
    if args.follow:
        return follow(args.path, args.last, args.duration, args.json)
    events, age, capped = snapshot(args.path, args.last)
    payload = report(args.path, events, age, args.last, capped)
    if args.json:
        print(json.dumps(payload, ensure_ascii=False, indent=2))
    else:
        print_text(payload)
    return 0 if path_exists(args.path) else 2


def path_exists(path: Path) -> bool:
    return path.exists()


if __name__ == "__main__":
    raise SystemExit(main())
