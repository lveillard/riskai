mergeInto(LibraryManager.library, {
  RiskAI_CanvasDensity: function () {
    var canvas = Module.canvas;
    return canvas && canvas.clientWidth > 0 ? canvas.width / canvas.clientWidth : 1;
  },
  RiskAI_TouchCapable: function () {
    return navigator.maxTouchPoints > 0 ? 1 : 0;
  },
  RiskAI_SafeInset: function (edge) {
    var insets = window.riskaiSafeInsets;
    return insets && edge >= 0 && edge < 4 ? insets[edge] : 0;
  }
});
