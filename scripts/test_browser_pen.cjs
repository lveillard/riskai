const assert = require("assert");
const RiskAIPen = require("../RiskAI/Assets/WebGLTemplates/RiskAI/riskai-pen.js");

class Target {
  constructor() { this.listeners = new Map(); }
  addEventListener(type, handler) {
    if (!this.listeners.has(type)) this.listeners.set(type, []);
    this.listeners.get(type).push(handler);
  }
  removeEventListener(type, handler) {
    const handlers = this.listeners.get(type) || [];
    const index = handlers.indexOf(handler);
    if (index >= 0) handlers.splice(index, 1);
  }
  dispatch(type, fields = {}) {
    const event = {
      type, pointerType: "pen", pointerId: 7, clientX: 110, clientY: 70,
      buttons: 1, pressure: .5, tiltX: 10, tiltY: -20, timeStamp: 123,
      defaultPrevented: false, stopped: false,
      preventDefault() { this.defaultPrevented = true; },
      stopImmediatePropagation() { this.stopped = true; },
      ...fields
    };
    for (const handler of [...(this.listeners.get(type) || [])]) handler(event);
    return event;
  }
}

function canvas() {
  const document = new Target();
  document.visibilityState = "visible";
  const window = new Target();
  document.defaultView = window;
  const value = new Target();
  value.ownerDocument = document;
  value.getBoundingClientRect = () => ({ left: 10, top: 20, width: 200, height: 100 });
  value.captured = [];
  value.focusCalls = [];
  value.setPointerCapture = id => value.captured.push(id);
  value.focus = options => value.focusCalls.push(options || null);
  return value;
}

function rows(bridge) {
  const result = [];
  for (;;) {
    const row = bridge.readSample();
    if (row === null) return result;
    result.push(row);
  }
}

(function coordinateAndCoalesce() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  const down = element.dispatch("pointerdown", { clientX: 110, clientY: 70, buttons: 1, timeStamp: 5 });
  element.dispatch("pointermove", { clientX: 130, clientY: 80, buttons: 1, timeStamp: 6 });
  element.dispatch("pointermove", { clientX: 170, clientY: 100, buttons: 1, timeStamp: 7 });
  assert(down.defaultPrevented && down.stopped);
  assert.deepStrictEqual(element.captured, [7]);
  assert.deepStrictEqual(element.focusCalls, [{ preventScroll: true }]);
  const output = rows(bridge);
  assert.strictEqual(output.length, 2, "consecutive same-button moves coalesce");
  assert.deepStrictEqual(output[0], [0, .5, .5, 1, .5, 10, -20, 5]);
  assert.deepStrictEqual(output[1], [0, .8, .8, 1, .5, 10, -20, 7]);
  bridge.dispose();
})();

(function buttonEdgesAndUpArePreserved() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  element.dispatch("pointerdown", { buttons: 1 });
  element.dispatch("pointermove", { buttons: 3 });
  element.dispatch("pointermove", { buttons: 1 });
  element.dispatch("pointerup", { buttons: 0 });
  const output = rows(bridge);
  assert.deepStrictEqual(output.map(row => row[3]), [1, 3, 1, 0]);
  bridge.dispose();
})();

(function cancelAndQuarantineInterruptedPen() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  element.dispatch("pointerdown");
  element.dispatch("pointerup", { buttons: 0 });
  element.dispatch("lostpointercapture");
  assert.deepStrictEqual(rows(bridge).map(row => row[0]), [0, 0], "lost capture after up is not cancellation");

  element.dispatch("pointerdown");
  element.dispatch("lostpointercapture");
  let output = rows(bridge);
  assert.strictEqual(output.length, 1, "unexpected lost capture clears pending states");
  assert.strictEqual(output[0][0], 1);
  element.dispatch("pointermove", { buttons: 1 });
  element.dispatch("pointerdown", { pointerId: 8, buttons: 1 });
  assert.strictEqual(bridge.readSample(), null, "held stream and a new down remain quarantined");
  element.dispatch("pointermove", { buttons: 0 });
  element.dispatch("pointerdown", { pointerId: 8, buttons: 1 });
  assert.deepStrictEqual(rows(bridge).map(row => row[3]), [1], "neutral move releases quarantine for the next down");

  element.dispatch("pointercancel", { pointerId: 8, buttons: 0 });
  output = rows(bridge);
  assert.strictEqual(output.length, 1, "pointercancel clears any queued press or move");
  assert.strictEqual(output[0][0], 1);
  element.dispatch("pointermove", { pointerId: 8, buttons: 1 });
  element.dispatch("pointerup", { pointerId: 8, buttons: 0 });
  assert.strictEqual(bridge.readSample(), null, "cancelled pen cannot resurrect a held stroke");
  bridge.dispose();
})();

(function onlyOnePenCanOwnTheVirtualPen() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  element.dispatch("pointerdown", { pointerId: 7, buttons: 1 });
  element.dispatch("pointerdown", { pointerId: 8, buttons: 1 });
  element.dispatch("pointermove", { pointerId: 8, buttons: 1, clientX: 190 });
  const output = rows(bridge);
  assert.strictEqual(output.length, 1);
  assert.strictEqual(output[0][1], .5, "secondary pen position never replaces owner");
  bridge.dispose();
})();

