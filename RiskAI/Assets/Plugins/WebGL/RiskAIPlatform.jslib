mergeInto(LibraryManager.library, {
  RiskAI_CanvasDensity: function () {
    var canvas = Module.canvas;
    return canvas && canvas.clientWidth > 0 ? canvas.width / canvas.clientWidth : 1;
  },
  RiskAI_TouchCapable: function () {
    return navigator.maxTouchPoints > 0 ? 1 : 0;
  },
  RiskAI_PrefersSpanish: function () {
    var tag = (navigator.languages && navigator.languages.length ? navigator.languages[0] : navigator.language) || "";
    return /^es([-_]|$)/i.test(tag) ? 1 : 0;
  },
  RiskAI_SafeInset: function (edge) {
    var insets = window.riskaiSafeInsets;
    return insets && edge >= 0 && edge < 4 ? insets[edge] : 0;
  },
  RiskAI_ReadPenSample: function (destination) {
    var bridge = window.riskaiPen;
    if (!bridge) return 0;
    var sample = bridge.readSample();
    if (!sample) return 0;
    for (var i = 0; i < 8; i++) HEAPF32[(destination >> 2) + i] = sample[i];
    return 1;
  },
  RiskAI_ReadWheelDeltas: function (destination) {
    var bridge = window.riskaiWheel;
    if (!bridge) return 0;
    var sample = bridge.readSample();
    if (!sample) {
      for (var empty = 0; empty < 4; empty++) HEAPF32[(destination >> 2) + empty] = 0;
      return 1;
    }
    for (var i = 0; i < 4; i++) HEAPF32[(destination >> 2) + i] = sample[i];
    return 1;
  }
});
