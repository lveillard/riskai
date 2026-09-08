"""Load the actual exported Unity player in Edge and retain browser evidence.

Requires Playwright and Edge. This is a desktop browser probe, not ARM emulation.
Serve the export with scripts/serve_web.py first. --probe uses the game's shared
command probe; --restart uses its shared scene lifecycle probe.
"""
import argparse
import json
from pathlib import Path
import sys
import time
from urllib.parse import urlencode

ROOT = Path(__file__).resolve().parents[1]
if (ROOT / '.tools/web-python').is_dir():
    sys.path.insert(0, str(ROOT / '.tools/web-python'))


def main():
    from playwright.sync_api import sync_playwright

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', default='http://127.0.0.1:8080')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--width', type=int, default=1600)
    parser.add_argument('--height', type=int, default=900)
    parser.add_argument('--dpr', type=float, default=1)
    parser.add_argument('--map', choices=['classic', 'riverlands', 'europe', 'world'])
    parser.add_argument('--seconds', type=int, default=60)
    parser.add_argument('--recruits', type=int, default=6, choices=range(1, 101), metavar='1..100')
    parser.add_argument('--path-budget', type=int, default=500, metavar='100..2000',
                        help='Unity asynchronous NavMesh path budget for the controlled A/B probe.')
    parser.add_argument('--warmup', type=int, default=0)
    parser.add_argument('--probe', action='store_true')
    parser.add_argument('--restart', action='store_true')
    parser.add_argument('--headed', action='store_true')
    parser.add_argument('--suppress-draws', action='store_true',
                        help='Diagnostic only: skip WebGL draw calls, retaining the same simulation. Not a playable performance result.')
    parser.add_argument('--overview', action='store_true',
                        help='Measure the normal strategic overview using the existing FrameMap camera action.')
    args = parser.parse_args()
    if args.probe and args.restart:
        parser.error('Choose one measurement type per browser session.')
    if args.probe and args.seconds <= 30:
        parser.error('The shared runtime probe requires more than 30 simulation seconds; use --seconds 60 or longer.')
    if args.suppress_draws and not args.probe:
        parser.error('--suppress-draws is only meaningful with --probe.')
    if args.overview and not args.probe:
        parser.error('--overview requires --probe and its battlefield.')
    if not 100 <= args.path_budget <= 2000:
        parser.error('--path-budget must be between 100 and 2000.')
    args.output.mkdir(parents=True, exist_ok=True)
    query = {'riskai-seed': 19031, 'riskai-players': 16, 'riskai-path-budget': args.path_budget}
    if args.map:
        query['riskai-map'] = args.map
        if not args.restart:
            query['riskai-play'] = 1
    if args.probe:
        query.update({'riskai-probe': 1, 'riskai-probe-seconds': args.seconds,
                      'riskai-probe-recruits': args.recruits, 'riskai-probe-warmup': args.warmup})
    if args.restart:
        query.update({'riskai-restart-probe': 1, 'riskai-restart-cycles': 3})
    url = args.url.rstrip('/') + '/?' + urlencode(query)
    report = {'url': url, 'desktop_browser': True, 'physical_arm': False,
              'viewport': [args.width, args.height], 'device_scale_factor': args.dpr,
              'draws_suppressed': args.suppress_draws, 'overview': args.overview,
              'path_budget': args.path_budget}
    started = time.monotonic()
    with (args.output / 'console.log').open('w', encoding='utf-8') as log, sync_playwright() as p:
        browser = p.chromium.launch(channel='msedge', headless=not args.headed)
        report['browser'] = browser.version
        page = browser.new_page(viewport={'width': args.width, 'height': args.height}, device_scale_factor=args.dpr)
        if args.suppress_draws:
            page.add_init_script('''
                window.riskaiSuppressedDraws = 0;
                for (const kind of ['WebGLRenderingContext', 'WebGL2RenderingContext']) {
                    const prototype = window[kind]?.prototype;
                    if (!prototype) continue;
                    for (const name of ['drawArrays', 'drawElements', 'drawArraysInstanced', 'drawElementsInstanced', 'drawRangeElements']) {
                        if (typeof prototype[name] === 'function')
                            prototype[name] = function() { window.riskaiSuppressedDraws++; };
                    }
                }
            ''')
        errors, results, console_errors, ready = [], [], [], []

        def console(message):
            value = message.text
            log.write(f'{time.monotonic()-started:.3f} [{message.type}] {value}\n')
            log.flush()
            if 'RISKAI_PROBE_RESULT' in value or 'RISKAI_RESTART_RESULT' in value:
                results.append(value)
            if 'RISKAI_STARTUP phase=ready' in value:
                ready.append(value)
            if 'SendMessage:' in value and 'does not have receiver' in value:
                errors.append(value)
            if message.type == 'error':
                console_errors.append(value)
                # A default missing favicon is a page resource issue, not a C# crash.
                if not ('Failed to load resource' in value and '404' in value):
                    errors.append(value)

        page.on('console', console)
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.on('requestfailed', lambda request: log.write(f'REQUEST_FAILED {request.url} {request.failure}\n'))
        try:
            page.goto(url, wait_until='domcontentloaded', timeout=30000)
            deadline = time.monotonic() + 240
            while not page.evaluate('Boolean(window.riskaiInstance)'):
                if time.monotonic() > deadline:
                    raise TimeoutError('Unity loader did not resolve in 240 seconds.')
                if page.locator('#retry').is_visible():
                    raise RuntimeError(page.locator('#hint').inner_text())
                page.wait_for_timeout(1000)
            report['loader_seconds'] = round(time.monotonic()-started, 3)
            print(f'Unity player loaded in {report["loader_seconds"]} s', flush=True)
            if args.map:
                deadline = time.monotonic() + 180
                while not ready:
                    if errors:
                        raise RuntimeError('Unity battle initialization emitted an error: ' + errors[0])
                    if time.monotonic() > deadline:
                        raise TimeoutError('The battlefield never emitted RISKAI_STARTUP phase=ready.')
                    page.wait_for_timeout(500)
                report['battle_ready_seconds'] = round(time.monotonic()-started, 3)
            if args.overview:
                page.evaluate('window.riskaiInstance.SendMessage("RiskAI · Bootstrap", "FrameMap")')
            page.wait_for_timeout(2000)
            page.screenshot(path=str(args.output / 'loaded.png'))
            report['canvas'] = page.evaluate('''() => {
                const c=document.getElementById('game'),g=c.getContext('webgl2');
                const d=g&&g.getExtension('WEBGL_debug_renderer_info');
                return {width:c.width,height:c.height,cssWidth:c.clientWidth,cssHeight:c.clientHeight,
                    renderer:g?g.getParameter(d?d.UNMASKED_RENDERER_WEBGL:g.RENDERER):null,
                    heapBytes:window.riskaiInstance.Module.HEAPU8?.buffer.byteLength};
            }''')
            if args.probe or args.restart:
                deadline = time.monotonic() + max(240, args.seconds * 3 + args.warmup + 120)
                last_notice = 0
                while not results:
                    if time.monotonic() > deadline:
                        raise TimeoutError('The Unity runtime probe did not emit its terminal result.')
                    page.wait_for_timeout(1000)
                    if time.monotonic() - last_notice > 30:
                        print(f'Waiting for runtime probe, elapsed {time.monotonic()-started:.0f} s', flush=True)
                        last_notice = time.monotonic()
                report['probe_results'] = results
                print(results[-1], flush=True)
            page.screenshot(path=str(args.output / 'final.png'))
            report['success'] = not errors and not any('success=false' in item.lower() for item in results)
        except Exception as error:
            report['success'] = False
            report['failure'] = str(error)
            try:
                page.screenshot(path=str(args.output / 'failure.png'), timeout=5000)
            except Exception:
                pass
        finally:
            if args.suppress_draws:
                try:
                    report['suppressed_draw_calls'] = page.evaluate('window.riskaiSuppressedDraws || 0')
                except Exception:
                    report['suppressed_draw_calls'] = None
            report['page_errors'] = errors
            report['console_errors'] = console_errors
            report['seconds'] = round(time.monotonic()-started, 3)
            (args.output / 'result.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
            browser.close()
    print(json.dumps(report), flush=True)
    return 0 if report['success'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
