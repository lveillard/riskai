using NUnit.Framework;
using UnityEngine;

namespace RiskAI.Tests
{
    public sealed class FeedbackLogicTests
    {
        static MessageEntry Entry(string text, float time, MessageKind kind = MessageKind.Info) =>
            new MessageEntry(text, kind, -1, false, Vector3.zero, time);

        [Test]
        public void MessageLogIsNewestFirstAndBounded()
        {
            var log = new MessageLog();
            for (int i = 0; i < MessageLog.Capacity + 3; i++) log.Add(Entry("m" + i, i * .1f));
            Assert.That(log.Count, Is.EqualTo(MessageLog.Capacity));
            Assert.That(log[0].Text, Is.EqualTo("m" + (MessageLog.Capacity + 2)));
            Assert.That(log[MessageLog.Capacity - 1].Text, Is.EqualTo("m3"));
        }

        [Test]
        public void RepeatedLinesMergeInsteadOfFlooding()
        {
            var log = new MessageLog();
            log.Add(Entry("Oro insuficiente.", 1, MessageKind.NoGold));
            log.Add(Entry("Oro insuficiente.", 2, MessageKind.NoGold));
            log.Add(Entry("Oro insuficiente.", 3, MessageKind.NoGold));
            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(log[0].Repeat, Is.EqualTo(3));
            Assert.That(log[0].Time, Is.EqualTo(3), "A repeat refreshes the fade timer.");
            log.Add(Entry("Oro insuficiente.", 3 + MessageLog.HoldSeconds + .1f, MessageKind.NoGold));
            Assert.That(log.Count, Is.EqualTo(2), "An old line is not revived by a much later repeat.");
        }

        [Test]
        public void MessagesHoldThenFadeAndDropOutOfView()
        {
            Assert.That(MessageLog.Alpha(0), Is.EqualTo(1));
            Assert.That(MessageLog.Alpha(MessageLog.HoldSeconds - .01f), Is.EqualTo(1));
            Assert.That(MessageLog.Alpha(MessageLog.HoldSeconds + MessageLog.FadeSeconds * .5f), Is.EqualTo(.5f).Within(.001f));
            Assert.That(MessageLog.Alpha(MessageLog.LifetimeSeconds), Is.EqualTo(0));
            var log = new MessageLog();
            log.Add(Entry("old", 0)); log.Add(Entry("mid", 3)); log.Add(Entry("new", 5));
            Assert.That(log.VisibleCount(5, 4), Is.EqualTo(3));
            Assert.That(log.VisibleCount(5, 2), Is.EqualTo(2), "Compact layouts cap the rows.");
            Assert.That(log.VisibleCount(MessageLog.LifetimeSeconds + .01f, 4), Is.EqualTo(2), "The oldest line fades out first.");
            Assert.That(log.VisibleCount(20, 4), Is.EqualTo(0));
        }

        [Test]
        public void ProductionResultsCarryTheirMessageKind()
        {
            Assert.That(new ProductionBatchResult(2, 2, 6, null, false).Kind, Is.EqualTo(MessageKind.Purchase));
            Assert.That(new ProductionBatchResult(1, 0, 0, "Oro insuficiente para comprar este barco.", true).Kind, Is.EqualTo(MessageKind.NoGold));
            Assert.That(new ProductionBatchResult(1, 0, 0, "Cola llena.", false).Kind, Is.EqualTo(MessageKind.Info));
        }

        [Test]
        public void AttackAlertsThrottlePerAreaAndNeighbours()
        {
            var alerts = new AttackAlerts();
            var here = new Vector3(10, 0, 10);
            Assert.That(alerts.TryRaise(here, 0), Is.True);
            Assert.That(alerts.TryRaise(here + Vector3.right * 2, 1), Is.False, "Same area inside the window.");
            Assert.That(alerts.TryRaise(here + Vector3.right * AttackAlerts.CellSize, 1), Is.False, "Adjacent cell must not double-alert a border fight.");
            Assert.That(alerts.TryRaise(here + Vector3.right * AttackAlerts.CellSize * 4, 1), Is.True, "A distant front raises its own alert.");
            Assert.That(alerts.TryRaise(here, AttackAlerts.ThrottleSeconds + .01f), Is.True, "The area re-arms after the window.");
            Assert.That(alerts.LastPosition, Is.EqualTo(here));
        }