(function cancelledPenMayBeginANewContactWithItsOwnIdOnly() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  element.dispatch("pointerdown", { pointerId: 7, buttons: 1 });
  element.dispatch("pointercancel", { pointerId: 7, buttons: 0 });
  element.dispatch("pointerdown", { pointerId: 8, buttons: 1 });
  element.dispatch("pointerdown", { pointerId: 7, buttons: 1 });
  const output = rows(bridge);
  assert.deepStrictEqual(output.map(row => [row[0], row[3]]), [[1, 0], [0, 1]],
    "a fresh down clears only its own cancelled quarantine");
  bridge.dispose();
})();

(function unownedHeldMoveCannotSynthesizeAPress() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  element.dispatch("pointermove", { pointerId: 7, buttons: 1 });
  assert.strictEqual(bridge.readSample(), null, "a held move that began outside has no owned down");
  bridge.dispose();
})();

(function captureFailureUsesDocumentTerminalEvents() {
  const element = canvas();
  element.setPointerCapture = () => { throw new Error("capture denied"); };
  const bridge = RiskAIPen.attach(element);
  element.dispatch("pointerdown", { pointerId: 7, buttons: 1 });
  element.ownerDocument.dispatch("pointerup", { pointerId: 7, buttons: 0 });
  assert.deepStrictEqual(rows(bridge).map(row => row[3]), [1, 0],
    "document up closes a stroke when capture is unavailable");

  element.dispatch("pointerdown", { pointerId: 8, buttons: 1 });
  element.ownerDocument.dispatch("pointercancel", { pointerId: 8, buttons: 0 });
  const output = rows(bridge);
  assert.strictEqual(output.length, 1, "document cancel replaces a pending stroke with one cancel");
  assert.strictEqual(output[0][0], 1);
  bridge.dispose();
})();

(function unrelatedDocumentPenEventsAreNotConsumed() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  element.dispatch("pointerdown", { pointerId: 7, buttons: 1 });
  const foreignUp = element.ownerDocument.dispatch("pointerup", { pointerId: 8, buttons: 0 });
  const foreignCancel = element.ownerDocument.dispatch("pointercancel", { pointerId: 8, buttons: 0 });
  assert(!foreignUp.defaultPrevented && !foreignUp.stopped,
    "unowned document pointerup remains available to the page");
  assert(!foreignCancel.defaultPrevented && !foreignCancel.stopped,
    "unowned document pointercancel remains available to the page");
  element.ownerDocument.dispatch("pointerup", { pointerId: 7, buttons: 0 });
  assert.deepStrictEqual(rows(bridge).map(row => row[3]), [1, 0],
    "the owned document pointerup still finishes the bridge stroke");
  bridge.dispose();
})();

(function buttonsAreSanitizedToTheSupportedSixBits() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  element.dispatch("pointerdown", { buttons: 65 });
  element.dispatch("pointermove", { buttons: 127 });
  element.dispatch("pointerup", { buttons: 0 });
  assert.deepStrictEqual(rows(bridge).map(row => row[3]), [1, 63, 0],
    "vendor button bits are stripped without dropping valid pointer state");
  bridge.dispose();
})();

(function lifecycleAndOverflowQuarantine() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  element.dispatch("pointerdown");
  for (let i = 0; i < 64; i++) element.dispatch("pointermove", { buttons: i & 1 ? 1 : 3, timeStamp: i });
  let output = rows(bridge);
  assert.strictEqual(output.length, 1);
  assert.strictEqual(output[0][0], 1, "overflow discards states and publishes cancel");
  element.dispatch("pointermove", { buttons: 1 });
  element.dispatch("pointerup", { buttons: 0 });
  assert.strictEqual(bridge.readSample(), null, "quarantined pointer does not emit a surprise up");

  element.dispatch("pointerdown", { pointerId: 9 });
  element.ownerDocument.defaultView.dispatch("blur");
  output = rows(bridge);
  assert.strictEqual(output.length, 1);
  assert.strictEqual(output[0][0], 1);
  element.dispatch("pointermove", { pointerId: 9, buttons: 1 });
  assert.strictEqual(bridge.readSample(), null, "blurred held pen cannot restart a stroke");
  element.dispatch("pointermove", { pointerId: 9, buttons: 0 });
  element.dispatch("pointerdown", { pointerId: 9, buttons: 1 });
  assert.deepStrictEqual(rows(bridge).map(row => row[3]), [1]);
  bridge.dispose();
})();

(function compatibilityMouseDoesNotBlockTouch() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  element.dispatch("pointerdown");
  for (const type of ["mousedown", "mouseup", "click", "dblclick", "contextmenu"]) {
    const event = element.dispatch(type, { pointerType: "mouse" });
    assert(event.defaultPrevented && event.stopped, type + " is suppressed during the pen quarantine window");
  }
  const touch = element.dispatch("pointerdown", { pointerType: "touch" });
  assert(!touch.defaultPrevented && !touch.stopped, "real touch pointer events remain untouched");
  bridge.dispose();
})();

(function disposeRemovesEveryListener() {
  const element = canvas(), bridge = RiskAIPen.attach(element);
  bridge.dispose();
  const event = element.dispatch("pointerdown");
  assert(!event.defaultPrevented && !event.stopped);
  assert.strictEqual(bridge.readSample(), null);
})();

console.log("browser pen bridge tests: 12 passed");
