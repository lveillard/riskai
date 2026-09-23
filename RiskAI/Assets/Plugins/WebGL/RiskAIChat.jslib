// Touch-browser chat entry. Unity's own mobile keyboard path focuses its hidden input
// a frame after the tap, outside the user gesture, which iOS Safari ignores. This
// focuses a real, visible <input> inside the finger-lift event instead.
var RiskAIChatLibrary = {
  $RiskAIChat: {
    el: null, state: -1, text: '', armed: false, placeholder: '', maxLength: 120, holding: false, blurTimer: 0,
    finish: function () {
      var chat = RiskAIChat;
      if (chat.state !== 0 || !chat.el) return;
      var value = (chat.el.value || '').trim();
      chat.text = value; chat.state = value.length ? 1 : 2;
      chat.el.style.display = 'none';
    },
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
      // A blur is finalised a moment later so a tap on a Unity control that keeps the
      // field open (the recipient chip calls RiskAI_ChatHold) can cancel it.
      el.addEventListener('blur', function () {
        if (chat.state !== 0) { el.style.display = 'none'; return; }
        clearTimeout(chat.blurTimer);
        chat.blurTimer = setTimeout(function () {
          if (chat.holding || chat.state !== 0 || document.activeElement === el) return;
          chat.finish();
        }, 350);
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
    chat.state = -1; chat.text = ''; chat.holding = false; clearTimeout(chat.blurTimer);
    if (chat.el) { chat.el.style.display = 'none'; chat.el.blur(); }
  },

  RiskAI_ChatHold__deps: ['$RiskAIChat'],
  RiskAI_ChatHold: function () {
    var chat = RiskAIChat;
    if (chat.state !== 0 || !chat.el || chat.holding) return;
    chat.holding = true; clearTimeout(chat.blurTimer);
    var refocus = function () {
      window.removeEventListener('touchend', refocus, true);
      window.removeEventListener('pointerup', refocus, true);
      if (!chat.holding) return;
      chat.holding = false;
      if (chat.state !== 0) return;
      chat.el.style.display = 'block'; chat.place(); chat.el.focus();
    };
    window.addEventListener('touchend', refocus, true);
    window.addEventListener('pointerup', refocus, true);
    setTimeout(refocus, 1500);
  },

  RiskAI_ChatPlaceholder__deps: ['$RiskAIChat'],
  RiskAI_ChatPlaceholder: function (placeholder) {
    var chat = RiskAIChat;
    chat.placeholder = UTF8ToString(placeholder);
    if (chat.el) chat.el.placeholder = chat.placeholder;
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
