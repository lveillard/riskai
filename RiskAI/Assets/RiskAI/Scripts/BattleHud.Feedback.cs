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

    /// <summary>What the local player sees and hears for a capture. Pure, so the routing is testable.</summary>
    public readonly struct CaptureCue
    {
        public readonly SfxId Sound;
        public readonly string Headline, Detail;
        public readonly bool Loss, Big;
        public CaptureCue(SfxId sound, string headline, string detail, bool loss, bool big) { Sound = sound; Headline = headline; Detail = detail; Loss = loss; Big = big; }
        /// <summary>Headline and detail on two lines (Spanish source; GameText localizes each).</summary>
        public string Text => string.IsNullOrEmpty(Detail) ? Headline : Headline + "\n" + Detail;

        /// <summary>
        /// Losing a city of a country the player fully owned is its own event: the country breaks
        /// and stops paying gold and reinforcements, so it gets CountryLost instead of CityLost.
        /// Returns false for captures that do not involve player 0.
        /// </summary>
        public static bool For(in CaptureEvent capture, string country, out CaptureCue cue)
        {
            if (capture.Owner == 0)
            {
                cue = capture.CountryCompleted && country != null
                    ? new CaptureCue(SfxId.CountryCompleted, "¡País completado: " + country + "!", "Oro y refuerzos de " + country + " cada ronda", false, true)
                    : new CaptureCue(SfxId.CityCaptured, "Has conquistado " + capture.Name, null, false, false);
                return true;
            }
            if (capture.Previous == 0)
            {
                cue = capture.CountryLost && country != null
                    ? new CaptureCue(SfxId.CountryLost, "¡Has perdido " + country + "!", "País roto: sin oro ni refuerzos de " + country, true, true)
                    : new CaptureCue(SfxId.CityLost, "Has perdido " + capture.Name, null, true, false);
                return true;
            }
            cue = default; return false;
        }
    }

    /// <summary>
    /// Pending centre toasts. Big announcements (country completed/lost) go ahead of every
    /// normal toast and are never dropped to make room for one; a burst of city captures
    /// can therefore not bury or evict a broken-country warning. Pure and testable.
    /// </summary>
    public sealed class ToastQueue
    {
        public const int Capacity = 4;
        readonly System.Collections.Generic.List<(string text, Color color, bool big)> items = new System.Collections.Generic.List<(string, Color, bool)>();
        public int Count => items.Count;
        public (string text, Color color, bool big) Peek(int index) => items[index];

        public void Add(string text, Color color, bool big)
        {
            int index = items.Count;
            if (big) { index = items.FindIndex(item => !item.big); if (index < 0) index = items.Count; }
            items.Insert(index, (text, color, big));
            while (items.Count > Capacity)
            {
                int drop = items.FindIndex(item => !item.big);
                items.RemoveAt(drop >= 0 ? drop : items.Count - 1);
            }
        }

        public bool TryTake(out (string text, Color color, bool big) toast)
        {
            if (items.Count == 0) { toast = default; return false; }
            toast = items[0]; items.RemoveAt(0); return true;
        }

        public void Clear() => items.Clear();
    }

    /// <summary>Feedback overlay: recent message log, toasts, chat, gold juice and attack alerts.</summary>
    public sealed partial class BattleHud
    {
        const int DesktopLogRows = 4, CompactLogRows = 3;
        /// <summary>Feedback overlay panel order; the HUD rises above it while a modal is open.</summary>
        const int FeedbackSortingOrder = 21;
        const float ToastSeconds = 2.6f, BigToastSeconds = 4f;
        static readonly Color AlertRed = new Color(1f, .36f, .3f);
        GameObject feedbackHost;
        RtsUiRuntime feedbackUi;
        VisualElement feedbackRoot, logBox, toastBox, chatBar;
        readonly Label[] logRows = new Label[DesktopLogRows];
        readonly int[] logRowKeys = new int[DesktopLogRows];
        readonly MessageEntry[] logRowEntries = new MessageEntry[DesktopLogRows];
        Label toastLabel, goldFloat;
        Button chatButton, recipientChip, chatSend, chatCancel;
        VisualElement recipientSwatch, recipientList, compactQuickBar;
        Label recipientLabel;
        int chatRecipient = ChatMessage.Everyone, chatTabFrame = -1;
        TextField chatField;
        readonly ChatChannel chat = new ChatChannel();
        bool chatOpen, webChat;
        float goldFloatStart = -10, goldPulseStart = -10, goldFlashStart = -10, toastStart = -10, lastTrained = -10;
        GoldTween goldTween;
        readonly ToastQueue toasts = new ToastQueue();
        bool toastShowingBig;
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
            feedbackUi = RtsUiRuntime.Attach(feedbackHost, "Battle feedback", FeedbackSortingOrder);
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
            if (feedbackHost) Destroy(feedbackHost); // UI host GameObject, not a generated asset.
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
            // Recipient chip ("Para: Todos"): opens the list of living players; Tab cycles while typing.
            recipientChip = CompactButton("", ToggleRecipientList, "HUD chat recipient");
            recipientChip.style.marginLeft = 0; recipientChip.style.marginRight = 4; recipientChip.style.paddingLeft = 6; recipientChip.style.paddingRight = 6;
            recipientChip.style.flexDirection = FlexDirection.Row; recipientChip.style.alignItems = Align.Center; recipientChip.style.flexShrink = 0;
            recipientChip.tooltip = GameText.Localize("Destinatario · Tab cambia · Mayús+Intro envía a todos");
            recipientSwatch = new VisualElement { pickingMode = PickingMode.Ignore }; recipientSwatch.style.width = recipientSwatch.style.height = 10; recipientSwatch.style.marginRight = 5;
            recipientLabel = new Label { pickingMode = PickingMode.Ignore }; recipientLabel.style.fontSize = 12; recipientLabel.style.color = RtsUiStyle.Text;
            var chevron = new RtsIcon(RtsGlyph.ChevronUp, 12); chevron.style.marginLeft = 5;
            recipientChip.Add(recipientSwatch); recipientChip.Add(recipientLabel); recipientChip.Add(chevron);
            // The WebGL DOM field would blur on this tap; keep it open and refocus it afterwards.
            recipientChip.RegisterCallback<PointerDownEvent>(_ => { if (webChat) WebChatInput.Hold(); }, TrickleDown.TrickleDown);
            chatBar.Add(recipientChip);
            chatField = new TextField { name = "HUD chat field", maxLength = ChatChannel.MaxLength };
            chatField.style.flexGrow = 1; chatField.style.minWidth = 0; chatField.style.fontSize = 13;
            chatField.RegisterCallback<KeyDownEvent>(OnChatKey, TrickleDown.TrickleDown);
            chatField.RegisterCallback<NavigationMoveEvent>(OnChatNavigate, TrickleDown.TrickleDown);
            chatBar.Add(chatField);
            chatSend = CompactButton("Enviar", () => CloseChat(true), "HUD chat send");
            chatBar.Add(chatSend);
            chatCancel = CompactButton("Cancelar", () => CloseChat(false), "HUD chat cancel");
            chatBar.Add(chatCancel);
            chatBar.style.display = DisplayStyle.None;
            feedbackRoot.Add(chatBar);

            recipientList = new VisualElement { name = "HUD chat recipients", pickingMode = PickingMode.Position };
            recipientList.style.position = Position.Absolute; recipientList.style.left = 8; recipientList.style.minWidth = 190;
            recipientList.style.backgroundColor = new Color(.035f, .04f, .03f, .96f);
            recipientList.style.borderTopWidth = recipientList.style.borderBottomWidth = recipientList.style.borderLeftWidth = recipientList.style.borderRightWidth = 1;
            recipientList.style.borderTopColor = recipientList.style.borderBottomColor = recipientList.style.borderLeftColor = recipientList.style.borderRightColor = RtsUiStyle.Bronze;
            recipientList.style.paddingLeft = recipientList.style.paddingRight = recipientList.style.paddingTop = recipientList.style.paddingBottom = 4;
            recipientList.style.display = DisplayStyle.None;
            feedbackRoot.Add(recipientList);
            UpdateRecipientChip();

            // The old stand-alone chat button is superseded by the quick bar's chat icon.
            chatButton = CompactButton("Chat", OpenChat, "HUD chat button");
            chatButton.style.position = Position.Absolute; chatButton.style.left = 8; chatButton.style.display = DisplayStyle.None;
            feedbackRoot.Add(chatButton);
            compactQuickBar = null;
            if (UiViewport.IsCompact)
            {
                compactQuickBar = BuildQuickBar(true);
                compactQuickBar.style.position = Position.Absolute; compactQuickBar.style.left = 8;
                feedbackRoot.Add(compactQuickBar);
            }

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
            bool quick = compactQuickBar != null && !chatOpen && session.Winner < 0 && !controller.HelpVisible;
            if (compactQuickBar != null) { compactQuickBar.style.display = quick ? DisplayStyle.Flex : DisplayStyle.None; compactQuickBar.style.bottom = bottom; }
            float chatHeight = touch ? Mathf.Max(34, UiViewport.MinimumTouchTarget * .8f) + 6 : 32;
            float barHeight = compactQuickBar != null ? Mathf.Max(chatHeight, QuickSize + 2) : chatHeight;
            // The WebGL DOM field sits above the on-screen keyboard; only the recipient chip stays in Unity, near the top.
            if (webChat) { chatBar.style.top = HeaderHeight + 8; chatBar.style.bottom = StyleKeyword.Auto; chatBar.style.width = StyleKeyword.Auto; }
            else { chatBar.style.top = StyleKeyword.Auto; chatBar.style.bottom = bottom; chatBar.style.width = width; }
            if (recipientList.style.display == DisplayStyle.Flex)
            {
                if (webChat) { recipientList.style.top = HeaderHeight + 8 + chatHeight + 4; recipientList.style.bottom = StyleKeyword.Auto; }
                else { recipientList.style.top = StyleKeyword.Auto; recipientList.style.bottom = bottom + chatHeight + 4; }
            }
            float logBottom = bottom + (chatOpen && !webChat || quick ? barHeight + 2 : 0);
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
            RefreshQuickBar();
            var keyboard = Keyboard.current;
            PollQuickKeys(keyboard);
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
            toasts.Add(text, color, big);
            // A big announcement replaces a normal toast already on screen at once.
            if (big && !toastShowingBig) toastStart = -10;
        }

        void RefreshToast(float now)
        {
            float age = now - toastStart;
            // Country completed/lost announcements stay up longer than routine city toasts.
            float duration = toastShowingBig ? BigToastSeconds : ToastSeconds;
            if (age >= duration || toastBox.style.display == DisplayStyle.None)
            {
                if (!toasts.TryTake(out var next)) { toastShowingBig = false; if (toastBox.style.display != DisplayStyle.None) toastBox.style.display = DisplayStyle.None; return; }
                toastShowingBig = next.big; duration = next.big ? BigToastSeconds : ToastSeconds;
                toastStart = now; age = 0;
                // Two-line toasts (headline + consequence) localize each line on its own.
                int split = next.text.IndexOf('\n');
                toastLabel.text = split < 0 ? GameText.Localize(next.text) : GameText.Localize(next.text.Substring(0, split)) + "\n" + GameText.Localize(next.text.Substring(split + 1));
                toastLabel.style.color = Readable(next.color);
                toastLabel.style.borderTopColor = toastLabel.style.borderBottomColor = next.color;
                toastLabel.style.fontSize = next.big ? (UiViewport.IsCompact ? 18 : 22) : (UiViewport.IsCompact ? 14 : 16);
                toastBox.style.display = DisplayStyle.Flex;
            }
            float pop = age < .18f ? 1 + .12f * Mathf.Sin(age / .18f * Mathf.PI) : 1;
            toastLabel.style.scale = new Scale(new Vector3(pop, pop, 1));
            toastBox.style.opacity = age < .12f ? age / .12f : age > duration - .5f ? Mathf.Clamp01((duration - age) / .5f) : 1;
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
            if (!CaptureCue.For(capture, country, out var cue)) return;
            Toast(cue.Text, cue.Loss ? AlertRed : VisualFactory.TeamColor(0), cue.Big);
            Sfx.Ui(cue.Sound);
            // A broken country is also logged plainly, with a jump to the lost city.
            if (cue.Sound == SfxId.CountryLost) session.Feedback.Post(cue.Headline + " " + cue.Detail, MessageKind.Loss, 0, capture.Position);
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
            if (!ChatRecipients().Contains(chatRecipient)) chatRecipient = ChatMessage.Everyone;
            UpdateRecipientChip();
            if (webChat) { WebChatInput.Open(ChatPlaceholder()); ShowChatBar(); return; }
            ShowChatBar();
        }

        /// <summary>UI capture fixture: opens the chat with its recipient list.</summary>
        public void ReviewChatRecipients()
        {
            OpenChat();
            if (chatOpen && recipientList.style.display != DisplayStyle.Flex) ToggleRecipientList();
        }

        void ShowChatBar()
        {
            chatBar.style.display = DisplayStyle.Flex;
            var field = webChat ? DisplayStyle.None : DisplayStyle.Flex;
            chatField.style.display = chatSend.style.display = chatCancel.style.display = field;
            if (webChat) return;
            chatField.value = string.Empty;
            // Focusing a UI Toolkit TextField raises TouchScreenKeyboard on native mobile builds.
            chatField.schedule.Execute(() => { if (chatOpen) chatField.Focus(); });
            chatField.Focus();
        }

        void CloseChat(bool send, bool everyone = false)
        {
            if (!chatOpen) return;
            string text = webChat ? WebChatInput.Take() : chatField != null ? chatField.value : string.Empty;
            chatOpen = false; ChatInput.SetOpen(false);
            if (webChat) WebChatInput.Close();
            webChat = false;
            if (recipientList != null) recipientList.style.display = DisplayStyle.None;
            if (chatBar != null) { chatBar.style.display = DisplayStyle.None; chatField.value = string.Empty; chatField.Blur(); }
            if (send) SendChat(text, everyone);
        }

        /// <summary>Resolves "/w name", "/color" and "/todos" shortcuts, then submits to the chosen recipient.</summary>
        void SendChat(string text, bool everyone)
        {
            int recipient = everyone ? ChatMessage.Everyone : chatRecipient;
            if (ChatChannel.TryParseRecipient(text, session.PlayerCount, CanReceiveChat, out int named, out string body, out bool unknown))
            { recipient = named; text = body; }
            else if (unknown) { session.Feedback.Post("No hay ningún jugador con ese nombre o color.", MessageKind.Info, 0); return; }
            if (recipient != ChatMessage.Everyone && !CanReceiveChat(recipient)) recipient = ChatMessage.Everyone;
            chat.Submit(0, text, recipient);
        }

        bool CanReceiveChat(int player) => player > 0 && player < session.PlayerCount && !session.IsPlayerEliminated(player);

        /// <summary>"Todos" first, then every living opponent.</summary>
        System.Collections.Generic.List<int> ChatRecipients()
        {
            var list = new System.Collections.Generic.List<int> { ChatMessage.Everyone };
            for (int player = 1; player < session.PlayerCount; player++) if (CanReceiveChat(player)) list.Add(player);
            return list;
        }

        static string ShortRecipient(int recipient)
        {
            if (recipient == ChatMessage.Everyone) return "Todos";
            string name = VisualFactory.TeamName(recipient);
            int split = name.LastIndexOf(" · ", System.StringComparison.Ordinal);
            return split >= 0 ? name.Substring(split + 3) : name;
        }

        string ChatPlaceholder() => GameText.Localize("Para " + ShortRecipient(chatRecipient) + " · Escribe un mensaje…");

        void SetChatRecipient(int recipient)
        {
            chatRecipient = recipient;
            UpdateRecipientChip();
            if (webChat) WebChatInput.SetPlaceholder(ChatPlaceholder());
        }

        void UpdateRecipientChip()
        {
            if (recipientLabel == null) return;
            recipientLabel.text = GameText.Localize("Para: " + ShortRecipient(chatRecipient));
            var colour = chatRecipient == ChatMessage.Everyone ? RtsUiStyle.Muted : VisualFactory.TeamColor(chatRecipient);
            recipientSwatch.style.backgroundColor = colour;
            recipientLabel.style.color = chatRecipient == ChatMessage.Everyone ? RtsUiStyle.Text : Readable(colour);
        }

        void CycleChatRecipient(int direction)
        {
            var list = ChatRecipients();
            int index = Mathf.Max(0, list.IndexOf(chatRecipient));
            SetChatRecipient(list[(index + direction + list.Count) % list.Count]);
            if (recipientList.style.display == DisplayStyle.Flex) BuildRecipientList();
        }

        void ToggleRecipientList()
        {
            if (recipientList.style.display == DisplayStyle.Flex) { recipientList.style.display = DisplayStyle.None; RefocusChat(); return; }
            BuildRecipientList();
            recipientList.style.display = DisplayStyle.Flex;
        }

        void BuildRecipientList()
        {
            recipientList.Clear();
            foreach (int recipient in ChatRecipients())
            {
                int target = recipient;
                var option = RtsUiStyle.Button("", () => { SetChatRecipient(target); recipientList.style.display = DisplayStyle.None; RefocusChat(); },
                    "HUD chat recipient option " + (target == ChatMessage.Everyone ? "all" : target.ToString()));
                option.style.flexDirection = FlexDirection.Row; option.style.alignItems = Align.Center; option.style.justifyContent = Justify.FlexStart;
                option.style.minHeight = option.style.height = UiViewport.IsTouchLayout ? 40 : 28;
                option.style.marginRight = 0; option.style.marginBottom = 2; option.style.paddingLeft = 8;
                if (target == chatRecipient) option.style.borderLeftWidth = 3;
                option.style.borderLeftColor = RtsUiStyle.Gold;
                var swatch = new VisualElement { pickingMode = PickingMode.Ignore }; swatch.style.width = swatch.style.height = 10; swatch.style.marginRight = 8;
                swatch.style.backgroundColor = target == ChatMessage.Everyone ? RtsUiStyle.Muted : VisualFactory.TeamColor(target);
                var text = new Label(GameText.Localize(target == ChatMessage.Everyone ? "Todos" : VisualFactory.TeamName(target))) { pickingMode = PickingMode.Ignore };
                text.style.fontSize = 12; text.style.color = target == ChatMessage.Everyone ? RtsUiStyle.Text : Readable(VisualFactory.TeamColor(target));
                option.Add(swatch); option.Add(text);
                option.RegisterCallback<PointerDownEvent>(_ => { if (webChat) WebChatInput.Hold(); }, TrickleDown.TrickleDown);
                recipientList.Add(option);
            }
        }

        void RefocusChat()
        {
            if (!chatOpen || webChat || chatField == null) return;
            chatField.schedule.Execute(() => { if (chatOpen) { chatField.Focus(); chatField.value = chatField.value.Replace("\t", ""); } });
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
            // Shift+Enter always goes to everyone (WC3 "all chat").
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { CloseChat(true, evt.shiftKey); evt.StopPropagation(); }
            else if (evt.keyCode == KeyCode.Escape)
            {
                if (recipientList.style.display == DisplayStyle.Flex) { recipientList.style.display = DisplayStyle.None; RefocusChat(); }
                else CloseChat(false);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Tab)
            {
                chatTabFrame = Time.frameCount;
                CycleChatRecipient(evt.shiftKey ? -1 : 1);
                evt.StopImmediatePropagation(); RefocusChat();
            }
            else if (evt.character == '\t') { evt.StopImmediatePropagation(); }
        }

        // Tab can also arrive as focus navigation; it never leaves the field while typing.
        void OnChatNavigate(NavigationMoveEvent evt)
        {
            if (evt.direction != NavigationMoveEvent.Direction.Next && evt.direction != NavigationMoveEvent.Direction.Previous) return;
            if (chatTabFrame != Time.frameCount) CycleChatRecipient(evt.direction == NavigationMoveEvent.Direction.Previous ? -1 : 1);
            evt.StopImmediatePropagation(); RefocusChat();
        }

        void OnChatReceived(ChatMessage message)
        {
            if (string.IsNullOrEmpty(message.Text) || !message.VisibleTo(0)) return;
            session.Feedback.Post(ChatChannel.Format(message), MessageKind.Chat, message.Sender);
        }

        // ---------------------------------------------------------------- minimap pings

        void DrawAlertPings(Rect rect)
        {
            var alerts = session.Feedback.Alerts;
            float now = Time.unscaledTime;
            bool any = false;
            for (int i = 0; i < AttackAlerts.PingCapacity; i++) if (alerts.PingProgress(i, now, out _) >= 0) { any = true; break; }
            if (!any) return;
            if (!pingTexture) pingTexture = BuildPingTexture(transform);
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

        static Texture2D BuildPingTexture(Transform root)
        {
            const int size = 64;
            var texture = GeneratedResourceOwner.For(root).Track(new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Minimap alert ping", wrapMode = TextureWrapMode.Clamp });
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
    }
}
