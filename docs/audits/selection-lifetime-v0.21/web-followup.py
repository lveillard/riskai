"""Bounded real exported-player gesture evidence, not physical tablet validation.
No commands or Unity state are injected: only browser input. Images require
visual review; script completion is never a gameplay acceptance verdict.
"""
import json,sys,time
from pathlib import Path
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'scripts/serve_web.py').is_file())
sys.path.insert(0,str(ROOT/'.tools/web-python'))
from playwright.sync_api import sync_playwright
out=ROOT/'Captures/v21-web-lifetime-followup'
out.mkdir(exist_ok=False)
report={'buildHead':'f05c586','physicalHardware':False,'gameplayAccepted':None,'events':[],'pageErrors':[],'consoleErrors':[]}
started=time.monotonic()
with sync_playwright() as pw, (out/'console.log').open('w',encoding='utf-8') as log:
    browser=pw.chromium.launch(channel='msedge',headless=True)
    try:
        page=browser.new_page(viewport={'width':1600,'height':900},has_touch=True)
        report['browser']=browser.version
        ready=[]
        def console(m):
            log.write(f'{time.monotonic()-started:.3f} [{m.type}] {m.text}\n');log.flush()
            if 'RISKAI_STARTUP phase=ready' in m.text:ready.append(m.text)
            if m.type=='error':report['consoleErrors'].append(m.text)
        page.on('console',console)
        page.on('pageerror',lambda e:report['pageErrors'].append(str(e)))
        page.goto('http://127.0.0.1:8081/?riskai-seed=19031&riskai-players=16&riskai-map=europe',wait_until='domcontentloaded')
        page.wait_for_function('Boolean(window.riskaiInstance)',timeout=240000)
        cdp=page.context.new_cdp_session(page)
        def capture(name):
            page.screenshot(path=str(out/(name+'.png')))
            report['events'].append({'capture':name,'elapsed':round(time.monotonic()-started,3)})
            print(name,flush=True)
        def pen(kind,x,y,buttons=0,button='left'):
            cdp.send('Input.dispatchMouseEvent',{'type':kind,'x':x,'y':y,'buttons':buttons,'button':button,'pointerType':'pen','force':.5 if buttons else 0,'clickCount':1})
        def touch(kind,points):
            cdp.send('Input.dispatchTouchEvent',{'type':kind,'touchPoints':[{'id':i+11,'x':p[0],'y':p[1]} for i,p in enumerate(points)]})
        def tap(x,y):
            touch('touchStart',[(x,y)]);page.wait_for_timeout(60)
            touch('touchEnd',[])
        page.wait_for_timeout(1000)
        pen('mousePressed',1415,828,1);page.wait_for_timeout(120);pen('mouseReleased',1415,828)
        deadline=time.monotonic()+90
        while not ready and time.monotonic()<deadline:page.wait_for_timeout(300)
        if not ready:raise RuntimeError('Pen did not start battlefield')
        page.wait_for_timeout(1000)
        page.keyboard.press('F2');page.wait_for_timeout(800)
        page.keyboard.press('w');page.wait_for_timeout(200);page.keyboard.press('w')
        page.wait_for_timeout(16000)
        page.keyboard.press('F10');page.wait_for_timeout(400)
        capture('01-recruited-paused')
        # One contact enters the real area selection path; no synthetic mouse.
        touch('touchStart',[(450,180)]);page.wait_for_timeout(100)
        for pt in [(600,280),(800,420),(1000,530),(1150,620)]:
            touch('touchMove',[pt]);page.wait_for_timeout(100)
        capture('02-touch-marquee-held')
        touch('touchEnd',[]);page.wait_for_timeout(400);capture('03-touch-selection')
        # Explicit key overlap spans multiple rendered frames.
        page.keyboard.down('Control');page.wait_for_timeout(250)
        page.keyboard.down('1');page.wait_for_timeout(250)
        page.keyboard.up('1');page.wait_for_timeout(250)
        page.keyboard.up('Control');page.wait_for_timeout(250)
        capture('04-group-stored')
        touch('touchStart',[(280,785)]);page.wait_for_timeout(200)
        touch('touchEnd',[]);page.wait_for_timeout(700)
        capture('05-desktop-card-touch')
        page.keyboard.press('Escape',delay=150);page.wait_for_timeout(500)
        capture('06-cleared')
        page.keyboard.press('1',delay=150);page.wait_for_timeout(500)
        capture('07-group-recalled')
        page.mouse.click(280,785,delay=150);page.wait_for_timeout(500)
        capture('08-desktop-card-mouse')
        page.keyboard.press('1',delay=150);page.wait_for_timeout(500)
        capture('09-group-recalled-after-card')
        other=browser.new_page();other.goto('about:blank');other.bring_to_front()
        other.wait_for_timeout(1800);page.bring_to_front();page.wait_for_timeout(1000)
        capture('10-after-browser-focus-switch')
        other.close()
        report['scriptCompleted']=True
    except Exception as e:
        report['failure']=str(e);report['scriptCompleted']=False
        raise
    finally:
        browser.close();report['seconds']=round(time.monotonic()-started,3)
        (out/'result.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
