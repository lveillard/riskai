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
    parser.add_argument("--camera", action="store_true", help="Capture paused mouse and touch camera gestures.")
    parser.add_argument("--camera-touch", action="store_true", help="Include a two-finger CDP camera gesture with --camera.")
    parser.add_argument("--ui-wheel", action="store_true", help="Capture real wheel input on an overflowing ranking panel.")
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
        "cameraRequested": args.camera,
        "cameraTouchRequested": args.camera and args.camera_touch,
        "cameraCaptures": [],
        "uiWheelCaptures": [],
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
                if args.camera:
                    capture_camera_evidence(page, cdp, output, report, include_touch=args.camera_touch)
                if args.ui_wheel:
                    capture_ui_wheel_evidence(page, output, report)
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


def capture_camera_evidence(page, cdp, output, report, include_touch=False):
    """Capture real paused camera gestures; evidence does not affect smoke success."""
    page.mouse.click(800, 400)
    page.wait_for_timeout(250)
    page.keyboard.press("F10")
    page.wait_for_timeout(350)

    def capture(name):
        path = output / name
        page.screenshot(path=str(path))
        report["cameraCaptures"].append(str(path))

    capture("paused-before.png")
    page.mouse.move(800, 400)
    page.mouse.wheel(0, 300)
    page.wait_for_timeout(700)
    capture("paused-wheel.png")

    page.mouse.move(800, 400)
    page.mouse.down(button="right")
    for point in ((840, 412), (885, 430), (930, 442), (970, 450)):
        page.mouse.move(*point)
        page.wait_for_timeout(80)
    page.mouse.up(button="right")
    page.wait_for_timeout(250)
    capture("paused-right-drag.png")

    page.mouse.move(970, 450)
    page.mouse.down(button="middle")
    for point in ((930, 442), (885, 430), (840, 412), (800, 400)):
        page.mouse.move(*point)
        page.wait_for_timeout(80)
    page.mouse.up(button="middle")
    page.wait_for_timeout(250)
    capture("paused-middle-drag.png")

    if include_touch:
        cdp.send(
            "Input.dispatchTouchEvent",
            {
                "type": "touchStart",
                "touchPoints": [
                    {"id": 31, "x": 760, "y": 400},
                    {"id": 32, "x": 840, "y": 400},
                ],
            },
        )
        page.wait_for_timeout(120)
        cdp.send(
            "Input.dispatchTouchEvent",
            {
                "type": "touchMove",
                "touchPoints": [
                    {"id": 31, "x": 730, "y": 430},
                    {"id": 32, "x": 870, "y": 430},
                ],
            },
        )
        page.wait_for_timeout(180)
        cdp.send("Input.dispatchTouchEvent", {"type": "touchEnd", "touchPoints": []})
        page.wait_for_timeout(300)
        capture("paused-touch-camera.png")


def capture_ui_wheel_evidence(page, output, report):
    """Real browser events; captures need visual review, not just file existence."""
    page.set_viewport_size({"width": 1600, "height": 420})
    page.wait_for_timeout(700)
    page.keyboard.down("Tab")
    page.wait_for_timeout(500)
    page.mouse.move(800, 210)
    for name, wheel in (("ranking-before.png", 0), ("ranking-down.png", 480), ("ranking-up.png", -480)):
        if wheel:
            page.mouse.wheel(0, wheel)
            page.wait_for_timeout(500)
        path=output/name
        page.screenshot(path=str(path))
        report["uiWheelCaptures"].append(str(path))
    page.keyboard.up("Tab")
    page.set_viewport_size(VIEWPORT)
    page.wait_for_timeout(700)


if __name__ == "__main__":
    raise SystemExit(main())
