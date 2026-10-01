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

(function wheelPreservesRawModesAndUnityPropagation() {
  let time = 10;
  const element = canvas(), bridge = RiskAIPen.attachWheel(element, { now: () => time });
  const pixels = element.dispatch("wheel", { deltaY: -12.5, deltaMode: 0, ctrlKey: true });
  element.dispatch("wheel", { deltaY: -100, deltaMode: 0 });
  element.dispatch("wheel", { deltaY: 3, deltaMode: 1 });
  element.dispatch("wheel", { deltaY: -1, deltaMode: 2 });
  assert(pixels.defaultPrevented, "browser page zoom/scroll is prevented");
  assert(!pixels.stopped, "wheel propagation remains available to Unity UI Toolkit");
  assert.deepStrictEqual(bridge.readSample(), [-12.5, -100, 3, -1, 0, 0, .5, .5]);
  assert.strictEqual(bridge.readSample(), null, "each frame consumes the sample exactly once");
  bridge.dispose();
})();

(function touchpadPansBothAxesAndAcceleratedMomentumCannotZoom() {
  let time = 0;
  const element = canvas(), bridge = RiskAIPen.attachWheel(element, { now: () => time });
  element.dispatch("wheel", { deltaX: 20, deltaY: 10, deltaMode: 0 });
  assert.deepStrictEqual(bridge.readSample(), [0, 0, 0, 0, -.1, .1, .5, .5]);
  time = 16;
  element.dispatch("wheel", { deltaY: 120, deltaMode: 0 });
  assert.deepStrictEqual(bridge.readSample(), [0, 0, 0, 0, 0, 1.2, .5, .5],
    "consuming a frame must not forget the ongoing pan gesture");
  time = 32;
  element.dispatch("wheel", { deltaX: -10, deltaY: -5, deltaMode: 0 });
  assert.deepStrictEqual(bridge.readSample(), [0, 0, 0, 0, .05, -.05, .5, .5]);
  time = 400;
  element.dispatch("wheel", { deltaY: 120, deltaMode: 0 });
  assert.deepStrictEqual(bridge.readSample(), [0, 120, 0, 0, 0, 0, .5, .5],
    "a later physical mouse notch still zooms");
  bridge.dispose();
})();

(function wheelBurstAccumulatesDistanceNotEventCountAndKeepsReversal() {
  let time = 0;
  const element = canvas(), bridge = RiskAIPen.attachWheel(element, { now: () => time });
  for (let index = 0; index < 40; index++) {
    time += 1;
    element.dispatch("wheel", { deltaY: -.5, deltaMode: 0 });
  }
  const burst = bridge.readSample();
  assert.deepStrictEqual(burst.slice(0, 5), [0, 0, 0, 0, 0]);
  assert(Math.abs(burst[5] + .2) < 1e-10, "pan distance accumulates across a burst");
  element.dispatch("wheel", { deltaY: 7, deltaMode: 0 });
  element.dispatch("wheel", { deltaY: -3, deltaMode: 0 });
  const reversal = bridge.readSample();
  assert(Math.abs(reversal[5] - .04) < 1e-10, "same-frame reversal is a signed net delta");
  assert.deepStrictEqual(reversal.slice(0, 4), [0, 0, 0, 0]);
  bridge.dispose();
})();

(function wheelLifecycleAndTtlDiscardStaleInput() {
  let time = 0;
  const element = canvas(), bridge = RiskAIPen.attachWheel(element, { now: () => time, maximumAgeMilliseconds: 100 });
  element.dispatch("wheel", { deltaY: 10, deltaMode: 0 });
  element.dispatch("pointerleave");
  assert.strictEqual(bridge.readSample(), null, "pointer leave discards a pending canvas wheel");
  element.dispatch("wheel", { deltaY: 10, deltaMode: 0 });
  element.ownerDocument.defaultView.dispatch("blur");
  assert.strictEqual(bridge.readSample(), null, "blur discards a pending wheel");
  element.dispatch("wheel", { deltaY: 10, deltaMode: 0 });
  element.ownerDocument.visibilityState = "hidden";
  element.ownerDocument.dispatch("visibilitychange");
  assert.strictEqual(bridge.readSample(), null, "hidden documents discard a pending wheel");
  element.ownerDocument.visibilityState = "visible";
  element.dispatch("wheel", { deltaY: 10, deltaMode: 0 });
  time = 101;
  assert.strictEqual(bridge.readSample(), null, "a scene-load-length delay expires stale wheel input");
  bridge.dispose();
})();

(function wheelDisposeRemovesListeners() {
  const element = canvas(), bridge = RiskAIPen.attachWheel(element);
  bridge.dispose();
  const event = element.dispatch("wheel", { deltaY: 100, deltaMode: 0 });
  assert(!event.defaultPrevented && !event.stopped);
  assert.strictEqual(bridge.readSample(), null);
})();

(function pinchNeverPansAndKeepsUnityUiPropagation() {
  const element = canvas(), bridge = RiskAIPen.attachWheel(element);
  const event = element.dispatch("wheel", { deltaX: 10, deltaY: -20, deltaMode: 0, ctrlKey: true });
  assert(event.defaultPrevented && !event.stopped);
  assert.deepStrictEqual(bridge.readSample(), [-20, 0, 0, 0, 0, 0, .5, .5]);
  element.dispatch("wheel", { deltaY: 20, deltaMode: 0, ctrlKey: true });
  assert.deepStrictEqual(bridge.readSample(), [20, 0, 0, 0, 0, 0, .5, .5]);
  bridge.dispose();
})();

