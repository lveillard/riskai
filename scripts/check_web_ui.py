"""Smoke the exported Unity UI with CDP pen and touch at a fixed 1600x900 size.

The pen gesture must start an actual battlefield. The touch-menu screenshot is
retained for visual inspection. This is a desktop browser probe, not physical
hardware QA.
"""

import argparse
import json
import sys
import time
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PLAYWRIGHT_PATH = ROOT / ".tools/web-python"
if PLAYWRIGHT_PATH.is_dir():
    sys.path.insert(0, str(PLAYWRIGHT_PATH))


VIEWPORT = {"width": 1600, "height": 900}
PEN_POINT = {"x": 1415, "y": 828}
MENU_POINT = {"x": 1560, "y": 25}
PEN_TIMEOUT_SECONDS = 90


def parse_args():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:8080")
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def main():
    args = parse_args()
    output = args.output
    output.mkdir(parents=True, exist_ok=True)
    url = args.url.rstrip("/") + "/?riskai-seed=19031&riskai-players=16"
    report = {
        "url": url,
        "viewport": [VIEWPORT["width"], VIEWPORT["height"]],
        "physicalHardware": False,
        "penStartedBattle": False,
        "consumedPen": [],
        "errors": [],
        "success": False,
    }
    errors = []
    ready = []
    playwright = None
    browser = None
    page = None
    started = time.monotonic()

    with (output / "console.log").open("w", encoding="utf-8") as log:
        try:
            from playwright.sync_api import sync_playwright

            playwright = sync_playwright().start()
            browser = playwright.chromium.launch(channel="msedge", headless=True)
            report["browser"] = browser.version
            page = browser.new_page(viewport=VIEWPORT, has_touch=True)

            def record_console(message):
                value = message.text
                log.write(f"{time.monotonic() - started:.3f} [{message.type}] {value}\n")
                log.flush()
                if "RISKAI_STARTUP phase=ready" in value:
                    ready.append(value)
                # A missing resource such as a favicon is harmless.
                harmless_404 = "Failed to load resource" in value and "404" in value
                if message.type == "error" and not harmless_404:
                    errors.append(value)

            page.on("console", record_console)
            page.on("pageerror", lambda error: errors.append(str(error)))
            page.goto(url, wait_until="domcontentloaded", timeout=30000)
            page.wait_for_function(
                "Boolean(window.riskaiInstance)", timeout=240000
            )
            page.wait_for_timeout(1500)
            page.screenshot(path=str(output / "menu.png"))

            page.evaluate(
                """() => {
                    const original = window.riskaiPen.readSample.bind(window.riskaiPen);
                    window.consumedPen = [];
                    window.riskaiPen.readSample = () => {
                        const row = original();
                        if (row) window.consumedPen.push(row);
                        return row;
                    };
                }"""
            )
            cdp = page.context.new_cdp_session(page)
            for event_type, buttons in (("mousePressed", 1), ("mouseReleased", 0)):
                cdp.send(
                    "Input.dispatchMouseEvent",
                    {
                        "type": event_type,
                        **PEN_POINT,
                        "buttons": buttons,
                        "button": "left",
                        "pointerType": "pen",
                        "force": 0.5 if buttons else 0,
                        "clickCount": 1,
                    },
                )
                page.wait_for_timeout(140)

            deadline = time.monotonic() + PEN_TIMEOUT_SECONDS
            while not ready and not errors and time.monotonic() < deadline:
                page.wait_for_timeout(500)
            page.screenshot(path=str(output / "pen-start.png"))
            report["penStartedBattle"] = bool(ready)
            report["consumedPen"] = page.evaluate("window.consumedPen")
            report["errors"] = errors

            if ready:
                cdp.send(
                    "Input.dispatchTouchEvent",
                    {"type": "touchStart", "touchPoints": [MENU_POINT]},
                )
                page.wait_for_timeout(160)
                cdp.send(
                    "Input.dispatchTouchEvent",
                    {"type": "touchEnd", "touchPoints": []},
                )
                page.wait_for_timeout(1000)
                page.screenshot(path=str(output / "touch-menu.png"))

            report["success"] = report["penStartedBattle"] and not errors
        except Exception as error:
            report["failure"] = f"{type(error).__name__}: {error}"
            if page is not None:
                try:
                    page.screenshot(path=str(output / "failure.png"), timeout=5000)
                except Exception:
                    pass
        finally:
            report["errors"] = errors
            report["seconds"] = round(time.monotonic() - started, 3)
            if browser is not None:
                try:
                    browser.close()
                except Exception as close_error:
                    report["closeFailure"] = str(close_error)
            if playwright is not None:
                try:
                    playwright.stop()
                except Exception as stop_error:
                    report["playwrightStopFailure"] = str(stop_error)
            (output / "result.json").write_text(
                json.dumps(report, indent=2), encoding="utf-8"
            )

    print(json.dumps(report), flush=True)
    return 0 if report["success"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
