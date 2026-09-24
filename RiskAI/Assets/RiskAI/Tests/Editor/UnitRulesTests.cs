using NUnit.Framework;
using RiskAI.Core;
using RiskAI;

namespace RiskAI.Tests
{
    /// <summary>The shared combat rules: every unit type uses them, differences are data.</summary>
    public sealed class UnitRulesTests
    {
        [Test]
        public void MeasuresReproduceTheFourLegacyFormulas()
        {
            // 3 m right, 4 m forward, 12 m up.
            Assert.That(UnitRules.Measure(RangeMeasure.CenterToApproach, 3, 12, 4, .3f, .2f), Is.EqualTo(13f).Within(1e-5f));
            Assert.That(UnitRules.Measure(RangeMeasure.BodyEdges, 3, 12, 4, .3f, .2f), Is.EqualTo(12.5f).Within(1e-5f));
            Assert.That(UnitRules.Measure(RangeMeasure.ToHull, 3, 12, 4, .3f, .2f), Is.EqualTo(5f).Within(1e-5f));
            Assert.That(UnitRules.Measure(RangeMeasure.CenterToCenter, 3, 12, 4, .3f, .2f), Is.EqualTo(5f).Within(1e-5f));
            Assert.That(UnitRules.MeasuresToApproachPoint(RangeMeasure.CenterToCenter), Is.False);
            Assert.That(UnitRules.MeasuresToApproachPoint(RangeMeasure.ToHull), Is.True);
        }

        [Test]
        public void EveryTypeMeasuresAsUnitsJsonSays()
        {
            Assert.That(UnitCatalog.Get(UnitKind.Knight).Weapon.Measure, Is.EqualTo(RangeMeasure.BodyEdges));
            Assert.That(UnitCatalog.Get(UnitKind.Archer).Weapon.Measure, Is.EqualTo(RangeMeasure.CenterToApproach));
            Assert.That(UnitCatalog.Get(UnitKind.Frigate).Weapon.Measure, Is.EqualTo(RangeMeasure.ToHull));
            Assert.That(UnitCatalog.Get(UnitKind.Tower).TownWeapon.Measure, Is.EqualTo(RangeMeasure.CenterToCenter));
            // Melee acquires to the approach point, not body edge to body edge.
            Assert.That(UnitCatalog.Get(UnitKind.Knight).Acquisition.Measure, Is.EqualTo(RangeMeasure.CenterToApproach));
        }

        [Test]
        public void MeleeReachHasHysteresisAndRangedDoesNot()
        {
            ref readonly var knight = ref UnitCatalog.Get(UnitKind.Knight).Weapon;
            Assert.That(UnitRules.Reach(knight, false), Is.EqualTo(knight.Range - .2f).Within(1e-6f));
            Assert.That(UnitRules.Reach(knight, true), Is.EqualTo(knight.Range));
            ref readonly var archer = ref UnitCatalog.Get(UnitKind.Archer).Weapon;
            Assert.That(UnitRules.Reach(archer, false), Is.EqualTo(archer.Range));
            ref readonly var mortar = ref UnitCatalog.Get(UnitKind.Mortar).Weapon;
            Assert.That(UnitRules.TooClose(mortar, 4.9f), Is.True);
            Assert.That(UnitRules.StrikeLands(mortar, 18.5f), Is.True, "strike tolerance .55");
            Assert.That(UnitRules.StrikeLands(mortar, 18.6f), Is.False);
        }

        [Test]
        public void AcquisitionRadiusLeashAndTieBreakAreData()
        {
            ref readonly var footman = ref UnitCatalog.Get(UnitKind.Footman).Acquisition;
            Assert.That(UnitRules.AcquireRadius(footman, false, false), Is.EqualTo(7.5f));
            Assert.That(UnitRules.AcquireRadius(footman, false, true), Is.EqualTo(5f));
            Assert.That(UnitRules.AcquireRadius(footman, true, false), Is.EqualTo(.9f));
            Assert.That(UnitRules.Leash(footman, true), Is.EqualTo(7f));
            Assert.That(UnitRules.AcquireScore(footman, 3, 2), Is.EqualTo(3 + 2 * .48f).Within(1e-6f));
            Assert.That(UnitRules.BetterCandidate(footman, 3, 5, 3, 9, true), Is.True, "soldiers prefer the lower id on a tie");
            ref readonly var frigate = ref UnitCatalog.Get(UnitKind.Frigate).Acquisition;
            Assert.That(UnitRules.BetterCandidate(frigate, 3, 5, 3, 9, true), Is.False, "ships keep the first candidate found");
            Assert.That(frigate.HasLeash, Is.False);
        }

