"""Bounded real exported-player gesture evidence, not physical tablet validation.
No commands or Unity state are injected: only browser input. Images require
visual review; script completion is never a gameplay acceptance verdict.
"""
import json,sys,time
from pathlib import Path
ROOT=next(p for p in Path(__file__).resolve().parents if (p/'scripts/serve_web.py').is_file())
sys.path.insert(0,str(ROOT/'.tools/web-python'))
from playwright.sync_api import sync_playwright
out=ROOT/'Captures/v21-web-lifetime-final'
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
        page.keyboard.press('Control+1');page.wait_for_timeout(200)
        # Exercise a real UI card tap while paused, then inspect the shared roster
        # in both compact orientations before resuming battle gestures.
        tap(280,785);page.wait_for_timeout(500);capture('03b-desktop-card-tap')
        page.keyboard.press('Escape');page.wait_for_timeout(300);capture('03ba-group-cleared')
        page.keyboard.press('1');page.wait_for_timeout(400);capture('03bb-group-recalled')
        def select_army_area():
            touch('touchStart',[(450,180)]);page.wait_for_timeout(100)
            touch('touchMove',[(1150,620)]);page.wait_for_timeout(150)
            touch('touchEnd',[]);page.wait_for_timeout(400)
        select_army_area()
        page.set_viewport_size({'width':1024,'height':768});page.wait_for_timeout(600);capture('03c-landscape-roster')
        page.set_viewport_size({'width':768,'height':1024});page.wait_for_timeout(600);capture('03d-portrait-roster')
        tap(48,910);page.wait_for_timeout(500);capture('03e-portrait-card-tap')
        page.set_viewport_size({'width':1600,'height':900});page.wait_for_timeout(600)
        select_army_area()

        # Test contextual double-tap while live, then allow visibly resolved motion.
        page.keyboard.press('F10');page.wait_for_timeout(300)
        tap(870,435);page.wait_for_timeout(80);tap(870,435)
        page.wait_for_timeout(250);capture('04-double-tap-order')
        page.wait_for_timeout(4500);capture('05-after-double-tap')
        touch('touchStart',[(710,470),(750,470)]);page.wait_for_timeout(110)
        touch('touchEnd',[]);page.wait_for_timeout(250);capture('06-two-finger-order')
        page.wait_for_timeout(5000);capture('07-after-two-finger')
        # The pen tip must select mobile units through the same area path.
        pen('mousePressed',450,180,1);page.wait_for_timeout(100)
        for x,y in [(600,280),(800,420),(1000,530),(1150,620)]:
            pen('mouseMoved',x,y,1);page.wait_for_timeout(100)
        capture('08-pen-marquee-held')
        pen('mouseReleased',1150,620);page.wait_for_timeout(400);capture('09-pen-selection')
        pen('mousePressed',850,475,2,'right');page.wait_for_timeout(100)
        pen('mouseReleased',850,475,0,'right');page.wait_for_timeout(250);capture('10-pen-barrel-order')
        page.wait_for_timeout(4000);capture('11-after-pen-barrel')
        page.keyboard.press('F10');page.wait_for_timeout(300)
        page.set_viewport_size({'width':1024,'height':768});page.wait_for_timeout(500);capture('12-tablet-landscape-paused')
        page.set_viewport_size({'width':768,'height':1024});page.wait_for_timeout(500);capture('13-tablet-portrait-paused')
        report['scriptCompleted']=True
    except Exception as e:
        report['failure']=str(e);report['scriptCompleted']=False
        raise
    finally:
        browser.close();report['seconds']=round(time.monotonic()-started,3)
        (out/'result.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
