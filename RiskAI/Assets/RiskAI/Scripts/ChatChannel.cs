using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Delivery of chat lines. The local transport echoes; a future network sender implements this.</summary>
    public interface IChatTransport
    {
        void Send(int sender, string text);
        event Action<int, string> Received;
    }

    /// <summary>Single-player transport: every sent line is received immediately by the local player.</summary>
    public sealed class LocalChatTransport : IChatTransport
    {
        public event Action<int, string> Received;
        public void Send(int sender, string text) => Received?.Invoke(sender, text);
    }

    /// <summary>Chat model: validates outgoing text and formats received lines for the message log.</summary>
    public sealed class ChatChannel
    {
        public const int MaxLength = 120;
        IChatTransport transport;
        public event Action<int, string> MessageReceived;

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

        void OnReceived(int sender, string text) => MessageReceived?.Invoke(sender, Sanitize(text));

        /// <summary>Returns false when nothing sendable remains after sanitising.</summary>
        public bool Submit(int sender, string text)
        {
            string clean = Sanitize(text);
            if (clean.Length == 0) return false;
            transport.Send(sender, clean);
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

        /// <summary>"Tú: hola" for the local player, "IA 3 · Violeta: hola" for others.</summary>
        public static string Format(int sender, string text) =>
            (sender == 0 ? "Tú" : VisualFactory.TeamName(sender)) + ": " + text;
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
        public static bool Supported => PlatformPresentation.TouchCapable;
        public static void Arm(string placeholder) { RiskAI_ChatArm(placeholder, ChatChannel.MaxLength); }
        public static void Open(string placeholder) { WebGLInput.captureAllKeyboardInput = false; RiskAI_ChatOpen(placeholder, ChatChannel.MaxLength); }
        public static void Close() { RiskAI_ChatClose(); WebGLInput.captureAllKeyboardInput = true; }
        public static State Poll() => (State)RiskAI_ChatPoll();
        public static string Take() => RiskAI_ChatTake() ?? string.Empty;
#else
        public static bool Supported => false;
        public static void Arm(string placeholder) { }
        public static void Open(string placeholder) { }
        public static void Close() { }
        public static State Poll() => State.Closed;
        public static string Take() => string.Empty;
#endif
    }
}
