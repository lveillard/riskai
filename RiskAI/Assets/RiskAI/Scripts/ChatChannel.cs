using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace RiskAI
{
    /// <summary>One chat line: who wrote it, who it is for (<see cref="Everyone"/> or one player) and its text.</summary>
    public readonly struct ChatMessage
    {
        public const int Everyone = -1;
        public readonly int Sender, Recipient;
        public readonly string Text;
        public ChatMessage(int sender, int recipient, string text) { Sender = sender; Recipient = recipient < 0 ? Everyone : recipient; Text = text ?? string.Empty; }
        public bool ToEveryone => Recipient == Everyone;
        /// <summary>Public lines, and private lines this player sent or received.</summary>
        public bool VisibleTo(int player) => ToEveryone || Sender == player || Recipient == player;
    }

    /// <summary>Delivery of chat lines. The local transport echoes; a future network sender implements this.</summary>
    public interface IChatTransport
    {
        void Send(ChatMessage message);
        event Action<ChatMessage> Received;
    }

    /// <summary>Single-player transport: every sent line is received immediately by the local player.</summary>
    public sealed class LocalChatTransport : IChatTransport
    {
        public event Action<ChatMessage> Received;
        public void Send(ChatMessage message) => Received?.Invoke(message);
    }

    /// <summary>Chat model: validates outgoing text, resolves recipients and formats received lines for the message log.</summary>
    public sealed class ChatChannel
    {
        public const int MaxLength = 120;
        IChatTransport transport;
        public event Action<ChatMessage> MessageReceived;

        public ChatChannel(IChatTransport chatTransport = null) { Transport = chatTransport ?? new LocalChatTransport(); }

        public IChatTransport Transport
        {
            get => transport;
            set
            {
                if (transport != null) transport.Received -= OnReceived;
                transport = value ?? new LocalChatTransport();
                transport.Received += OnReceived;
            }
        }

        void OnReceived(ChatMessage message) =>
            MessageReceived?.Invoke(new ChatMessage(message.Sender, message.Recipient, Sanitize(message.Text)));

        /// <summary>Returns false when nothing sendable remains after sanitising.</summary>
        public bool Submit(int sender, string text, int recipient = ChatMessage.Everyone)
        {
            string clean = Sanitize(text);
            if (clean.Length == 0) return false;
            transport.Send(new ChatMessage(sender, recipient, clean));
            return true;
        }

        /// <summary>Trims, removes control characters and markup brackets, collapses spaces and caps the length.</summary>
        public static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var builder = new StringBuilder(Math.Min(text.Length, MaxLength));
            bool space = false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c)) { space = builder.Length > 0; continue; }
                if (c == '<' || c == '>') continue;
                if (space) { if (builder.Length >= MaxLength) break; builder.Append(' '); space = false; }
                if (builder.Length >= MaxLength) break;
                builder.Append(c);
            }
            return builder.ToString();
        }

        /// <summary>"Tú", "Todos" or the player's name ("IA 3 · Violeta").</summary>
        public static string RecipientName(int recipient) =>
            recipient == ChatMessage.Everyone ? "Todos" : recipient == 0 ? "Tú" : VisualFactory.TeamName(recipient);

        /// <summary>"Tú → Todos: hola", "Tú → IA 2 · Azul: hola", "IA 3 · Violeta → Tú: hola".</summary>
        public static string Format(ChatMessage message) =>
            RecipientName(message.Sender == ChatMessage.Everyone ? 0 : message.Sender) + " → " + RecipientName(message.Recipient) + ": " + message.Text;

        /// <summary>
        /// Chat shortcuts: "/w azul hola", "/w 3 hola", "/azul hola", "/blue hola", "/todos hola" or
        /// "/all hola". Returns true when the line names a recipient; <paramref name="body"/> is then
        /// the message without the command. <paramref name="unknown"/> reports a whisper to nobody.
        /// </summary>
        public static bool TryParseRecipient(string text, int playerCount, Func<int, bool> canReceive,
            out int recipient, out string body, out bool unknown)
        {
            recipient = ChatMessage.Everyone; body = text ?? string.Empty; unknown = false;
            string line = (text ?? string.Empty).TrimStart();
            if (line.Length < 2 || line[0] != '/') return false;
            string rest = line.Substring(1);
            string command = FirstWord(rest);
            string lower = Normalize(command);
            if (lower == "todos" || lower == "all" || lower == "a" || lower == "t")
            { body = rest.Substring(command.Length).TrimStart(); return true; }
            bool whisper = lower == "w" || lower == "whisper" || lower == "p" || lower == "privado" || lower == "m" || lower == "msg";
            string target = whisper ? rest.Substring(command.Length).TrimStart() : rest;
            int best = -1, bestLength = 0;
            for (int player = 0; player < playerCount; player++)
            {
                if (canReceive != null && !canReceive(player)) continue;
                foreach (var alias in Aliases(player))
                {
                    int length = MatchPrefix(target, alias);
                    if (length > bestLength) { best = player; bestLength = length; }
                }
            }
            if (best < 0) { unknown = whisper; return false; }
            recipient = best; body = target.Substring(bestLength).TrimStart();
            return true;
        }

        static string FirstWord(string text)
        {
            int end = 0; while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
            return text.Substring(0, end);
        }

        static System.Collections.Generic.IEnumerable<string> Aliases(int player)
        {
            string name = VisualFactory.TeamName(player);
            int split = name.LastIndexOf(" · ", StringComparison.Ordinal);
            string colour = split >= 0 ? name.Substring(split + 3) : name;
            yield return colour;
            string english = GameText.EnglishOf(colour);
            if (english != colour) yield return english;
            yield return player.ToString();
            yield return "ia" + player;
            yield return "ai" + player;
            yield return "ia " + player;
            yield return "ai " + player;
        }

        /// <summary>Length of <paramref name="text"/> consumed by <paramref name="alias"/> as a whole word, or 0.</summary>
        static int MatchPrefix(string text, string alias)
        {
            if (string.IsNullOrEmpty(alias) || text.Length < alias.Length) return 0;
            if (Normalize(text.Substring(0, alias.Length)) != Normalize(alias)) return 0;
            if (text.Length > alias.Length && !char.IsWhiteSpace(text[alias.Length])) return 0;
            return alias.Length;
        }

        static string Normalize(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (char c in text.Normalize(NormalizationForm.FormD))
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    builder.Append(char.ToLowerInvariant(c));
            return builder.ToString();
        }
    }

    /// <summary>
    /// Keyboard ownership while the chat field is open. RtsController ignores game hotkeys
    /// while this is true; it stays true for the closing frame so Enter/Escape never leak.
    /// </summary>
    public static class ChatInput
    {
        static bool open;
        static int closedFrame = -10;
        public static bool IsOpen => open;
        public static bool IsTyping => open || Time.frameCount <= closedFrame + 1;
        internal static void SetOpen(bool value)
        {
            if (open && !value) closedFrame = Time.frameCount;
            open = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { open = false; closedFrame = -10; }
    }

    /// <summary>
    /// Browser text entry for touch WebGL. A real DOM &lt;input&gt; is focused inside the
    /// finger-lift gesture (armed on pointer down), which is what makes iOS Safari and
    /// Android Chrome raise the on-screen keyboard reliably.
    /// </summary>
    public static class WebChatInput
    {
        public enum State { Closed = -1, Typing = 0, Submitted = 1, Cancelled = 2 }
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void RiskAI_ChatArm(string placeholder, int maxLength);
        [DllImport("__Internal")] static extern void RiskAI_ChatOpen(string placeholder, int maxLength);
        [DllImport("__Internal")] static extern void RiskAI_ChatClose();
        [DllImport("__Internal")] static extern int RiskAI_ChatPoll();
        [DllImport("__Internal")] static extern string RiskAI_ChatTake();
        [DllImport("__Internal")] static extern void RiskAI_ChatHold();
        [DllImport("__Internal")] static extern void RiskAI_ChatPlaceholder(string placeholder);
        public static bool Supported => PlatformPresentation.TouchCapable;
        public static void Arm(string placeholder) { RiskAI_ChatArm(placeholder, ChatChannel.MaxLength); }
        public static void Open(string placeholder) { WebGLInput.captureAllKeyboardInput = false; RiskAI_ChatOpen(placeholder, ChatChannel.MaxLength); }
        public static void Close() { RiskAI_ChatClose(); WebGLInput.captureAllKeyboardInput = true; }
        public static State Poll() => (State)RiskAI_ChatPoll();
        public static string Take() => RiskAI_ChatTake() ?? string.Empty;
        /// <summary>Keeps the DOM field open while a Unity control (recipient chip) takes the tap, then refocuses it.</summary>
        public static void Hold() { RiskAI_ChatHold(); }
        public static void SetPlaceholder(string placeholder) { RiskAI_ChatPlaceholder(placeholder); }
#else
        public static bool Supported => false;
        public static void Arm(string placeholder) { }
        public static void Open(string placeholder) { }
        public static void Close() { }
        public static State Poll() => State.Closed;
        public static string Take() => string.Empty;
        public static void Hold() { }
        public static void SetPlaceholder(string placeholder) { }
#endif
    }
}