        [Test]
        public void TargetFlagsAdmitClassesAndRelations()
        {
            var frigate = UnitCatalog.Get(UnitKind.Frigate).Weapon;
            // Warship splash: debris/ground/structure/wall on enemies and neutrals, never allies.
            Assert.That(UnitRules.Allows(frigate.SplashTargets, WeaponTargetMask.Ground, UnitRelation.Enemy), Is.True);
            Assert.That(UnitRules.Allows(frigate.SplashTargets, WeaponTargetMask.Ground, UnitRelation.Ally), Is.False);
            // Mortar splash has no relation bits: it hurts allies too, but never the Mage-only class filter.
            var mortar = UnitCatalog.Get(UnitKind.Mortar).Weapon;
            Assert.That(UnitRules.Allows(mortar.SplashTargets, WeaponTargetMask.Ground | WeaponTargetMask.Soldier, UnitRelation.Ally), Is.True);
            Assert.That(UnitRules.Allows(mortar.SplashTargets, WeaponTargetMask.Ground, UnitRelation.Self), Is.False);
            var mage = UnitCatalog.Get(UnitKind.Mage).Weapon;
            Assert.That(UnitRules.Allows(mage.SplashTargets, WeaponTargetMask.Ground, UnitRelation.Enemy), Is.False, "the mage splash only hits soldiers");
            Assert.That(UnitRules.Allows(mage.SplashTargets, WeaponTargetMask.Ground | WeaponTargetMask.Soldier, UnitRelation.Neutral), Is.True);
            // A melee soldier can attack a ship as it always could; posts are never targets.
            var knight = UnitCatalog.Get(UnitKind.Knight).Weapon;
            Assert.That(UnitRules.CanAttack(knight, UnitCatalog.Get(UnitKind.Frigate), UnitRelation.Enemy), Is.True);
            Assert.That(UnitRules.CanAttack(knight, UnitCatalog.Get(UnitKind.Tower), UnitRelation.Enemy), Is.False);
            Assert.That(UnitRules.CanAttack(knight, UnitCatalog.Get(UnitKind.Footman), UnitRelation.Ally), Is.False);
            Assert.That(UnitRules.CanAttack(UnitCatalog.Get(UnitKind.Transport).Weapon, UnitCatalog.Get(UnitKind.Footman), UnitRelation.Enemy), Is.False);
        }

