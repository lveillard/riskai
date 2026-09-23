// Touch-browser chat entry. Unity's own mobile keyboard path focuses its hidden input
// a frame after the tap, outside the user gesture, which iOS Safari ignores. This
// focuses a real, visible <input> inside the finger-lift event instead.
var RiskAIChatLibrary = {
  $RiskAIChat: {
    el: null, state: -1, text: '', armed: false, placeholder: '', maxLength: 120,
    ensure: function () {
      var chat = RiskAIChat;
      if (chat.el) return chat.el;
      var el = document.createElement('input');
      el.type = 'text';
      el.setAttribute('autocomplete', 'off');
      el.setAttribute('autocorrect', 'on');
      el.setAttribute('enterkeyhint', 'send');
      el.setAttribute('aria-label', 'Chat');
      var s = el.style;
      s.position = 'fixed'; s.left = '8px'; s.right = '8px'; s.zIndex = '2147483000';
      s.display = 'none'; s.boxSizing = 'border-box'; s.padding = '10px 12px';
      s.fontSize = '16px'; /* 16px avoids iOS auto-zoom on focus */
      s.fontFamily = 'sans-serif'; s.color = '#eeeadb';
      s.background = 'rgba(14,16,13,0.94)'; s.border = '1px solid #b8904c'; s.borderRadius = '4px';
      s.outline = 'none';
      var place = function () {
        var vv = window.visualViewport;
        var height = el.offsetHeight || 44;
        var top = vv ? vv.offsetTop + vv.height - height - 8 : window.innerHeight - height - 8;
        el.style.top = Math.max(8, top) + 'px'; el.style.bottom = 'auto';
      };
      chat.place = place;
      el.addEventListener('keydown', function (e) {
        e.stopPropagation();
        if (e.key === 'Enter') { chat.text = el.value; chat.state = 1; el.blur(); e.preventDefault(); }
        else if (e.key === 'Escape') { chat.text = ''; chat.state = 2; el.blur(); e.preventDefault(); }
      });
      el.addEventListener('keyup', function (e) { e.stopPropagation(); });
      el.addEventListener('keypress', function (e) { e.stopPropagation(); });
      el.addEventListener('blur', function () {
        if (chat.state === 0) {
          var value = (el.value || '').trim();
          chat.text = value; chat.state = value.length ? 1 : 2;
        }
        el.style.display = 'none';
      });
      if (window.visualViewport) {
        window.visualViewport.addEventListener('resize', function () { if (el.style.display !== 'none') place(); });
        window.visualViewport.addEventListener('scroll', function () { if (el.style.display !== 'none') place(); });
      }
      document.body.appendChild(el);
      chat.el = el;
      return el;
    },
    show: function () {
      var chat = RiskAIChat;
      var el = chat.ensure();
      if (chat.state === 0 && el.style.display !== 'none') { el.focus(); return; }
      el.value = ''; el.placeholder = chat.placeholder; el.maxLength = chat.maxLength;
      el.style.display = 'block'; chat.state = 0; chat.text = '';
      chat.place();
      el.focus();
    }
  },

  RiskAI_ChatArm__deps: ['$RiskAIChat'],
  RiskAI_ChatArm: function (placeholder, maxLength) {
    var chat = RiskAIChat;
    chat.placeholder = UTF8ToString(placeholder); chat.maxLength = maxLength;
    if (chat.armed) return;
    chat.armed = true;
    var fire = function () { disarm(); chat.show(); };
    var disarm = function () {
      chat.armed = false;
      window.removeEventListener('touchend', fire, true);
      window.removeEventListener('pointerup', fire, true);
    };
    window.addEventListener('touchend', fire, true);
    window.addEventListener('pointerup', fire, true);
    setTimeout(function () { if (chat.armed) disarm(); }, 1500);
  },

  RiskAI_ChatOpen__deps: ['$RiskAIChat'],
  RiskAI_ChatOpen: function (placeholder, maxLength) {
    var chat = RiskAIChat;
    chat.placeholder = UTF8ToString(placeholder); chat.maxLength = maxLength;
    chat.show();
  },

  RiskAI_ChatClose__deps: ['$RiskAIChat'],
  RiskAI_ChatClose: function () {
    var chat = RiskAIChat;
    chat.state = -1; chat.text = '';
    if (chat.el) { chat.el.style.display = 'none'; chat.el.blur(); }
  },

  RiskAI_ChatPoll__deps: ['$RiskAIChat'],
  RiskAI_ChatPoll: function () { return RiskAIChat.state; },

  RiskAI_ChatTake__deps: ['$RiskAIChat'],
  RiskAI_ChatTake: function () {
    var chat = RiskAIChat;
    var value = chat.text || '';
    chat.text = '';
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  }
};

autoAddDeps(RiskAIChatLibrary, '$RiskAIChat');
mergeInto(LibraryManager.library, RiskAIChatLibrary);