        [Test]
        public void AlertJumpAndPingExpire()
        {
            var alerts = new AttackAlerts();
            Assert.That(alerts.CanJump(0), Is.False);
            alerts.TryRaise(Vector3.zero, 10);
            Assert.That(alerts.CanJump(10 + AttackAlerts.JumpWindowSeconds - 1), Is.True);
            Assert.That(alerts.CanJump(10 + AttackAlerts.JumpWindowSeconds + 1), Is.False);
            bool active = false;
            for (int i = 0; i < AttackAlerts.PingCapacity; i++) active |= alerts.PingProgress(i, 10.5f, out _) >= 0;
            Assert.That(active, Is.True);
            for (int i = 0; i < AttackAlerts.PingCapacity; i++) Assert.That(alerts.PingProgress(i, 10 + AttackAlerts.PingSeconds + .01f, out _), Is.LessThan(0));
        }

        [Test]
        public void GoldTweenEasesToIncomeAndSnapsOnOtherChanges()
        {
            var tween = new GoldTween();
            tween.Begin(10, 20, 0);
            int mid = tween.Value(20, GoldTween.Seconds * .5f);
            Assert.That(mid, Is.GreaterThan(10).And.LessThan(20));
            Assert.That(tween.Value(20, GoldTween.Seconds + .01f), Is.EqualTo(20));
            Assert.That(tween.Active, Is.False);
            tween.Begin(10, 20, 0);
            Assert.That(tween.Value(15, .1f), Is.EqualTo(15), "A purchase during the tween shows the real balance at once.");
            Assert.That(tween.Active, Is.False);
        }

        [Test]
        public void SfxThrottleCapsPlaysPerWindow()
        {
            var throttle = new SfxThrottle(2);
            Assert.That(throttle.TryPlay(0, 2, 0), Is.True);
            Assert.That(throttle.TryPlay(0, 2, .01f), Is.True);
            Assert.That(throttle.TryPlay(0, 2, .02f), Is.False, "Third hit inside 60 ms is dropped.");
            Assert.That(throttle.TryPlay(1, 2, .02f), Is.True, "Budgets are per clip.");
            Assert.That(throttle.TryPlay(0, 2, SfxThrottle.WindowSeconds + .011f), Is.True, "The window slides.");
            Assert.That(throttle.TryPlay(5, 1, 0), Is.False);
        }

        [Test]
        public void ChatSanitisesAndEchoesLocally()
        {
            Assert.That(ChatChannel.Sanitize("  hola   <b>mundo</b>\n "), Is.EqualTo("hola bmundo/b"));
            Assert.That(ChatChannel.Sanitize(new string('x', 400)).Length, Is.EqualTo(ChatChannel.MaxLength));
            var channel = new ChatChannel();
            string received = null; int from = -1;
            int to = 99;
            channel.MessageReceived += message => { from = message.Sender; to = message.Recipient; received = message.Text; };
            Assert.That(channel.Submit(0, "   "), Is.False);
            Assert.That(received, Is.Null);
            Assert.That(channel.Submit(0, " ¡a por ellos! "), Is.True);
            Assert.That(received, Is.EqualTo("¡a por ellos!"));
            Assert.That(from, Is.EqualTo(0));
            Assert.That(to, Is.EqualTo(ChatMessage.Everyone));
            Assert.That(ChatChannel.Format(new ChatMessage(0, ChatMessage.Everyone, received)), Is.EqualTo(GameText.Localize("Tú") + " → " + GameText.Localize("Todos") + ": ¡a por ellos!"), "Names are worded in the current language; the message text is the player's own.");
            Assert.That(channel.Submit(0, "hola", 2), Is.True);
            Assert.That(to, Is.EqualTo(2));
            Assert.That(ChatChannel.Format(new ChatMessage(0, 2, "hola")), Is.EqualTo(GameText.Localize("Tú") + " → " + VisualFactory.TeamName(2) + ": hola"));
            Assert.That(ChatChannel.Format(new ChatMessage(3, 0, "hola")), Is.EqualTo(VisualFactory.TeamName(3) + " → " + GameText.Localize("Tú") + ": hola"));
        }

        static CaptureEvent Capture(int previous, int owner, bool completed = false, bool lost = false) =>
            new CaptureEvent(Vector3.zero, previous, owner, "Encinar Bajo", null, false, 0, completed, lost);

        [Test]
        public void LosingACompleteCountryIsItsOwnLouderEvent()
        {
            // Cues are worded when they are raised, in the current language.
            ProbeHooks.SetLanguage(GameLanguage.Spanish);
            try { CheckLouderCountryLoss(); }
            finally { ProbeHooks.SetLanguage(GameLanguage.English); }
        }