        [Test]
        public void OneQueueAdmitsShiftStopAndTheCap()
        {
            var queue = new OrderQueue();
            var move = new UnitCommand(0, 1, UnitCommandKind.Move, 1, 0, 2, append: true);
            Assert.That(queue.Admit(move, false), Is.EqualTo(OrderQueue.AdmitResult.Run), "the first Shift order on an idle unit starts now");
            Assert.That(queue.Count, Is.EqualTo(0));
            Assert.That(queue.Admit(move, true), Is.EqualTo(OrderQueue.AdmitResult.Queued));
            var replace = new UnitCommand(0, 1, UnitCommandKind.AttackMove, 3, 0, 4, append: false);
            Assert.That(queue.Admit(replace, true), Is.EqualTo(OrderQueue.AdmitResult.Run));
            Assert.That(queue.Count, Is.EqualTo(0), "an order without Shift replaces the queue");
            for (int i = 0; i < OrderQueue.Limit; i++)
                Assert.That(queue.Admit(move, true), Is.EqualTo(OrderQueue.AdmitResult.Queued));
            Assert.That(queue.Admit(move, true), Is.EqualTo(OrderQueue.AdmitResult.Full));
            Assert.That(queue.Admit(new UnitCommand(0, 1, UnitCommandKind.Stop, append: true), true), Is.EqualTo(OrderQueue.AdmitResult.Run));
            Assert.That(queue.Count, Is.EqualTo(0), "Stop never stays in the queue");
            Assert.That(queue.Admit(new UnitCommand(0, 1, UnitCommandKind.Hold), true), Is.EqualTo(OrderQueue.AdmitResult.Run));

            queue.Admit(move, true);
            queue.Publish(true, UnitCommandKind.Move, 8, 0, 9);
            Assert.That(queue.LegCount, Is.EqualTo(2));
            queue.Leg(0, out var x, out _, out var z, out var kind);
            Assert.That(x, Is.EqualTo(8f));
            Assert.That(z, Is.EqualTo(9f));
            Assert.That(kind, Is.EqualTo((byte)UnitCommandKind.Move));
            var attack = new UnitCommand(0, 1, UnitCommandKind.Attack, 4, 1, 5, targetId: 90, append: true);
            Assert.That(queue.Admit(attack, true), Is.EqualTo(OrderQueue.AdmitResult.Queued));
            queue.Publish(true, UnitCommandKind.Move, 8, 0, 9);
            queue.Stash(true, new UnitCommand(0, 1, UnitCommandKind.Follow, 8, 0, 9, targetId: 40, structureId: "town-1", structureKind: BuildingKind.Settlement));
            queue.Clear();
            queue.Publish(false, UnitCommandKind.Move, 0, 0, 0);
            Assert.That(queue.LegCount, Is.EqualTo(0));
            Assert.That(queue.StashCount, Is.EqualTo(3), "boarding keeps the active command and the queue");
            var restored = queue.StashedCommand(0);
            Assert.That(restored.Kind, Is.EqualTo(UnitCommandKind.Follow));
            Assert.That(restored.TargetId, Is.EqualTo(40));
            Assert.That(restored.StructureId, Is.EqualTo("town-1"));
            Assert.That(queue.StashedCommand(2).Kind, Is.EqualTo(UnitCommandKind.Attack));
            Assert.That(queue.StashedCommand(2).TargetId, Is.EqualTo(90));
            Assert.That(UnitRules.OnTargetLost(UnitCommandKind.AttackMove), Is.EqualTo(UnitRules.TargetLost.KeepDestination));
            Assert.That(UnitRules.OnTargetLost(UnitCommandKind.Attack), Is.EqualTo(UnitRules.TargetLost.Advance));
            Assert.That(UnitRules.OnTargetLost(UnitCommandKind.Move), Is.EqualTo(UnitRules.TargetLost.KeepDestination));
            Assert.That(UnitRules.KindAllowed(UnitDomain.Land, UnitCommandKind.Unload), Is.False);
            Assert.That(UnitRules.KindAllowed(UnitDomain.Sea, UnitCommandKind.Unload), Is.True);
            Assert.That(UnitRules.KindAllowed(UnitDomain.Land, UnitCommandKind.Patrol), Is.True);
            Assert.That(UnitRules.KindAllowed(UnitDomain.Sea, UnitCommandKind.Patrol), Is.False);
            Assert.That(UnitRules.KindAllowed(UnitDomain.Sea, UnitCommandKind.Capture), Is.True);
        }

        [Test]
        public void ShiftQueueMatchesThePlanMatrix()
        {
            var kinds = new[]
            {
                UnitCommandKind.Move, UnitCommandKind.AttackMove, UnitCommandKind.Attack, UnitCommandKind.Capture,
                UnitCommandKind.Follow, UnitCommandKind.Patrol, UnitCommandKind.Embark, UnitCommandKind.Unload
            };
            foreach (var kind in kinds)
            {
                Assert.That(UnitRules.Queue(kind, true, true), Is.EqualTo(UnitRules.OrderQueueAction.Append), kind + " appends while busy");
                Assert.That(UnitRules.Queue(kind, false, true), Is.EqualTo(UnitRules.OrderQueueAction.Start), kind + " replaces without Shift");
                Assert.That(UnitRules.Queue(kind, true, false), Is.EqualTo(UnitRules.OrderQueueAction.Start), kind + " starts now on an idle unit");
            }
            Assert.That(UnitRules.Queue(UnitCommandKind.Stop, true, true), Is.EqualTo(UnitRules.OrderQueueAction.Clear));
            Assert.That(UnitRules.Queue(UnitCommandKind.Hold, false, true), Is.EqualTo(UnitRules.OrderQueueAction.Clear));
            Assert.That(UnitRules.OnTargetLost(UnitCommandKind.Attack), Is.EqualTo(UnitRules.TargetLost.Advance));
            Assert.That(UnitRules.OnTargetLost(UnitCommandKind.AttackMove), Is.EqualTo(UnitRules.TargetLost.KeepDestination));

            var queue = new OrderQueue();
            queue.Admit(new UnitCommand(0, 1, UnitCommandKind.Move, 1, 0, 2, append: true), true);
            Assert.That(queue.Commit(new UnitCommand(0, 1, UnitCommandKind.Attack, append: false), true, false), Is.EqualTo(OrderQueue.AdmitResult.Rejected));
            Assert.That(queue.Count, Is.EqualTo(1), "a rejected order does not clear the queue");
        }

