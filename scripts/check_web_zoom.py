"""Exercise Riesgus WebGL wheel normalization without runtime instrumentation.

The probe wraps riskaiWheel.readSample transparently: C# still consumes every
sample, while the browser records the exact four-channel values it returned.
Camera ratios in result.json are derived from the public policy constants; the
screenshots are the integration evidence and are not reported as TargetZoom.
"""
import argparse
import json
import math
from pathlib import Path
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/web-python'))
from playwright.sync_api import sync_playwright


WHEEL_EXPONENT = .24
MAX_STEPS = 4


def derived(sample):
    fine, coarse, lines, pages = (sum(row[index] for row in sample) for index in range(4))
    steps = -(fine / 400 + coarse / 100 + lines / 3 + pages)
    steps = max(-MAX_STEPS, min(MAX_STEPS, steps))
    old_steps = -((fine + coarse) / 100 + lines / 3 + pages)
    old_steps = max(-MAX_STEPS, min(MAX_STEPS, old_steps))
    return {
        'sum': [fine, coarse, lines, pages],
        'steps': steps,
        'targetZoomMultiplierDerived': math.exp(-steps * WHEEL_EXPONENT),
        'oldTargetZoomMultiplierDerived': math.exp(-old_steps * WHEEL_EXPONENT),
        'targetZoomMeasured': False,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', default='http://127.0.0.1:8086')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--width', type=int, default=1600)
    parser.add_argument('--height', type=int, default=900)
    parser.add_argument('--dpr', type=float, default=1)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    report = {'url': args.url, 'viewport': [args.width, args.height], 'dpr': args.dpr,
              'errors': [], 'cases': {}, 'captures': [], 'targetZoomMeasured': False}
    ready = []
    started = time.monotonic()

    with sync_playwright() as pw, (args.output / 'console.log').open('w', encoding='utf-8') as log:
        browser = pw.chromium.launch(channel='msedge', headless=True)
        page = browser.new_page(viewport={'width': args.width, 'height': args.height},
                                device_scale_factor=args.dpr)

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

        def send(method):
            page.evaluate("method => window.riskaiInstance.SendMessage('RiskAI · Bootstrap', method)", method)

        def reset_camera():
            send('ResetView')
            page.wait_for_timeout(750)
            page.mouse.move(args.width * .5, args.height * .42)
            page.evaluate('window.__riskaiWheelReads.length=0')

        def dispatch(deltas, mode=0, ctrl=False, point=None):
            x, y = point or (args.width * .5, args.height * .42)
            page.evaluate("""([deltas, mode, ctrl, x, y]) => {
                const canvas=document.getElementById('game');
                for(const deltaY of deltas) canvas.dispatchEvent(new WheelEvent('wheel', {
                    deltaY, deltaMode: mode, ctrlKey: ctrl, clientX: x, clientY: y,
                    bubbles: true, cancelable: true
                }));
            }""", [deltas, mode, ctrl, x, y])
            page.wait_for_timeout(750)

        def dispatch_trusted(deltas, ctrl=False, point=None):
            x, y = point or (args.width * .5, args.height * .42)
            page.mouse.move(x, y)
            if ctrl:
                page.keyboard.down('Control')
            try:
                for delta_y in deltas:
                    page.mouse.wheel(0, delta_y)
                    page.wait_for_timeout(16)
            finally:
                if ctrl:
                    page.keyboard.up('Control')
            page.wait_for_timeout(750)

        def browser_viewport():
            return page.evaluate("""() => ({
                scale: window.visualViewport ? window.visualViewport.scale : 1,
                dpr: window.devicePixelRatio,
                innerWidth: window.innerWidth,
                innerHeight: window.innerHeight
            })""")

        def finish_case(name):
            reads = page.evaluate('window.__riskaiWheelReads.splice(0)')
            report['cases'][name] = derived(reads)
            report['cases'][name]['reads'] = reads
            capture(name + '-after')

        page.on('console', console)
        page.on('pageerror', lambda error: report['errors'].append(str(error)))
        try:
            page.goto(args.url.rstrip('/') + '/?riskai-map=classic&riskai-seed=19031', wait_until='domcontentloaded')
            page.wait_for_function('window.riskaiInstance != null', timeout=240000)
            page.wait_for_timeout(6500)
            page.evaluate("window.riskaiInstance.SendMessage('RiskAI · Front End', 'StartBattle')")
            deadline = time.monotonic() + 120
            while not ready:
                assert time.monotonic() < deadline, 'Match did not start'
                page.wait_for_timeout(50)
            remaining = 6.5 - (time.monotonic() - ready[0])
            if remaining > 0:
                page.wait_for_timeout(remaining * 1000)
            send('TogglePause')
            page.wait_for_timeout(250)
            page.wait_for_function('window.riskaiWheel && typeof window.riskaiWheel.readSample === "function"')
            page.evaluate("""() => {
                window.__riskaiWheelReads=[];
                const bridge=window.riskaiWheel;
                const original=bridge.readSample.bind(bridge);
                bridge.readSample=function(){
                    const sample=original();
                    if(sample)window.__riskaiWheelReads.push(sample.slice());
                    return sample;
                };
            }""")

            reset_camera();capture('before')
            dispatch([5] * 40);finish_case('fine-burst-200px')

            reset_camera();dispatch_trusted([5] * 10);finish_case('trusted-fine-burst-50px')

            reset_camera();dispatch_trusted([100]);finish_case('wheel-100px')
            reset_camera();dispatch_trusted([120]);finish_case('wheel-120px')
            reset_camera();dispatch([-5] * 40);finish_case('fine-burst-reverse-200px')

            reset_camera()
            viewport_before = browser_viewport()
            dispatch_trusted([5] * 10, ctrl=True);finish_case('ctrl-trusted-fine-50px')
            viewport_after = browser_viewport()
            report['browserViewport'] = {'before': viewport_before, 'after': viewport_after}
            assert viewport_after == viewport_before, 'ctrl+wheel changed browser scale or viewport dimensions'

            reset_camera()
            world_point = (args.width * .5, args.height * .42)
            page.mouse.move(*world_point);page.wait_for_timeout(250);capture('hud-before')
            hud_point = (args.width * .75, args.height - 40)
            dispatch_trusted([100], point=hud_point)
            page.mouse.move(*world_point)
            page.wait_for_timeout(300)
            reads = page.evaluate('window.__riskaiWheelReads.splice(0)')
            report['cases']['hud-then-map-no-replay'] = derived(reads)
            report['cases']['hud-then-map-no-replay']['reads'] = reads
            report['cases']['hud-then-map-no-replay']['application'] = 'not measured; compare paused world in hud-before.png and hud-return-map.png'
            capture('hud-return-map')

            assert report['cases']['fine-burst-200px']['sum'] == [200, 0, 0, 0]
            assert abs(report['cases']['fine-burst-200px']['steps'] + .5) < 1e-6
            assert report['cases']['trusted-fine-burst-50px']['sum'] == [50, 0, 0, 0]
            assert abs(report['cases']['trusted-fine-burst-50px']['steps'] + .125) < 1e-6
            assert report['cases']['wheel-100px']['sum'] == [0, 100, 0, 0]
            assert abs(report['cases']['wheel-100px']['steps'] + 1) < 1e-6
            assert report['cases']['wheel-120px']['sum'] == [0, 120, 0, 0]
            assert abs(report['cases']['fine-burst-reverse-200px']['steps'] - .5) < 1e-6
            assert report['cases']['ctrl-trusted-fine-50px']['sum'] == [50, 0, 0, 0]
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