        static void CheckLouderCountryLoss()
        {
            Assert.That(CaptureCue.For(Capture(0, 2, lost: true), "España", out var broken), Is.True);
            Assert.That(broken.Sound, Is.EqualTo(SfxId.CountryLost), "A broken country must not reuse city_lost.");
            Assert.That(broken.Headline, Is.EqualTo("¡Has perdido España!"));
            Assert.That(broken.Detail, Is.EqualTo("País roto: sin oro ni refuerzos de España"));
            Assert.That(broken.Loss && broken.Big, Is.True);

            Assert.That(CaptureCue.For(Capture(0, 2), "España", out var city), Is.True);
            Assert.That(city.Sound, Is.EqualTo(SfxId.CityLost));
            Assert.That(city.Text, Is.EqualTo("Has perdido Encinar Bajo"));
            Assert.That(city.Big, Is.False, "A plain city loss stays the smaller toast.");

            Assert.That(CaptureCue.For(Capture(0, 2, lost: true), null, out var unnamed), Is.True);
            Assert.That(unnamed.Sound, Is.EqualTo(SfxId.CityLost), "Without a country name there is nothing to announce as broken.");

            Assert.That(CaptureCue.For(Capture(2, 0, completed: true), "España", out var completed), Is.True);
            Assert.That(completed.Sound, Is.EqualTo(SfxId.CountryCompleted));
            Assert.That(completed.Headline, Is.EqualTo("¡País completado: España!"));
            Assert.That(CaptureCue.For(Capture(2, 0), "España", out var captured), Is.True);
            Assert.That(captured.Sound, Is.EqualTo(SfxId.CityCaptured));

            Assert.That(CaptureCue.For(Capture(2, 3, lost: true), "España", out _), Is.False, "Captures between other players are silent.");
        }

        [Test]
        public void BigAnnouncementsJumpTheToastQueueAndAreNeverEvicted()
        {
            var queue = new ToastQueue();
            queue.Add("Has perdido A", Color.red, false);
            queue.Add("Has perdido B", Color.red, false);
            queue.Add("¡Has perdido España!", Color.red, true);
            Assert.That(queue.Peek(0).text, Is.EqualTo("¡Has perdido España!"), "The broken country is shown next, ahead of routine city losses.");
            for (int i = 0; i < 10; i++) queue.Add("Has perdido " + i, Color.red, false);
            Assert.That(queue.Count, Is.EqualTo(ToastQueue.Capacity));
            Assert.That(queue.Peek(0).big, Is.True, "A burst of city toasts cannot evict the announcement.");
            queue.Add("¡País completado: Francia!", Color.blue, true);
            Assert.That(queue.TryTake(out var first), Is.True); Assert.That(first.text, Is.EqualTo("¡Has perdido España!"));
            Assert.That(queue.TryTake(out var second), Is.True); Assert.That(second.big, Is.True, "Big toasts keep their own order.");
            Assert.That(queue.TryTake(out var third), Is.True); Assert.That(third.big, Is.False);
        }

        [Test]
        public void CountryLostSoundIsAppendedWithoutMovingOtherClips()
        {
            Assert.That((int)SfxId.Chat, Is.EqualTo(24));
            Assert.That((int)SfxId.CountryLost, Is.EqualTo(25));
            var json = System.IO.File.ReadAllText("Assets/RiskAI/Resources/Audio/clips.json");
            Assert.That(json, Does.Contain("\"id\": \"country_lost\""));
        }

        [Test]
        public void BrokenCountryToastReadsPlainlyInEnglish()
        {
            ProbeHooks.SetLanguage(GameLanguage.English);
            try
            {
                Assert.That(GameText.Format("¡Has perdido {0}!", "Las Marcas"), Is.EqualTo("You lost The Marches!"));
                Assert.That(GameText.Format("País roto: sin oro ni refuerzos de {0}", "Las Marcas"), Is.EqualTo("Country broken: no gold or reinforcements from The Marches"));
                Assert.That(GameText.Format("Has perdido {0}", "Encinar Bajo"), Is.EqualTo("You lost Lower Oakwood"));
            }
            finally { ProbeHooks.SetLanguage(GameLanguage.English); }
        }

