"""Exercise the pen adapter with Chromium CDP events, without a Unity player.

Requires Playwright and an installed Chromium channel (default: msedge).
This verifies DOM event handling, not Unity WebGL or a physical stylus.
"""
import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
local_packages = ROOT / '.tools/web-python'
if local_packages.is_dir():
    sys.path.insert(0, str(local_packages))


def main():
    from playwright.sync_api import sync_playwright

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--channel', default='msedge')
    args = parser.parse_args()
    adapter = ROOT / 'RiskAI/Assets/WebGLTemplates/RiskAI/riskai-pen.js'
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(channel=args.channel, headless=True)
        page = browser.new_page(viewport={'width': 640, 'height': 480}, device_scale_factor=2)
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.set_content('''<!doctype html><style>
            body{margin:0}canvas{position:absolute;left:40px;top:30px;width:400px;height:240px;touch-action:none}
            </style><canvas id="game" width="800" height="480" tabindex="0"></canvas>''')
        page.add_script_tag(path=str(adapter))
        page.evaluate('''() => {
            window.bridge=RiskAIPen.attach(document.getElementById('game'));
            window.seen={pointerdown:0,mousedown:0,click:0,touchstart:0};
            for(const type of Object.keys(seen))game.addEventListener(type,()=>seen[type]++);
            window.drain=()=>{const out=[];let sample;while((sample=bridge.readSample()))out.push(sample);return out;};
        }''')
        cdp = page.context.new_cdp_session(page)

        def pen(kind, x, y, buttons, button='none'):
            cdp.send('Input.dispatchMouseEvent', {
                'type': kind, 'x': x, 'y': y, 'buttons': buttons, 'button': button,
                'pointerType': 'pen', 'force': .5 if buttons else 0,
                'tiltX': 12, 'tiltY': -8, 'clickCount': 1,
            })

        pen('mousePressed', 140, 90, 1, 'left')
        pen('mouseMoved', 240, 150, 1)
        pen('mouseReleased', 240, 150, 0, 'left')
        samples = page.evaluate('drain()')
        states = [sample for sample in samples if sample[0] == 0]
        assert any(sample[3] & 1 for sample in states), samples
        assert states[-1][3] == 0, samples
        first_down = next(sample for sample in states if sample[3] & 1)
        assert abs(first_down[1] - .25) < .001 and abs(first_down[2] - .25) < .001, samples
        assert not any(sample[0] == 1 for sample in samples), 'Ordinary release/capture loss canceled a normal tap'
        assert page.evaluate('seen.mousedown===0&&seen.click===0&&seen.pointerdown===0'), page.evaluate('seen')

        pen('mousePressed', 180, 110, 2, 'right')
        pen('mouseReleased', 180, 110, 0, 'right')
        barrel = page.evaluate('drain()')
        assert any(sample[3] & 2 for sample in barrel), barrel

        pen('mousePressed', 180, 110, 1, 'left')
        page.evaluate('window.dispatchEvent(new Event("blur"))')
        canceled = page.evaluate('drain()')
        assert canceled and canceled[-1][0] == 1, canceled
        pen('mouseReleased', 180, 110, 0, 'left')
        page.evaluate('drain()')

        # Genuine touch must continue to reach the engine's normal touch path.
        cdp.send('Input.dispatchTouchEvent', {'type': 'touchStart', 'touchPoints': [{'x': 200, 'y': 130}]})
        cdp.send('Input.dispatchTouchEvent', {'type': 'touchEnd', 'touchPoints': []})
        assert page.evaluate('seen.touchstart>0'), page.evaluate('seen')
        assert page.evaluate('drain().length===0'), 'Finger input leaked into the pen queue'

        # Move through the bridge's compatibility suppression window before testing a mouse.
        page.wait_for_timeout(550)
        page.mouse.click(200, 130)
        assert page.evaluate('seen.mousedown>0&&seen.click>0'), page.evaluate('seen')
        assert page.evaluate('drain().length===0'), 'Mouse input leaked into the pen queue'
        assert not errors, errors
        print(json.dumps({'result': 'passed', 'browser': browser.version, 'channel': args.channel,
                          'penSamples': len(samples), 'barrelSamples': len(barrel),
                          'checks': ['pen edges', 'canvas coordinates at DPR 2', 'no compatibility double action',
                                     'barrel', 'cancel', 'finger preserved', 'mouse preserved'],
                          'scope': 'DOM adapter with CDP synthetic input; no Unity WebGL or physical stylus'}))
        browser.close()


if __name__ == '__main__':
    main()