        [Test]
        public void RelationsFollowTeams()
        {
            Assert.That(UnitRules.Relation(1, 1, true, PlayerRules.NeutralTeam), Is.EqualTo(UnitRelation.Self));
            Assert.That(UnitRules.Relation(1, 1, false, PlayerRules.NeutralTeam), Is.EqualTo(UnitRelation.Ally));
            Assert.That(UnitRules.Relation(1, PlayerRules.NeutralTeam, false, PlayerRules.NeutralTeam), Is.EqualTo(UnitRelation.Neutral));
            Assert.That(UnitRules.Relation(1, 2, false, PlayerRules.NeutralTeam), Is.EqualTo(UnitRelation.Enemy));
        }

        [Test]
        public void CaptureEndsWhenThePostIsTakenNotWhenTheApproachFinishes()
        {
            var order = new CaptureOrderState();
            order.Begin(true, 2);
            Assert.That(order.Done(true, 2, 0, true, true), Is.False, "standing on an enemy post does not finish the capture");
            Assert.That(order.Done(true, 1, 0, false, true), Is.True, "another player taking the post finishes it at once");
            Assert.That(order.Done(true, PlayerRules.NeutralOwner, 0, true, true), Is.False, "a freed neutral post is still captured");
            order.Begin(true, 2);
            Assert.That(order.Done(true, 0, 0, false, true), Is.False, "a ship keeps sailing after the port becomes ours");
            Assert.That(order.Done(true, 0, 0, true, true), Is.True, "once the approach is done and the post is ours, the order ends");
            order.Begin(true, 0);
            Assert.That(order.Done(true, 0, 0, false, true), Is.False, "a friendly port waits for the disembark");
            Assert.That(order.Done(true, 0, 0, true, true), Is.True);
            order.Begin(true, 2);
            Assert.That(order.Done(true, 2, 0, false, false), Is.False, "a transport still sailing has not finished");
            Assert.That(order.Done(true, 1, 0, false, false), Is.False, "another player does not abort a transport before the unload");
            Assert.That(order.Done(true, 1, 0, true, false), Is.True, "a transport that cannot claim is done when the unload finishes");
            Assert.That(OrderAdvance.MotorIdle(UnitCommandKind.AttackMove, true), Is.False, "attack-move stays busy while a target lives");
            Assert.That(OrderAdvance.MotorIdle(UnitCommandKind.Attack, true), Is.False);
            Assert.That(OrderAdvance.MotorIdle(UnitCommandKind.Capture, true), Is.True, "a capture approach ignores a combat target");
            Assert.That(OrderAdvance.MotorIdle(UnitCommandKind.AttackMove, false), Is.True);
        }

        [Test]
        public void EmbarkStashMergesTheLiveQueueAndDoesNotDuplicate()
        {
            var queue = new OrderQueue();
            var active = new UnitCommand(0, 1, UnitCommandKind.Move, 2, 0, 3);
            queue.Stash(true, active);
            var extra = new UnitCommand(0, 1, UnitCommandKind.Move, 8, 0, 1, append: true);
            Assert.That(queue.Admit(extra, true), Is.EqualTo(OrderQueue.AdmitResult.Queued));
            queue.AppendStash(extra);
            queue.MergeQueue();
            Assert.That(queue.StashCount, Is.EqualTo(2));
            Assert.That(queue.StashedCommand(0).X, Is.EqualTo(2f));
            Assert.That(queue.StashedCommand(1).X, Is.EqualTo(8f));
            queue.MergeQueue();
            Assert.That(queue.StashCount, Is.EqualTo(2), "merging the same live order twice does not duplicate it");
        }

        [Test]
        public void AMissingDisembarkResultStaysPendingUntilTheShipConfirmsIt()
        {
            var slot = new DisembarkConfirmation.Slot();
            Assert.That(DisembarkConfirmation.Advance(ref slot, 4, true, false, false, false), Is.EqualTo(DisembarkConfirmation.Status.Pending));
            Assert.That(slot.Waiting, Is.True);
            Assert.That(DisembarkConfirmation.Advance(ref slot, 0, false, false, false, false), Is.EqualTo(DisembarkConfirmation.Status.Pending),
                "an evicted result is unknown, not a failure");
            Assert.That(DisembarkConfirmation.Advance(ref slot, 0, false, false, false, true), Is.EqualTo(DisembarkConfirmation.Status.Accepted));
            slot = new DisembarkConfirmation.Slot { CommandId = 4, Waiting = true };
            Assert.That(DisembarkConfirmation.Advance(ref slot, 0, false, true, false, true), Is.EqualTo(DisembarkConfirmation.Status.Rejected));
        }
    }
}