        [Test]
        public void PrivateChatIsVisibleOnlyToItsTwoPlayers()
        {
            Assert.That(new ChatMessage(2, ChatMessage.Everyone, "x").VisibleTo(0), Is.True);
            Assert.That(new ChatMessage(0, 3, "x").VisibleTo(0), Is.True);
            Assert.That(new ChatMessage(2, 0, "x").VisibleTo(0), Is.True);
            Assert.That(new ChatMessage(2, 3, "x").VisibleTo(0), Is.False);
            Assert.That(new ChatMessage(0, -7, "x").ToEveryone, Is.True);
        }

        [TestCase("/w azul hola", 1, "hola")]
        [TestCase("/w Blue hola", 1, "hola")]
        [TestCase("/azul hola que tal", 1, "hola que tal")]
        [TestCase("/blue hi", 1, "hi")]
        [TestCase("/w 3 cuidado", 3, "cuidado")]
        [TestCase("/azul claro vamos", 9, "vamos")]
        [TestCase("/light blue go", 9, "go")]
        [TestCase("/MARRÓN ok", 11, "ok")]
        [TestCase("/marron ok", 11, "ok")]
        [TestCase("/burdeos hola", 12, "hola")]
        [TestCase("/w burgundy hi", 12, "hi")]
        [TestCase("/todos hola", ChatMessage.Everyone, "hola")]
        [TestCase("/all hi", ChatMessage.Everyone, "hi")]
        public void ChatShortcutsNameTheRecipient(string line, int recipient, string body)
        {
            Assert.That(ChatChannel.TryParseRecipient(line, 16, player => player != 0, out int parsed, out string text, out bool unknown), Is.True);
            Assert.That(parsed, Is.EqualTo(recipient));
            Assert.That(text, Is.EqualTo(body));
            Assert.That(unknown, Is.False);
        }

        [Test]
        public void ChatShortcutsIgnorePlainTextAndReportUnknownWhispers()
        {
            Assert.That(ChatChannel.TryParseRecipient("hola a todos", 16, p => p != 0, out _, out string body, out bool unknown), Is.False);
            Assert.That(body, Is.EqualTo("hola a todos")); Assert.That(unknown, Is.False);
            Assert.That(ChatChannel.TryParseRecipient("/w nadie hola", 16, p => p != 0, out _, out _, out unknown), Is.False);
            Assert.That(unknown, Is.True, "A whisper to nobody is reported instead of broadcast.");
            Assert.That(ChatChannel.TryParseRecipient("/azul hola", 16, p => p != 1, out _, out _, out unknown), Is.False, "Eliminated or absent players cannot be named.");
            Assert.That(ChatChannel.TryParseRecipient("/azules hola", 16, p => p != 0, out _, out _, out _), Is.False, "Aliases match whole words only.");
            Assert.That(ChatChannel.TryParseRecipient("/rojo hola", 16, p => p != 0, out _, out _, out _), Is.False, "The local player cannot whisper to themselves.");
        }

        [Test]
        public void GaitPlaybackFollowsGroundSpeed()
        {
            float ratio = 1;
            Assert.That(GaitPolicy.RunPlayback(5.4f, ratio), Is.EqualTo(1.15f).Within(.001f), "Base footman speed keeps the tuned playback.");
            Assert.That(GaitPolicy.RunPlayback(5.4f * .6f, ratio), Is.LessThan(GaitPolicy.RunPlayback(5.4f, ratio)), "Forest slowdown slows the stride.");
            Assert.That(GaitPolicy.Runs(2.6f, ratio, false), Is.False);
            Assert.That(GaitPolicy.Runs(2.6f, ratio, true), Is.True, "Hysteresis keeps a running unit running near the threshold.");
            Assert.That(GaitPolicy.Runs(3.2f, ratio, false), Is.True);
        }

        [Test]
        public void LongCameraJumpsStayShort()
        {
            Assert.That(RtsCameraRig.JumpDuration(0, 34), Is.EqualTo(.3f).Within(.001f));
            Assert.That(RtsCameraRig.JumpDuration(10000, 34), Is.LessThanOrEqualTo(.42f));
        }

        [Test]
        public void HeadlessCorpseDelayIsShort()
        {
            Assert.That(SoldierPool.CorpseDelay(null, true), Is.EqualTo(1.4f));
            Assert.That(SoldierPool.CorpseDelay(null, false), Is.EqualTo(0));
            Assert.That(SoldierPool.CorpseSeconds, Is.GreaterThan(SoldierPool.MinimumCorpseSeconds));
        }
    }
}
