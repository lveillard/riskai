using RiskAI.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RiskAI
{
    /// <summary>Tweens the displayed gold toward a target after round income. Pure and time-driven.</summary>
    public struct GoldTween
    {
        public const float Seconds = .5f;
        public int From, To;
        public float Start;
        public bool Active;
        public void Begin(int from, int to, float now) { From = from; To = to; Start = now; Active = from != to; }
        /// <summary>Displayed value at <paramref name="now"/>; the true balance once finished or when it no longer matches.</summary>
        public int Value(int actual, float now)
        {
            if (!Active) return actual;
            // Any other change (purchase, bounty) snaps to the real balance at once.
            if (actual != To) { Active = false; return actual; }
            float t = Mathf.Clamp01((now - Start) / Seconds);
            if (t >= 1) { Active = false; return actual; }
            float eased = 1 - (1 - t) * (1 - t) * (1 - t);
            return Mathf.RoundToInt(Mathf.Lerp(From, To, eased));
        }
    }

    /// <summary>Feedback overlay: recent message log, toasts, chat, gold juice and attack alerts.</summary>
    public sealed partial class BattleHud
    {
        const int DesktopLogRows = 4, CompactLogRows = 3;
        const float ToastSeconds = 2.6f;
        static readonly Color AlertRed = new Color(1f, .36f, .3f);
        GameObject feedbackHost;
        RtsUiRuntime feedbackUi;
        VisualElement feedbackRoot, logBox, toastBox, chatBar;
        readonly Label[] logRows = new Label[DesktopLogRows];
        readonly int[] logRowKeys = new int[DesktopLogRows];
        readonly MessageEntry[] logRowEntries = new MessageEntry[DesktopLogRows];
        Label toastLabel, goldFloat;
        Button chatButton;
        TextField chatField;
        readonly ChatChannel chat = new ChatChannel();
        bool chatOpen, webChat;
        float goldFloatStart = -10, goldPulseStart = -10, goldFlashStart = -10, toastStart = -10, lastTrained = -10;
        GoldTween goldTween;
        readonly System.Collections.Generic.Queue<(string text, Color color, bool big)> toasts = new System.Collections.Generic.Queue<(string, Color, bool)>();
        bool feedbackCompact, goldTweenShown;
        Texture2D pingTexture;

        /// <summary>The chat channel; a future network transport replaces <see cref="ChatChannel.Transport"/>.</summary>
        public ChatChannel Chat => chat;
        int DisplayedGold => goldTween.Value(hud.Gold, Time.unscaledTime);

        void InitializeFeedback()
        {
            GameFeel.Attach(gameObject, session, cam);
            Sfx.Attach(gameObject, session, controller.CameraRig, cam);
            Music.Attach(gameObject);
            feedbackHost = new GameObject("Battle HUD feedback");
            // A UIDocument nested under another UIDocument must share its PanelSettings,
            // so the overlay host is a sibling of the HUD rather than its child.
            feedbackHost.transform.SetParent(transform.parent, false);
            feedbackUi = RtsUiRuntime.Attach(feedbackHost, "Battle feedback", 21);
            BuildFeedbackUi();
            var feedback = session.Feedback;
            feedback.MessagePosted += OnFeedbackMessage;
            feedback.Captured += OnFeedbackCapture;
            feedback.IncomeReceived += OnFeedbackIncome;
            feedback.Damaged += OnFeedbackDamage;
            feedback.WinnerDecided += OnFeedbackWinner;
            feedback.SoldierSpawned += OnFeedbackSpawned;
            session.PlayerEliminated += OnFeedbackEliminated;
            chat.MessageReceived += OnChatReceived;
        }

        void DisposeFeedback()
        {
            if (chatOpen) CloseChat(false);
            if (session && session.Feedback != null)
            {
                var feedback = session.Feedback;
                feedback.MessagePosted -= OnFeedbackMessage;
                feedback.Captured -= OnFeedbackCapture;
                feedback.IncomeReceived -= OnFeedbackIncome;
                feedback.Damaged -= OnFeedbackDamage;
                feedback.WinnerDecided -= OnFeedbackWinner;
                feedback.SoldierSpawned -= OnFeedbackSpawned;
                session.PlayerEliminated -= OnFeedbackEliminated;
            }
            chat.MessageReceived -= OnChatReceived;
            if (pingTexture) Destroy(pingTexture);
            if (feedbackHost) Destroy(feedbackHost);
        }

        // ---------------------------------------------------------------- layout

        void BuildFeedbackUi()
        {
            if (!feedbackUi) return;
            feedbackCompact = UiViewport.IsCompact;
            feedbackRoot = new VisualElement { name = "Battle feedback root", pickingMode = PickingMode.Ignore };
            feedbackRoot.style.position = Position.Absolute;
            feedbackRoot.style.left = 0; feedbackRoot.style.top = 0; feedbackRoot.style.right = 0; feedbackRoot.style.bottom = 0;

            logBox = new VisualElement { name = "HUD message log", pickingMode = PickingMode.Ignore };
            logBox.style.position = Position.Absolute; logBox.style.left = 8;
            logBox.style.flexDirection = FlexDirection.ColumnReverse;
            for (int i = 0; i < logRows.Length; i++)
            {
                var row = new Label { name = "HUD message " + i, pickingMode = PickingMode.Ignore, enableRichText = false };
                row.style.fontSize = feedbackCompact ? 11 : 12;
                row.style.color = RtsUiStyle.Text;
                row.style.backgroundColor = new Color(.02f, .025f, .02f, .58f);
                row.style.paddingLeft = 6; row.style.paddingRight = 6; row.style.paddingTop = 1; row.style.paddingBottom = 1;
                row.style.marginTop = 2; row.style.borderLeftWidth = 3;
                row.style.whiteSpace = WhiteSpace.NoWrap; row.style.overflow = Overflow.Hidden; row.style.textOverflow = TextOverflow.Ellipsis;
                row.style.unityTextAlign = TextAnchor.MiddleLeft; row.style.alignSelf = Align.FlexStart; row.style.maxWidth = Length.Percent(100);
                row.style.display = DisplayStyle.None;
                int index = i;
                row.RegisterCallback<PointerDownEvent>(evt => { OnLogRowPressed(index); evt.StopPropagation(); });
                logRows[i] = row; logRowKeys[i] = int.MinValue;
                logBox.Add(row);
            }
            feedbackRoot.Add(logBox);

            chatBar = new VisualElement { name = "HUD chat bar", pickingMode = PickingMode.Position };
            RtsUiStyle.Row(chatBar);
            chatBar.style.position = Position.Absolute; chatBar.style.left = 8;
            chatBar.style.backgroundColor = new Color(.035f, .04f, .03f, .92f);
            chatBar.style.borderTopWidth = chatBar.style.borderBottomWidth = chatBar.style.borderLeftWidth = chatBar.style.borderRightWidth = 1;
            chatBar.style.borderTopColor = chatBar.style.borderBottomColor = chatBar.style.borderLeftColor = chatBar.style.borderRightColor = RtsUiStyle.Bronze;
            chatBar.style.paddingLeft = 4; chatBar.style.paddingRight = 4; chatBar.style.paddingTop = 2; chatBar.style.paddingBottom = 2;
            chatField = new TextField { name = "HUD chat field", maxLength = ChatChannel.MaxLength };
            chatField.style.flexGrow = 1; chatField.style.minWidth = 0; chatField.style.fontSize = 13;
            chatField.RegisterCallback<KeyDownEvent>(OnChatKey, TrickleDown.TrickleDown);
            chatBar.Add(chatField);
            var send = CompactButton("Enviar", () => CloseChat(true), "HUD chat send");
            chatBar.Add(send);
            chatBar.Add(CompactButton("Cancelar", () => CloseChat(false), "HUD chat cancel"));
            chatBar.style.display = DisplayStyle.None;
            feedbackRoot.Add(chatBar);

            chatButton = CompactButton("Chat", OpenChat, "HUD chat button");
            chatButton.tooltip = GameText.Localize("Escribir un mensaje (Intro)");
            chatButton.style.position = Position.Absolute; chatButton.style.left = 8;
            // Touch browsers need the DOM input armed inside the same finger gesture.
            chatButton.RegisterCallback<PointerDownEvent>(_ => { if (WebChatInput.Supported) WebChatInput.Arm(GameText.Localize("Escribe un mensaje…")); }, TrickleDown.TrickleDown);
            feedbackRoot.Add(chatButton);

            toastBox = new VisualElement { name = "HUD toast", pickingMode = PickingMode.Ignore };
            toastBox.style.position = Position.Absolute; toastBox.style.left = 0; toastBox.style.right = 0;
            toastBox.style.alignItems = Align.Center;
            toastLabel = new Label { name = "HUD toast text", pickingMode = PickingMode.Ignore, enableRichText = false };
            toastLabel.style.backgroundColor = new Color(.03f, .035f, .03f, .78f);
            toastLabel.style.paddingLeft = 16; toastLabel.style.paddingRight = 16; toastLabel.style.paddingTop = 6; toastLabel.style.paddingBottom = 6;
            toastLabel.style.borderTopWidth = toastLabel.style.borderBottomWidth = 2;
            toastLabel.style.unityTextAlign = TextAnchor.MiddleCenter; toastLabel.style.whiteSpace = WhiteSpace.Normal;
            toastLabel.style.maxWidth = Length.Percent(92);
            toastBox.Add(toastLabel); toastBox.style.display = DisplayStyle.None;
            feedbackRoot.Add(toastBox);

            goldFloat = new Label { name = "HUD gold float", pickingMode = PickingMode.Ignore, enableRichText = false };
            goldFloat.style.position = Position.Absolute; goldFloat.style.color = RtsUiStyle.Gold;
            goldFloat.style.fontSize = 14; goldFloat.style.unityFontStyleAndWeight = FontStyle.Bold;
            goldFloat.style.display = DisplayStyle.None;
            feedbackRoot.Add(goldFloat);

            feedbackUi.SetContent(feedbackRoot);
            for (int i = 0; i < logRowKeys.Length; i++) logRowKeys[i] = int.MinValue;
        }

        static Button CompactButton(string text, System.Action action, string name)
        {
            var button = RtsUiStyle.Button(text, action, name);
            float height = UiViewport.IsTouchLayout ? Mathf.Max(34, UiViewport.MinimumTouchTarget * .8f) : 26;
            button.style.minHeight = button.style.height = height;
            button.style.fontSize = 11; button.style.paddingLeft = 8; button.style.paddingRight = 8;
            button.style.marginRight = 0; button.style.marginBottom = 0; button.style.marginLeft = 4;
            return button;
        }

        float FeedbackBottom => (FooterVisible ? FooterHeight : 0) + 8;

        void LayoutFeedback()
        {
            float bottom = FeedbackBottom;
            float width = Mathf.Min(UiViewport.IsCompact ? 360 : 440, UiViewport.LogicalWidth - 16);
            bool touch = UiViewport.IsTouchLayout;
            chatButton.style.display = touch && !chatOpen && session.Winner < 0 ? DisplayStyle.Flex : DisplayStyle.None;
            chatButton.style.bottom = bottom;
            float chatHeight = touch ? Mathf.Max(34, UiViewport.MinimumTouchTarget * .8f) + 6 : 32;
            chatBar.style.bottom = bottom; chatBar.style.width = width;
            float logBottom = bottom + (chatOpen || touch ? chatHeight + 2 : 0);
            logBox.style.bottom = logBottom; logBox.style.width = width;
            toastBox.style.top = UiViewport.LogicalHeight * (UiViewport.IsPortrait ? .2f : .17f) + HeaderHeight * .5f;
        }

        // ---------------------------------------------------------------- per frame

        void RefreshFeedback()
        {
            if (feedbackRoot == null) return;
            if (feedbackCompact != UiViewport.IsCompact) { BuildFeedbackUi(); if (chatOpen) ShowChatBar(); }
            float now = Time.unscaledTime;
            LayoutFeedback();
            RefreshLog(now);
            RefreshToast(now);
            RefreshGold(now);
            PollChat();
            var keyboard = Keyboard.current;
            if (!chatOpen && keyboard != null && !ChatInput.IsTyping && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                && !controller.HelpVisible && session.Winner < 0)
                OpenChat();
        }

        void RefreshLog(float now)
        {
            var log = session.Feedback.Log;
            int max = UiViewport.IsCompact ? CompactLogRows : DesktopLogRows;
            int visible = log.VisibleCount(now, max);
            for (int i = 0; i < logRows.Length; i++)
            {
                var row = logRows[i];
                if (i >= visible) { if (logRowKeys[i] != int.MinValue) { row.style.display = DisplayStyle.None; row.pickingMode = PickingMode.Ignore; logRowKeys[i] = int.MinValue; } continue; }
                var entry = log[i];
                int key = unchecked(entry.Text.GetHashCode() * 31 + entry.Time.GetHashCode() * 7 + entry.Repeat + (GameText.IsSpanish ? 1 : 0));
                if (key != logRowKeys[i])
                {
                    logRowKeys[i] = key; logRowEntries[i] = entry;
                    string text = entry.Kind == MessageKind.Chat ? ChatLine(entry) : GameText.Localize(entry.Text);
                    row.text = entry.Repeat > 1 ? text + "  ×" + entry.Repeat : text;
                    var color = KindColor(entry);
                    row.style.borderLeftColor = color;
                    row.style.color = entry.Kind == MessageKind.Info ? RtsUiStyle.Text : Readable(color);
                    row.style.display = DisplayStyle.Flex;
                    row.pickingMode = entry.HasFocus ? PickingMode.Position : PickingMode.Ignore;
                }
                row.style.opacity = MessageLog.Alpha(now - entry.Time) * (i == 0 ? 1 : .92f);
            }
        }

        static string ChatLine(MessageEntry entry)
        {
            int split = entry.Text.IndexOf(": ", System.StringComparison.Ordinal);
            return split < 0 ? entry.Text : GameText.Localize(entry.Text.Substring(0, split)) + entry.Text.Substring(split);
        }

        // Dark WC3 team colours (violet, navy, maroon, dark green, brown) keep their hue but are
        // lifted toward white until they reach a readable luminance on the dark HUD panels.
        static Color Readable(Color color)
        {
            float luminance = .2126f * color.r + .7152f * color.g + .0722f * color.b;
            float lift = Mathf.Clamp01((.62f - luminance) / Mathf.Max(.01f, 1 - luminance));
            return Color.Lerp(color, Color.white, Mathf.Max(.2f, lift));
        }

        static Color KindColor(MessageEntry entry)
        {
            switch (entry.Kind)
            {
                case MessageKind.Income: case MessageKind.Purchase: return RtsUiStyle.Gold;
                case MessageKind.Capture: case MessageKind.Country: return PlayerRules.IsPlayer(entry.Team) ? VisualFactory.TeamColor(entry.Team) : RtsUiStyle.Muted;
                case MessageKind.Loss: case MessageKind.Attack: case MessageKind.NoGold: case MessageKind.Defeat: return AlertRed;
                case MessageKind.Victory: return RtsUiStyle.Gold;
                case MessageKind.Chat: return new Color(.62f, .86f, 1f);
                default: return RtsUiStyle.Bronze;
            }
        }

        void OnLogRowPressed(int index)
        {
            if (index < 0 || index >= logRows.Length || logRowKeys[index] == int.MinValue) return;
            var entry = logRowEntries[index];
            if (!entry.HasFocus || !controller) return;
            Sfx.Ui(SfxId.UiClick);
            controller.Focus(entry.Focus);
        }

        void Toast(string text, Color color, bool big)
        {
            if (toasts.Count >= 3) toasts.Dequeue();
            toasts.Enqueue((text, color, big));
        }

        void RefreshToast(float now)
        {
            float age = now - toastStart;
            if (age >= ToastSeconds || toastBox.style.display == DisplayStyle.None)
            {
                if (toasts.Count == 0) { if (toastBox.style.display != DisplayStyle.None) toastBox.style.display = DisplayStyle.None; return; }
                var next = toasts.Dequeue();
                toastStart = now; age = 0;
                toastLabel.text = GameText.Localize(next.text);
                toastLabel.style.color = Readable(next.color);
                toastLabel.style.borderTopColor = toastLabel.style.borderBottomColor = next.color;
                toastLabel.style.fontSize = next.big ? (UiViewport.IsCompact ? 18 : 22) : (UiViewport.IsCompact ? 14 : 16);
                toastBox.style.display = DisplayStyle.Flex;
            }
            float pop = age < .18f ? 1 + .12f * Mathf.Sin(age / .18f * Mathf.PI) : 1;
            toastLabel.style.scale = new Scale(new Vector3(pop, pop, 1));
            toastBox.style.opacity = age < .12f ? age / .12f : age > ToastSeconds - .5f ? Mathf.Clamp01((ToastSeconds - age) / .5f) : 1;
        }

        void RefreshGold(float now)
        {
            if (goldLabel == null) return;
            // Per-frame text only while tweening (plus the final frame); otherwise the 10 Hz label refresh owns it.
            if (goldTween.Active || goldTweenShown) goldLabel.text = GameText.Localize(GoldText);
            goldTweenShown = goldTween.Active;
            float pulse = now - goldPulseStart, flash = now - goldFlashStart;
            float scale = pulse < .35f ? 1 + .18f * Mathf.Sin(pulse / .35f * Mathf.PI) : flash < .3f ? 1 + .06f * Mathf.Sin(flash / .3f * Mathf.PI * 3) : 1;
            goldLabel.style.scale = new Scale(new Vector3(scale, scale, 1));
            goldLabel.style.color = flash < .5f ? Color.Lerp(AlertRed, RtsUiStyle.Gold, flash / .5f)
                : pulse < .5f ? Color.Lerp(Color.white, RtsUiStyle.Gold, pulse / .5f) : RtsUiStyle.Gold;
            float floatAge = now - goldFloatStart;
            if (floatAge < 1.1f)
            {
                var anchor = goldLabel.worldBound;
                var local = feedbackRoot.WorldToLocal(new Vector2(anchor.xMin + 4, anchor.yMax));
                goldFloat.style.left = local.x; goldFloat.style.top = local.y - floatAge * 16;
                goldFloat.style.opacity = floatAge < .8f ? 1 : 1 - (floatAge - .8f) / .3f;
                goldFloat.style.display = DisplayStyle.Flex;
            }
            else if (goldFloat.style.display != DisplayStyle.None) goldFloat.style.display = DisplayStyle.None;
        }

        // ---------------------------------------------------------------- events

        void OnFeedbackMessage(MessageEntry entry)
        {
            switch (entry.Kind)
            {
                case MessageKind.NoGold: goldFlashStart = Time.unscaledTime; Sfx.Ui(SfxId.NoGold); break;
                case MessageKind.Purchase: Sfx.Ui(SfxId.Purchase); break;
                case MessageKind.Chat: Sfx.Ui(SfxId.Chat); break;
            }
        }

        void OnFeedbackIncome(int team, int amount)
        {
            if (team != 0 || amount <= 0) return;
            float now = Time.unscaledTime;
            int gold = session.Economy.Gold[0];
            goldTween.Begin(Mathf.Max(0, gold - amount), gold, now);
            goldPulseStart = now; goldFloatStart = now;
            goldFloat.text = "+" + amount;
            Sfx.Ui(SfxId.Income);
        }

        void OnFeedbackCapture(CaptureEvent capture)
        {
            string country = capture.Country >= 0 && capture.Country < MapLayout.Countries.Length ? MapLayout.Countries[capture.Country].Name : null;
            if (capture.Owner == 0)
            {
                if (capture.CountryCompleted && country != null) { Toast("¡País completado: " + country + "!", VisualFactory.TeamColor(0), true); Sfx.Ui(SfxId.CountryCompleted); }
                else { Toast("Has conquistado " + capture.Name, VisualFactory.TeamColor(0), false); Sfx.Ui(SfxId.CityCaptured); }
            }
            else if (capture.Previous == 0)
            {
                if (capture.CountryLost && country != null) Toast("Has perdido el país " + country, AlertRed, true);
                else Toast("Has perdido " + capture.Name, AlertRed, false);
                Sfx.Ui(SfxId.CityLost);
            }
        }

        void OnFeedbackDamage(CombatTarget victim, int attacker, CombatTarget source)
        {
            if (!victim || victim.Team != 0 || attacker == 0 || attacker == int.MinValue) return;
            var position = victim.transform.position;
            float now = Time.unscaledTime;
            if (!session.Feedback.Alerts.TryRaise(position, now)) return;
            // Fights already in view need no alarm; Space and the minimap ping still work.
            var viewport = cam ? cam.WorldToViewportPoint(position) : Vector3.zero;
            if (viewport.z > 0 && viewport.x > .12f && viewport.x < .88f && viewport.y > .15f && viewport.y < .85f && !StrategicMapView.Active) return;
            string place = NearestPlaceName(position);
            session.Feedback.Post(place != null ? "¡Te atacan en " + place + "!" : "¡Te atacan!", MessageKind.Attack, 0, position);
            Sfx.Ui(SfxId.UnderAttack);
        }

        string NearestPlaceName(Vector3 position)
        {
            string best = null; float bestDistance = 40 * 40;
            foreach (var town in session.Towns)
            {
                if (!town) continue;
                var delta = town.transform.position - position; delta.y = 0;
                float distance = delta.sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = town.DisplayName; }
            }
            return best;
        }

        void OnFeedbackWinner(int team)
        {
            if (team == 0) Sfx.Ui(SfxId.Victory); else Sfx.Ui(SfxId.Defeat);
        }

        void OnFeedbackEliminated(int player)
        {
            if (player == 0 && session.Winner < 0) Sfx.Ui(SfxId.Defeat);
        }

        void OnFeedbackSpawned(Soldier unit)
        {
            if (!unit || unit.Team != 0 || session.Clock.TickCount == 0) return;
            float now = Time.unscaledTime;
            if (now - lastTrained < 1.2f) return;
            lastTrained = now;
            Sfx.Ui(SfxId.UnitTrained);
        }

        // ---------------------------------------------------------------- chat

        void OpenChat()
        {
            if (chatOpen || !session || session.Winner >= 0) return;
            chatOpen = true; ChatInput.SetOpen(true);
            controller.CancelCursor();
            webChat = WebChatInput.Supported;
            if (webChat) { WebChatInput.Open(GameText.Localize("Escribe un mensaje…")); return; }
            ShowChatBar();
        }

        void ShowChatBar()
        {
            chatBar.style.display = DisplayStyle.Flex;
            chatField.value = string.Empty;
            // Focusing a UI Toolkit TextField raises TouchScreenKeyboard on native mobile builds.
            chatField.schedule.Execute(() => { if (chatOpen) chatField.Focus(); });
            chatField.Focus();
        }

        void CloseChat(bool send)
        {
            if (!chatOpen) return;
            string text = webChat ? WebChatInput.Take() : chatField != null ? chatField.value : string.Empty;
            chatOpen = false; ChatInput.SetOpen(false);
            if (webChat) WebChatInput.Close();
            webChat = false;
            if (chatBar != null) { chatBar.style.display = DisplayStyle.None; chatField.value = string.Empty; chatField.Blur(); }
            if (send) chat.Submit(0, text);
        }

        void PollChat()
        {
            if (!chatOpen) return;
            if (webChat)
            {
                var state = WebChatInput.Poll();
                if (state == WebChatInput.State.Submitted) CloseChat(true);
                else if (state == WebChatInput.State.Cancelled || state == WebChatInput.State.Closed) CloseChat(false);
                return;
            }
            var keyboard = Keyboard.current;
            // Fallback when the field lost focus: Escape still closes.
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CloseChat(false);
            if (controller.HelpVisible || session.Winner >= 0) CloseChat(false);
        }

        void OnChatKey(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { CloseChat(true); evt.StopPropagation(); }
            else if (evt.keyCode == KeyCode.Escape) { CloseChat(false); evt.StopPropagation(); }
        }

        void OnChatReceived(int sender, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            session.Feedback.Post(ChatChannel.Format(sender, text), MessageKind.Chat, sender);
        }

        // ---------------------------------------------------------------- minimap pings

        void DrawAlertPings(Rect rect)
        {
            var alerts = session.Feedback.Alerts;
            float now = Time.unscaledTime;
            bool any = false;
            for (int i = 0; i < AttackAlerts.PingCapacity; i++) if (alerts.PingProgress(i, now, out _) >= 0) { any = true; break; }
            if (!any) return;
            if (!pingTexture) pingTexture = BuildPingTexture();
            Color previous = GUI.color;
            for (int i = 0; i < AttackAlerts.PingCapacity; i++)
            {
                float progress = alerts.PingProgress(i, now, out var position);
                if (progress < 0) continue;
                var point = MapPoint(position, rect);
                // Two staggered rings expand from the attacked spot.
                for (int wave = 0; wave < 2; wave++)
                {
                    float p = Mathf.Repeat(progress * 2 + wave * .5f, 1);
                    float size = Mathf.Lerp(5, 30, p);
                    GUI.color = new Color(1, .2f, .15f, (1 - p) * (1 - progress * .6f));
                    GUI.DrawTexture(new Rect(point.x - size * .5f, point.y - size * .5f, size, size), pingTexture, ScaleMode.StretchToFill, true);
                }
                GUI.color = new Color(1, .25f, .2f, 1 - progress);
                RtsSkin.Fill(new Rect(point.x - 1.5f, point.y - 1.5f, 3, 3), GUI.color);
            }
            GUI.color = previous;
        }

        static Texture2D BuildPingTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Minimap alert ping", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + .5f - size * .5f, dy = y + .5f - size * .5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (size * .5f);
                    float a = Mathf.Clamp01(1 - Mathf.Abs(r - .82f) / .12f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }

        // ---------------------------------------------------------------- settings

        void BuildFeedbackSettings(VisualElement panel)
        {
            int master = Mathf.RoundToInt(Sfx.MasterVolume * 100), effects = Mathf.RoundToInt(Sfx.EffectsVolume * 100);
            var row = new VisualElement(); RtsUiStyle.Row(row, true);
            row.Add(RtsUiStyle.Button(Sfx.Muted ? "SONIDO: SILENCIADO" : "SONIDO: ACTIVO", () => { Sfx.SetMuted(!Sfx.Muted); BuildRetainedUi(false); }, "Audio mute"));
            row.Add(RtsUiStyle.Button("VOLUMEN −", () => { Sfx.SetMasterVolume(Sfx.MasterVolume - .1f); BuildRetainedUi(false); }, "Audio master down"));
            row.Add(RtsUiStyle.Button("VOLUMEN +", () => { Sfx.SetMasterVolume(Sfx.MasterVolume + .1f); BuildRetainedUi(false); }, "Audio master up"));
            row.Add(RtsUiStyle.Button("EFECTOS −", () => { Sfx.SetEffectsVolume(Sfx.EffectsVolume - .1f); BuildRetainedUi(false); }, "Audio effects down"));
            row.Add(RtsUiStyle.Button("EFECTOS +", () => { Sfx.SetEffectsVolume(Sfx.EffectsVolume + .1f); BuildRetainedUi(false); }, "Audio effects up"));
            row.Add(RtsUiStyle.Button(Music.MusicEnabled ? "MÚSICA: ACTIVA" : "MÚSICA: DESACTIVADA", () => { Music.ToggleMusic(); BuildRetainedUi(false); }, "Music toggle"));
            row.Add(RtsUiStyle.Button("MÚSICA −", () => { Music.SetMusicVolume(Music.MusicVolume - .05f); BuildRetainedUi(false); }, "Music down"));
            row.Add(RtsUiStyle.Button("MÚSICA +", () => { Music.SetMusicVolume(Music.MusicVolume + .05f); BuildRetainedUi(false); }, "Music up"));
            row.Add(RtsUiStyle.Button(GameFeel.ShakeEnabled ? "TEMBLOR DE CÁMARA: ACTIVO" : "TEMBLOR DE CÁMARA: INACTIVO", () => { GameFeel.SetShakeEnabled(!GameFeel.ShakeEnabled); BuildRetainedUi(false); }, "Camera shake"));
            panel.Add(row);
            AddInfo(panel, "Volumen general " + master + " % · efectos " + effects + " % · música " + Mathf.RoundToInt(Music.MusicVolume * 100) + " % · F8 música");
        }
    }
}
