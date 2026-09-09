"""Capture the real WebGL HUD and all four maps at a chosen viewport and DPR.

Requires Playwright and Edge. The explicit review URL pauses AI and grants only
fixture purchases. Reports actual UI Toolkit rectangles alongside browser images;
this is desktop touch emulation, not a physical-phone performance measurement.
Example: python scripts/check_responsive_hud.py v22-phone --map europe --width 390 --height 844
"""
import argparse,json,sys,time
from pathlib import Path
root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'.tools/web-python'))
from playwright.sync_api import sync_playwright
p=argparse.ArgumentParser();p.add_argument('output');p.add_argument('--width',type=int,default=390);p.add_argument('--height',type=int,default=844);p.add_argument('--dpr',type=float,default=2);p.add_argument('--url',default='http://127.0.0.1:8081');p.add_argument('--map',default='europe',choices=['classic','riverlands','europe','world']);a=p.parse_args()
folder=root/'Captures'/a.output;folder.mkdir(exist_ok=False)
report={'map':a.map,'viewport':[a.width,a.height],'dpr':a.dpr,'physicalMobile':False,'errors':[],'layouts':{},'captures':[]}
ready=[];start=time.monotonic()
with sync_playwright() as pw,(folder/'console.log').open('w',encoding='utf-8') as log:
    browser=pw.chromium.launch(channel='msedge',headless=True)
    page=browser.new_page(viewport={'width':a.width,'height':a.height},device_scale_factor=a.dpr,has_touch=a.width<1100)
    def console(m):
        log.write(f'{time.monotonic()-start:.3f} [{m.type}] {m.text}\n');log.flush()
        if 'RISKAI_STARTUP phase=ready' in m.text:ready.append(True)
        if m.text.startswith('RISKAI_UI_REVIEW {'):
            layout=json.loads(m.text.split(' ',1)[1]);report['layouts'][layout['stage']]=layout
        if m.type=='error' and '404' not in m.text:report['errors'].append(m.text)
    page.on('console',console);page.on('pageerror',lambda e:report['errors'].append(str(e)))
    def send(method,host='RiskAI · Bootstrap',arg=None):
        page.evaluate('([h,m,a])=>a===null?window.riskaiInstance.SendMessage(h,m):window.riskaiInstance.SendMessage(h,m,a)',[host,method,arg])
    def shot(name):
        page.screenshot(path=str(folder/(name+'.png')));report['captures'].append(name)
    def review(stage):
        send('Review','RiskAI responsive UI capture',stage)
        deadline=time.monotonic()+20
        while stage not in report['layouts']:
            if time.monotonic()>deadline:raise TimeoutError(stage)
            page.wait_for_timeout(50)
        page.wait_for_timeout(150);shot(stage)
    def click_named(name,layout):
        e=next(e for e in report['layouts'][layout]['elements'] if e['name']==name)
        r=e['rect'];page.mouse.click(r['x']+r['width']/2,r['y']+r['height']/2)
        page.wait_for_timeout(400)
    try:
        page.goto(a.url+'/?riskai-map='+a.map+'&riskai-seed=19031&riskai-players=16&riskai-ui-capture=review',wait_until='domcontentloaded')
        page.wait_for_function('window.riskaiInstance != null',timeout=180000);page.wait_for_timeout(6500);shot('menu')
        send('StartBattle','RiskAI · Front End')
        deadline=time.monotonic()+120
        while not ready:
            if time.monotonic()>deadline:raise TimeoutError('battle')
            page.wait_for_timeout(50)
        page.wait_for_timeout(3500)
        for stage in ['empty','city','queue']:review(stage)
        send('TogglePause');page.wait_for_timeout(6500);send('SelectAll');send('FocusSelection');send('TogglePause');page.wait_for_timeout(1000);shot('army-actions')
        review('harbor')
        click_named('HUD gold button','harbor');shot('gold-real-click')
        review('income')
        send('Review','RiskAI responsive UI capture','empty');page.wait_for_timeout(1800)
        click_named('HUD cities button','empty');shot('cities-real-click')
        review('ranking');review('strategic')
        for stage in ['north','south','coast']:review(stage)
        # Explicit responsive checks against actual resolved UIToolkit layout.
        for stage in ['empty','city','queue','harbor']:
            layout=report['layouts'][stage]
            for e in layout['elements']:
                if e['name'] in ['HUD gold button','HUD cities button','HUD units button']:
                    r=e['rect'];assert r['x']>=-1 and r['x']+r['width']<=a.width+1,(stage,e)
            if stage=='empty':assert not any(e['name']=='HUD footer' for e in layout['elements'])
        report['success']=not report['errors']
    except Exception as e:report['success']=False;report['failure']=str(e)
    finally:
        report['seconds']=round(time.monotonic()-start,2)
        (folder/'result.json').write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
        browser.close()
print(json.dumps({k:v for k,v in report.items() if k!='layouts'},ensure_ascii=False));raise SystemExit(0 if report['success'] else 1)
