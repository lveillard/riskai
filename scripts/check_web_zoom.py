"""Exercise Riesgus WebGL touchpad pan, pinch and mouse wheel routing.

The probe wraps riskaiWheel.readSample transparently: C# still consumes every
sample, while the browser records the eight-channel values it returned.
Camera ratios in result.json are derived from the public policy constants; the
screenshots are the integration evidence and are not reported as TargetZoom.
"""
import argparse
import json
import math
from pathlib import Path
import re
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/web-python'))
from playwright.sync_api import sync_playwright


WHEEL_EXPONENT = .24
MAX_STEPS = 4


def derived(sample):
    pinch, coarse, lines, pages, pan_x, pan_y = (sum(row[index] for row in sample) for index in range(6))
    steps = -(pinch * .01 / WHEEL_EXPONENT + coarse / 100 + lines / 3 + pages)
    steps = max(-MAX_STEPS, min(MAX_STEPS, steps))
    return {
        'sum': [pinch, coarse, lines, pages],
        'panNormalized': [pan_x, pan_y],
        'steps': steps,
        'targetZoomMultiplierDerived': math.exp(-steps * WHEEL_EXPONENT),
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
    diagnostics = []
    wheel_injection_scale = 1
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
            if 'RuntimeDiagnostics 30s' in message.text:
                diagnostics.append(dict(re.findall(r'(\w+)=([^\s]+)', message.text)))
            if message.type == 'error' and '404' not in message.text:
                report['errors'].append(message.text)

        def capture(name):
            page.screenshot(path=str(args.output / (name + '.png')))
            report['captures'].append(name)

        def send(method):
            page.evaluate("method => window.riskaiInstance.SendMessage('RiskAI · Bootstrap', method)", method)

        def snapshot():
            count = len(diagnostics)
            send('EndProbeMeasurement')
            deadline = time.monotonic() + 10
            while len(diagnostics) == count:
                assert time.monotonic() < deadline, 'Runtime did not report its pause state'
                page.wait_for_timeout(50)
            return diagnostics[-1]

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

        def dispatch_trusted(deltas, ctrl=False, point=None, delta_x=0):
            x, y = point or (args.width * .5, args.height * .42)
            page.mouse.move(x, y)
            if ctrl:
                page.keyboard.down('Control')
            try:
                for delta_y in deltas:
                    page.mouse.wheel(delta_x * wheel_injection_scale, delta_y * wheel_injection_scale)
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
            # Cold shader uploads delay the first rendered countdown frame.
            # Wait for actual simulation instead of assuming ready + 6.5 s.
            deadline = time.monotonic() + 90
            while True:
                state = snapshot()
                if state.get('paused') == 'False' and int(state.get('simTicks', 0)) > 0:
                    break
                assert time.monotonic() < deadline, 'Deployment countdown did not finish'
                page.wait_for_timeout(500)
            send('TogglePause')
            page.wait_for_timeout(250)
            paused_before = snapshot()
            assert paused_before['paused'] == 'True', 'Camera probe must start paused'
            page.wait_for_function('window.riskaiWheel && typeof window.riskaiWheel.readSample === "function"')
            # CDP wheel deltas are DIP; emulated DPR can change their DOM pixel
            # value. Calibrate the driver so each case sends its named CSS delta.
            page.evaluate("""() => {
                window.__probeWheelDelta = null;
                document.getElementById('game').addEventListener('wheel', event => {
                    window.__probeWheelDelta = event.deltaY;
                }, {once: true});
            }""")
            page.mouse.move(args.width * .5, args.height * .42)
            page.mouse.wheel(0, 100)
            page.wait_for_function('window.__probeWheelDelta !== null')
            delivered_delta = page.evaluate('window.__probeWheelDelta')
            assert delivered_delta > 0, 'CDP did not deliver the wheel calibration event'
            wheel_injection_scale = 100 / delivered_delta
            report['trustedWheelInjectionScale'] = wheel_injection_scale
            page.wait_for_timeout(750)
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

            reset_camera();dispatch_trusted([0] * 10, delta_x=5);finish_case('horizontal-50px')
            reset_camera();dispatch_trusted([5] * 10, delta_x=5);finish_case('diagonal-50px')
            reset_camera();dispatch_trusted([5, 120, 5]);finish_case('accelerated-pan-130px')

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

            reset_camera();capture('hud-touchpad-before')
            dispatch_trusted([5] * 10, point=hud_point, delta_x=5)
            page.mouse.move(*world_point)
            page.wait_for_timeout(300)
            reads = page.evaluate('window.__riskaiWheelReads.splice(0)')
            report['cases']['hud-touchpad-then-map-no-replay'] = derived(reads)
            report['cases']['hud-touchpad-then-map-no-replay']['reads'] = reads
            report['cases']['hud-touchpad-then-map-no-replay']['application'] = 'not measured; compare paused world in hud-touchpad-before.png and hud-touchpad-return-map.png'
            capture('hud-touchpad-return-map')

            for name, pixels in [('fine-burst-200px', 200), ('trusted-fine-burst-50px', 50),
                                 ('fine-burst-reverse-200px', -200), ('accelerated-pan-130px', 130)]:
                case = report['cases'][name]
                assert case['sum'] == [0, 0, 0, 0], f'{name} must pan without zoom'
                assert abs(case['panNormalized'][1] - pixels / args.height) < 1e-6
            horizontal = report['cases']['horizontal-50px']
            assert horizontal['sum'] == [0, 0, 0, 0] and horizontal['panNormalized'][1] == 0
            assert abs(horizontal['panNormalized'][0] + 50 / args.width) < 1e-6
            diagonal = report['cases']['diagonal-50px']
            assert diagonal['sum'] == [0, 0, 0, 0]
            assert abs(diagonal['panNormalized'][0] + 50 / args.width) < 1e-6
            assert abs(diagonal['panNormalized'][1] - 50 / args.height) < 1e-6
            assert report['cases']['wheel-100px']['sum'] == [0, 100, 0, 0]
            assert abs(report['cases']['wheel-100px']['steps'] + 1) < 1e-6
            assert report['cases']['wheel-120px']['sum'] == [0, 120, 0, 0]
            assert report['cases']['ctrl-trusted-fine-50px']['sum'] == [50, 0, 0, 0]
            assert report['cases']['ctrl-trusted-fine-50px']['panNormalized'] == [0, 0]
            paused_after = snapshot()
            assert paused_after['paused'] == 'True'
            assert paused_after['simTicks'] == paused_before['simTicks'], 'Camera gestures advanced the paused simulation'
            report['pausedSimulationTicks'] = int(paused_after['simTicks'])
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
