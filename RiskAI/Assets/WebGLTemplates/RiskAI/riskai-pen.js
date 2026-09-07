(function(root, factory) {
  var api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  else root.RiskAIPen = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function() {
  "use strict";

  var MaxQueue = 64;
  var CompatibilityMouseMilliseconds = 500;

  function attach(canvas) {
    if (!canvas || typeof canvas.addEventListener !== "function")
      throw new TypeError("RiskAIPen.attach requires a canvas EventTarget");

    var queue = [];
    var disposed = false;
    var activePointerId = null;
    var expectedLostCaptureId = null;
    var quarantinedPointerId = null;
    var lastRow = [0, .5, .5, 0, 0, 0, 0, 0];
    var suppressMouseUntil = 0;
    var ownerDocument = canvas.ownerDocument || null;
    var ownerWindow = ownerDocument && ownerDocument.defaultView ? ownerDocument.defaultView :
      (typeof window !== "undefined" ? window : null);

    function now() {
      return typeof performance !== "undefined" && performance.now ? performance.now() : Date.now();
    }

    function clamp01(value) {
      value = Number(value);
      if (!isFinite(value)) return 0;
      return value < 0 ? 0 : value > 1 ? 1 : value;
    }

    function finite(value, fallback) {
      value = Number(value);
      return isFinite(value) ? value : fallback;
    }

    function buttonsFor(event) {
      // The C# bridge accepts a six-bit integer. Preserve valid DOM button bits
      // while stripping vendor/X buttons instead of making an otherwise good row invalid.
      return (Math.max(0, finite(event.buttons, 0)) | 0) & 63;
    }

    function rowFor(event, kind) {
      var rect = canvas.getBoundingClientRect();
      var width = finite(rect.width, 0);
      var height = finite(rect.height, 0);
      var x = width > 0 ? clamp01((finite(event.clientX, rect.left) - rect.left) / width) : lastRow[1];
      var y = height > 0 ? clamp01((finite(event.clientY, rect.top) - rect.top) / height) : lastRow[2];
      return [
        kind,
        x,
        y,
        buttonsFor(event),
        clamp01(finite(event.pressure, 0)),
        finite(event.tiltX, 0),
        finite(event.tiltY, 0),
        finite(event.timeStamp, now())
      ];
    }

    function eventPointerId(event) {
      return event && event.pointerId !== undefined ? event.pointerId : null;
    }

    function consumePenDomEvent(event) {
      if (!event || event.pointerType !== "pen") return false;
      suppressMouseUntil = Math.max(suppressMouseUntil, now() + CompatibilityMouseMilliseconds);
      if (event.preventDefault) event.preventDefault();
      if (event.stopImmediatePropagation) event.stopImmediatePropagation();
      return true;
    }

    function enqueueCancel(event) {
      var cancel = rowFor(event || {}, 1);
      cancel[3] = 0;
      cancel[4] = 0;
      queue.length = 0;
      queue.push({ row: cancel, edge: "cancel" });
      lastRow = cancel;
    }

    function overflow(event) {
      var pointerId = eventPointerId(event);
      queue.length = 0;
      enqueueCancel(event);
      quarantinedPointerId = pointerId;
      activePointerId = null;
      expectedLostCaptureId = null;
    }

    function enqueueState(event, source) {
      var pointerId = eventPointerId(event);
      if (quarantinedPointerId !== null && pointerId === quarantinedPointerId) return;
      var row = rowFor(event, 0);
      var previous = queue.length > 0 ? queue[queue.length - 1] : null;
      // A move is disposable only if it cannot hide a press/release or a barrel transition.
      if (source === "move" && previous && previous.edge === "move" && previous.row[3] === row[3]) {
        previous.row = row;
        lastRow = row;
        return;
      }
      if (queue.length >= MaxQueue) {
        overflow(event);
        return;
      }
      queue.push({ row: row, edge: source });
      lastRow = row;
    }

    function focusCanvas() {
      if (!canvas.focus) return;
      try { canvas.focus({ preventScroll: true }); }
      catch (_) { canvas.focus(); }
    }

    function clearQuarantineOnRelease(event) {
      if (quarantinedPointerId === null || eventPointerId(event) !== quarantinedPointerId) return false;
      quarantinedPointerId = null;
      expectedLostCaptureId = null;
      return true;
    }

    function onPointerDown(event) {
      if (!consumePenDomEvent(event)) return;
      var pointerId = eventPointerId(event);
      // A second id cannot displace the owner. For the quarantined id, however, a
      // fresh pointerdown is proof of a new contact after an OS cancel.
      if (activePointerId !== null) return;
      if (quarantinedPointerId !== null) {
        if (pointerId !== quarantinedPointerId) return;
        quarantinedPointerId = null;
      }
      focusCanvas();
      activePointerId = pointerId;
      expectedLostCaptureId = null;
      if (canvas.setPointerCapture && pointerId !== null) {
        try { canvas.setPointerCapture(pointerId); } catch (_) { /* Browser rejected capture. */ }
      }
      enqueueState(event, "down");
    }

    function onPointerMove(event) {
      if (!consumePenDomEvent(event)) return;
      var pointerId = eventPointerId(event);
      if (quarantinedPointerId !== null) {
        // Some browsers keep sending held moves after focus/capture loss. A neutral
        // move is the only non-terminal proof that the quarantined pen is no longer down.
        if (pointerId === quarantinedPointerId && finite(event.buttons, 0) === 0) clearQuarantineOnRelease(event);
        return;
      }
      if (activePointerId === null) {
        // A contact which began outside the canvas has no matching down state here;
        // never synthesize a press that would later have no owned release.
        if (buttonsFor(event) !== 0) return;
      } else if (pointerId !== activePointerId) return;
      enqueueState(event, "move");
    }

    function onPointerUp(event) {
      if (!consumePenDomEvent(event)) return;
      var pointerId = eventPointerId(event);
      if (clearQuarantineOnRelease(event)) return;
      if (activePointerId !== pointerId) return;
      enqueueState(event, "up");
      activePointerId = null;
      expectedLostCaptureId = pointerId;
    }

    function onPointerCancel(event) {
      if (!consumePenDomEvent(event)) return;
      var pointerId = eventPointerId(event);
      if (quarantinedPointerId === pointerId) return;
      if (activePointerId !== pointerId) return;
      enqueueCancel(event);
      activePointerId = null;
      expectedLostCaptureId = null;
      // Keep this id quarantined: platforms can emit a held move after cancel.
      quarantinedPointerId = pointerId;
    }

    function onLostPointerCapture(event) {
      if (!consumePenDomEvent(event)) return;
      var pointerId = eventPointerId(event);
      // pointerup normally releases capture; that is a successful completion, not a cancel.
      if (expectedLostCaptureId !== null && pointerId === expectedLostCaptureId) {
        expectedLostCaptureId = null;
        return;
      }
      if (quarantinedPointerId === pointerId) return;
      if (activePointerId === pointerId) {
        enqueueCancel(event);
        activePointerId = null;
        quarantinedPointerId = pointerId;
      }
    }

    function cancelFromLifecycle() {
      if (disposed) return;
      if (activePointerId !== null || queue.length > 0) enqueueCancel({});
      if (activePointerId !== null) quarantinedPointerId = activePointerId;
      activePointerId = null;
      expectedLostCaptureId = null;
    }

    function onBlur() { cancelFromLifecycle(); }
    function onVisibilityChange() {
      if (ownerDocument && ownerDocument.visibilityState === "hidden") cancelFromLifecycle();
    }

    function isTrackedDocumentPen(event) {
      if (!event || event.pointerType !== "pen") return false;
      var pointerId = eventPointerId(event);
      if (pointerId === null) return false;
      return pointerId === activePointerId || pointerId === quarantinedPointerId ||
        pointerId === expectedLostCaptureId;
    }

    // Document-level terminal events are only a fallback when pointer capture is
    // unavailable. Do not consume another canvas or page's stylus interaction.
    function onDocumentPointerUp(event) {
      if (!isTrackedDocumentPen(event)) return;
      onPointerUp(event);
    }

    function onDocumentPointerCancel(event) {
      if (!isTrackedDocumentPen(event)) return;
      onPointerCancel(event);
    }

    function onDocumentLostPointerCapture(event) {
      if (!isTrackedDocumentPen(event)) return;
      onLostPointerCapture(event);
    }

    function suppressCompatibilityMouse(event) {
      if (disposed || now() >= suppressMouseUntil) return;
      if (event.preventDefault) event.preventDefault();
      if (event.stopImmediatePropagation) event.stopImmediatePropagation();
    }

    var captureOptions = { capture: true, passive: false };
    canvas.addEventListener("pointerdown", onPointerDown, captureOptions);
    canvas.addEventListener("pointermove", onPointerMove, captureOptions);
    canvas.addEventListener("pointerup", onPointerUp, captureOptions);
    canvas.addEventListener("pointercancel", onPointerCancel, captureOptions);
    canvas.addEventListener("lostpointercapture", onLostPointerCapture, captureOptions);
    ["mousedown", "mousemove", "mouseup", "click", "dblclick", "contextmenu"].forEach(function(type) {
      canvas.addEventListener(type, suppressCompatibilityMouse, captureOptions);
    });
    if (ownerWindow) ownerWindow.addEventListener("blur", onBlur, captureOptions);
    if (ownerDocument) {
      // Canvas capture can fail or be released outside the element. These document
      // listeners see the terminal event first for in-canvas contacts too; the id
      // ownership checks in the handlers make the later canvas pass a no-op.
      ownerDocument.addEventListener("pointerup", onDocumentPointerUp, captureOptions);
      ownerDocument.addEventListener("pointercancel", onDocumentPointerCancel, captureOptions);
      ownerDocument.addEventListener("lostpointercapture", onDocumentLostPointerCapture, captureOptions);
      ownerDocument.addEventListener("visibilitychange", onVisibilityChange, captureOptions);
    }

    return {
      readSample: function() {
        if (queue.length === 0) return null;
        return queue.shift().row;
      },
      dispose: function() {
        if (disposed) return;
        disposed = true;
        canvas.removeEventListener("pointerdown", onPointerDown, captureOptions);
        canvas.removeEventListener("pointermove", onPointerMove, captureOptions);
        canvas.removeEventListener("pointerup", onPointerUp, captureOptions);
        canvas.removeEventListener("pointercancel", onPointerCancel, captureOptions);
        canvas.removeEventListener("lostpointercapture", onLostPointerCapture, captureOptions);
        ["mousedown", "mousemove", "mouseup", "click", "dblclick", "contextmenu"].forEach(function(type) {
          canvas.removeEventListener(type, suppressCompatibilityMouse, captureOptions);
        });
        if (ownerWindow) ownerWindow.removeEventListener("blur", onBlur, captureOptions);
        if (ownerDocument) {
          ownerDocument.removeEventListener("pointerup", onDocumentPointerUp, captureOptions);
          ownerDocument.removeEventListener("pointercancel", onDocumentPointerCancel, captureOptions);
          ownerDocument.removeEventListener("lostpointercapture", onDocumentLostPointerCapture, captureOptions);
          ownerDocument.removeEventListener("visibilitychange", onVisibilityChange, captureOptions);
        }
        queue.length = 0;
      }
    };
  }

  return { attach: attach };
});
