"""Measure an isolated real WebGL menu/match in Edge, without changing game rules.

Frame intervals are browser requestAnimationFrame cadence, not Unity profiler
FPS. GPU readings include the whole machine. Process private bytes measure
committed memory, not unique resident RAM. Physical mobile is not simulated.
"""
import argparse
import json
import os
from pathlib import Path
import statistics
import subprocess
import sys
import time
from urllib.parse import urlencode

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/web-python'))
from playwright.sync_api import sync_playwright


def private_memory(processes):
    if os.name != 'nt':
        return None
    pids = ','.join(str(int(p['id'])) for p in processes)
    command = f'Get-Process -Id {pids} -ErrorAction SilentlyContinue | Select-Object Id,PrivateMemorySize64,WorkingSet64 | ConvertTo-Json -Compress'
    result = subprocess.run(['powershell', '-NoProfile', '-Command', command],
                            capture_output=True, text=True, creationflags=subprocess.CREATE_NO_WINDOW)
    if not result.stdout.strip():
        return None
    rows = json.loads(result.stdout)
    return rows if isinstance(rows, list) else [rows]


def gpu_snapshot():
    try:
        result = subprocess.run(['nvidia-smi', '--query-gpu=name,memory.used,memory.total,utilization.gpu',
                                 '--format=csv,noheader,nounits'], capture_output=True, text=True, timeout=10)
        return result.stdout.strip() if result.returncode == 0 else None
    except (OSError, subprocess.TimeoutExpired):
        return None


