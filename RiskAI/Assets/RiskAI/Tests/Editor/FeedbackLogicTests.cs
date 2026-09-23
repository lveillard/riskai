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
        public void LegacyMessagesAreClassified()
        {
            Assert.That(MessageLog.Classify("Ronda 4 · +7 de oro"), Is.EqualTo(MessageKind.Income));
            Assert.That(MessageLog.Classify("Oro insuficiente para comprar este barco."), Is.EqualTo(MessageKind.NoGold));
            Assert.That(MessageLog.Classify("×2 Espadachín encargados · 6 oro"), Is.EqualTo(MessageKind.Purchase));
            Assert.That(MessageLog.Classify("Embarque terminado: 2 / 10."), Is.EqualTo(MessageKind.Info));
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
            channel.MessageReceived += (sender, text) => { from = sender; received = text; };
            Assert.That(channel.Submit(0, "   "), Is.False);
            Assert.That(received, Is.Null);
            Assert.That(channel.Submit(0, " ¡a por ellos! "), Is.True);
            Assert.That(received, Is.EqualTo("¡a por ellos!"));
            Assert.That(from, Is.EqualTo(0));
            Assert.That(ChatChannel.Format(0, received), Is.EqualTo("Tú: ¡a por ellos!"));
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
        public void HeadlessCorpseDelayKeepsHistoricalTiming()
        {
            Assert.That(SoldierPool.CorpseDelay(null, true), Is.EqualTo(1.4f));
            Assert.That(SoldierPool.CorpseDelay(null, false), Is.EqualTo(0));
            Assert.That(SoldierPool.CorpseSeconds, Is.GreaterThan(SoldierPool.MinimumCorpseSeconds));
        }
    }
}
