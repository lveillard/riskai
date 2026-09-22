"""Capture a real WebGL match start, including the five-second briefing.

Uses desktop Edge with optional touch emulation; this does not certify physical
mobile performance. Supply --start-x/--start-y to start with an actual tap.
"""
import argparse
import json
from pathlib import Path
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/web-python'))
from playwright.sync_api import sync_playwright


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', default='http://127.0.0.1:8086')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--width', type=int, default=390)
    parser.add_argument('--height', type=int, default=844)
    parser.add_argument('--dpr', type=float, default=2)
    parser.add_argument('--start-x', type=float)
    parser.add_argument('--start-y', type=float)
    parser.add_argument('--language-x', type=float)
    parser.add_argument('--language-y', type=float)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    report = {'url': args.url, 'viewport': [args.width, args.height],
              'dpr': args.dpr, 'physicalMobile': False, 'errors': [],
              'captures': [], 'actualStartInput': args.start_x is not None}
    started = time.monotonic()
    ready = []
    with sync_playwright() as pw, (args.output / 'console.log').open('w', encoding='utf-8') as log:
        browser = pw.chromium.launch(channel='msedge', headless=True)
        page = browser.new_page(viewport={'width': args.width, 'height': args.height},
                                device_scale_factor=args.dpr, has_touch=args.width < 1100)
        def console(message):
            log.write(f'{time.monotonic()-started:.3f} [{message.type}] {message.text}\n')
            log.flush()
            if 'RISKAI_STARTUP phase=ready' in message.text:
                ready.append(time.monotonic())
            if message.type == 'error' and '404' not in message.text:
                report['errors'].append(message.text)
        def capture(name):
            page.screenshot(path=str(args.output / (name + '.png')))
            report['captures'].append(name)
        page.on('console', console)
        page.on('pageerror', lambda error: report['errors'].append(str(error)))
        try:
            page.goto(args.url.rstrip('/') + '/?riskai-map=classic&riskai-seed=19031', wait_until='domcontentloaded')
            page.wait_for_function('window.riskaiInstance != null', timeout=240000)
            # Unity resolves createUnityInstance while its splash is still active.
            page.wait_for_timeout(6500)
            report['title'] = page.title()
            assert 'Riesgus' in report['title'], report['title']
            if args.language_x is not None:
                assert args.language_y is not None, '--language-y is required with --language-x'
                if args.width < 1100:
                    page.touchscreen.tap(args.language_x, args.language_y)
                else:
                    page.mouse.click(args.language_x, args.language_y, delay=100)
                page.wait_for_timeout(500)
            capture('menu')
            if args.start_x is not None:
                assert args.start_y is not None, '--start-y is required with --start-x'
                if args.width < 1100:
                    page.touchscreen.tap(args.start_x, args.start_y)
                else:
                    page.mouse.click(args.start_x, args.start_y, delay=100)
            else:
                page.evaluate("window.riskaiInstance.SendMessage('RiskAI · Front End', 'StartBattle')")
            deadline = time.monotonic() + 120
            while not ready:
                assert time.monotonic() < deadline, 'Match did not start'
                page.wait_for_timeout(50)
            for target, name in [(0.25, 'briefing-early'), (2.0, 'briefing-middle'),
                                 (3.6, 'briefing-late'), (6.0, 'playing')]:
                remaining = target - (time.monotonic() - ready[0])
                if remaining > 0:
                    page.wait_for_timeout(remaining * 1000)
                capture(name)
            report['canvas'] = page.locator('canvas').bounding_box()
            assert abs(report['canvas']['width'] - args.width) <= 1
            assert abs(report['canvas']['height'] - args.height) <= 1
            report['success'] = not report['errors']
        except Exception as error:
            report.update(success=False, failure=str(error))
        finally:
            report['seconds'] = round(time.monotonic() - started, 2)
            (args.output / 'result.json').write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
            browser.close()
    print(json.dumps(report, ensure_ascii=False))
    return 0 if report['success'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