def percentile(values, percent):
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, round((len(ordered) - 1) * percent / 100))]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--url', default='https://riesgus.com')
    parser.add_argument('--scenario', choices=['menu', 'classic', 'europe'], required=True)
    parser.add_argument('--seconds', type=int, default=30)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--width', type=int, default=1600)
    parser.add_argument('--height', type=int, default=900)
    parser.add_argument('--dpr', type=float, default=1)
    parser.add_argument('--disable-architecture-batching', action='store_true')
    parser.add_argument('--manual-architecture-batching', action='store_true')
    parser.add_argument('--native-architecture-batching', action='store_true')
    parser.add_argument('--disable-unit-presentation-culling', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    report = {'scenario': args.scenario, 'viewport': [args.width, args.height], 'dpr': args.dpr,
              'physicalMobile': False, 'errors': [], 'gpuScope': 'whole machine',
              'frameScope': 'browser requestAnimationFrame cadence'}
    query = {'riskai-seed': 19031}
    if args.disable_architecture_batching:
        query['riskai-disable-architecture-batching'] = 1
    if args.manual_architecture_batching:
        query['riskai-manual-architecture-batching'] = 1
    if args.native_architecture_batching:
        query['riskai-native-architecture-batching'] = 1
    if args.disable_unit_presentation_culling:
        query['riskai-disable-unit-presentation-culling'] = 1
    if args.scenario != 'menu':
        query.update({'riskai-map': args.scenario, 'riskai-play': 1, 'riskai-players': 16})
    report['url'] = args.url.rstrip('/') + '/?' + urlencode(query)
    ready = []
    started = time.monotonic()
    with sync_playwright() as pw, (args.output / 'console.log').open('w', encoding='utf-8') as log:
        browser = pw.chromium.launch(channel='msedge', headless=True)
        report['browser'] = browser.version
        page = browser.new_page(viewport={'width': args.width, 'height': args.height}, device_scale_factor=args.dpr)
        def console(message):
            log.write(f'{time.monotonic()-started:.3f} [{message.type}] {message.text}\n')
            log.flush()
            if 'RISKAI_STARTUP phase=ready' in message.text:
                ready.append(time.monotonic())
            if message.type == 'error' and '404' not in message.text:
                report['errors'].append(message.text)
        page.on('console', console)
        page.on('pageerror', lambda error: report['errors'].append(str(error)))
        try:
            page.goto(report['url'], wait_until='domcontentloaded')
            page.wait_for_function('Boolean(window.riskaiInstance)', timeout=240000)
            report['loaderSeconds'] = round(time.monotonic()-started, 3)
            if args.scenario != 'menu':
                deadline = time.monotonic() + 120
                while not ready:
                    assert time.monotonic() < deadline, 'Battle did not start'
                    page.wait_for_timeout(100)
                report['battleReadySeconds'] = round(ready[0]-started, 3)
            page.mouse.move(args.width*.5, args.height*.4)
            page.wait_for_timeout(8000)
            page.screenshot(path=str(args.output/'before.png'))
            cdp = page.context.new_cdp_session(page)
            system = browser.new_browser_cdp_session()
            cdp.send('Performance.enable')
            first_processes = system.send('SystemInfo.getProcessInfo')['processInfo']
            report['privateMemoryBefore'] = private_memory(first_processes)
            report['gpuBefore'] = gpu_snapshot()
            page.evaluate('''() => {
              window.resourceFrames=[]; window.resourceMeasuring=true;
              let previous=null;
              function sample(now){
                if(!window.resourceMeasuring)return;
                if(previous!==null)window.resourceFrames.push(now-previous);
                previous=now;requestAnimationFrame(sample);
              }
              requestAnimationFrame(sample);
            }''')
            first_processes = system.send('SystemInfo.getProcessInfo')['processInfo']
            before = {m['name']: m['value'] for m in cdp.send('Performance.getMetrics')['metrics']}
            measured_start = time.monotonic()
            page.wait_for_timeout(args.seconds*1000)
            measured_seconds = time.monotonic()-measured_start
            after = {m['name']: m['value'] for m in cdp.send('Performance.getMetrics')['metrics']}
            last_processes = system.send('SystemInfo.getProcessInfo')['processInfo']
            frames = page.evaluate('() => {window.resourceMeasuring=false;return window.resourceFrames}')
            report['measurementSeconds'] = round(measured_seconds, 3)
            report['frames'] = {'count': len(frames), 'meanMs': round(statistics.mean(frames), 3),
                                'p95Ms': round(percentile(frames,95), 3), 'p99Ms': round(percentile(frames,99), 3),
                                'maxMs': round(max(frames), 3), 'over50ms': sum(x>50 for x in frames),
                                'cadenceHz': round(1000/statistics.mean(frames), 2)}
            first_cpu = {p['id']: p['cpuTime'] for p in first_processes}
            report['processCpu'] = [dict(p, cpuSeconds=round(p['cpuTime']-first_cpu[p['id']], 3),
                corePercent=round(100*(p['cpuTime']-first_cpu[p['id']])/measured_seconds, 1))
                for p in last_processes if p['id'] in first_cpu]
            report['browserMainThreadBusyPercent'] = round(100*(after['TaskDuration']-before['TaskDuration'])/measured_seconds, 1)
            report['jsHeapUsedBytes'] = after.get('JSHeapUsedSize')
            report['wasmHeapBytes'] = page.evaluate('window.riskaiInstance.Module.HEAPU8.buffer.byteLength')
            report['privateMemoryAfter'] = private_memory(last_processes)
            report['gpuAfter'] = gpu_snapshot()
            report['webglRenderer'] = page.evaluate('''() => {const g=document.querySelector('canvas').getContext('webgl2');
              const d=g.getExtension('WEBGL_debug_renderer_info');return g.getParameter(d?d.UNMASKED_RENDERER_WEBGL:g.RENDERER)}''')
            page.screenshot(path=str(args.output/'after.png'))
            report['success'] = not report['errors']
        except Exception as error:
            report.update(success=False, failure=str(error))
        finally:
            report['totalSeconds'] = round(time.monotonic()-started, 3)
            (args.output/'result.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
            browser.close()
    print(json.dumps({key: report[key] for key in
        ['scenario', 'success', 'failure', 'frames', 'browserMainThreadBusyPercent',
         'wasmHeapBytes', 'jsHeapUsedBytes', 'gpuBefore', 'gpuAfter', 'totalSeconds'] if key in report}, ensure_ascii=False))
    return 0 if report['success'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