(function horizontalScrollAndLargeDiagonalStartPanWithoutZoom() {
  const element = canvas(), bridge = RiskAIPen.attachWheel(element);
  element.dispatch("wheel", { deltaX: 100, deltaY: 120, deltaMode: 0 });
  assert.deepStrictEqual(bridge.readSample(), [0, 0, 0, 0, -.5, 1.2, .5, .5]);
  element.dispatch("wheel", { deltaX: 40, deltaY: 0, deltaMode: 0 });
  assert.deepStrictEqual(bridge.readSample(), [0, 0, 0, 0, -.2, 0, .5, .5]);
  bridge.dispose();
})();

(function sampleRemembersEventPositionAndDoesNotMergeHudIntoWorld() {
  const element = canvas(), bridge = RiskAIPen.attachWheel(element);
  element.dispatch("wheel", { deltaY: 40, deltaMode: 0, clientY: 115 });
  element.dispatch("pointermove", { pointerType: "mouse", clientY: 70 });
  assert(Math.abs(bridge.readSample()[7] - .05) < 1e-10, "use the scroll origin, not the later cursor");
  element.dispatch("wheel", { deltaY: 40, deltaMode: 0, clientY: 115 });
  element.dispatch("wheel", { deltaY: 5, deltaMode: 0, clientY: 70 });
  assert.deepStrictEqual(bridge.readSample(), [0, 0, 0, 0, 0, .05, .5, .5]);
  bridge.dispose();
})();

(function safariPinchUsesIncrementalScaleAndDoesNotDoubleApplyWheel() {
  const element = canvas(), bridge = RiskAIPen.attachWheel(element);
  assert(element.dispatch("gesturestart", { scale: 1 }).defaultPrevented);
  element.dispatch("gesturechange", { scale: 1.25 });
  element.dispatch("wheel", { deltaY: -10, deltaMode: 0, ctrlKey: true });
  let sample = bridge.readSample();
  assert(Math.abs(Math.exp(-sample[0] / 100) - 1.25) < 1e-10);
  assert.deepStrictEqual(sample.slice(1, 6), [0, 0, 0, 0, 0]);
  element.dispatch("gesturechange", { scale: 1.5 });
  sample = bridge.readSample();
  assert(Math.abs(Math.exp(-sample[0] / 100) - 1.2) < 1e-10);
  element.dispatch("gestureend");
  element.dispatch("wheel", { deltaY: 5, deltaMode: 0, ctrlKey: true });
  assert.strictEqual(bridge.readSample()[0], 5);
  bridge.dispose();
  assert(!element.dispatch("gesturestart", { scale: 1 }).defaultPrevented);
})();

(function invalidDeltasCannotPoisonTheCamera() {
  const element = canvas(), bridge = RiskAIPen.attachWheel(element);
  element.dispatch("wheel", { deltaX: Infinity, deltaY: NaN, deltaMode: 0 });
  assert.deepStrictEqual(bridge.readSample(), [0, 0, 0, 0, 0, 0, .5, .5]);
  bridge.dispose();
})();

(function jslibTransfersAllEightChannelsAndEmptyReadCannotReplayUnityWheel() {
  const fs = require("fs"), vm = require("vm");
  const heap = new Float32Array(12);
  let library;
  const element = canvas(), bridge = RiskAIPen.attachWheel(element);
  vm.runInNewContext(fs.readFileSync(require.resolve("../RiskAI/Assets/Plugins/WebGL/RiskAIPlatform.jslib"), "utf8"), {
    LibraryManager: { library: {} }, mergeInto: (_, value) => { library = value; },
    window: { riskaiWheel: bridge }, HEAPF32: heap
  });
  element.dispatch("wheel", { deltaX: 50, deltaY: 25, deltaMode: 0 });
  assert.strictEqual(library.RiskAI_ReadWheelDeltas(8), 1);
  assert.deepStrictEqual(Array.from(heap.slice(2, 10)), [0, 0, 0, 0, -.25, .25, .5, .5]);
  assert.strictEqual(library.RiskAI_ReadWheelDeltas(8), 1, "an empty bridge is still authoritative over Unity scroll");
  assert.deepStrictEqual(Array.from(heap.slice(2, 10)), Array(8).fill(0));
  bridge.dispose();
})();

(function safariScreenTouchesRemainOwnedByUnityWithoutTrailingPinch() {
  const element = canvas(), bridge = RiskAIPen.attachWheel(element);
  const first = element.dispatch("touchstart", { touches: [{}] });
  assert(!first.defaultPrevented && !first.stopped);
  const start = element.dispatch("gesturestart", { scale: 1 });
  element.dispatch("touchstart", { touches: [{}, {}] });
  element.dispatch("gesturechange", { scale: 1.5 });
  element.dispatch("gestureend");
  element.ownerDocument.dispatch("touchend", { touches: [] });
  assert(!start.defaultPrevented && !start.stopped);
  assert.strictEqual(bridge.readSample(), null, "screen pinch cannot be replayed after the last touch lifts");
  element.dispatch("gesturestart", { scale: 1 });
  element.dispatch("gesturechange", { scale: 1.5 });
  assert(bridge.readSample()[0] < 0, "a later trackpad pinch on the same tablet still works");
  bridge.dispose();
})();

console.log("browser pen/wheel bridge tests: 24 passed");
