using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    /// <summary>
    /// One click, one decision, for whatever is selected. Capabilities come from units.json;
    /// the table does not know land from sea. An enemy unit is always an attack when someone
    /// can attack it. Capture is only the place itself, and only when someone can capture.
    /// </summary>
    public sealed class ClickRulesTests
    {
        static ClickContext Click(bool canAttack = false, bool canCapture = false, bool canEmbark = false, bool canFollow = false,
            bool enemy = false, bool transport = false, bool ally = false, bool place = false, bool hostile = false, bool armed = false) =>
            new ClickContext(canAttack, canCapture, canEmbark, canFollow, enemy, transport, ally, place, hostile, armed);

        [Test]
        public void AnEnemyUnitInsideAPlaceIsAttacked()
        {
            // The owner's case: a ship that can hit ground, and a soldier in a harbour circle.
            // The same row is the land unit. The place does not matter.
            Assert.That(ClickRules.Resolve(Click(canAttack: true, canCapture: true, enemy: true, place: true, hostile: true)), Is.EqualTo(ClickDecision.Attack));
            Assert.That(ClickRules.Resolve(Click(canAttack: true, enemy: true, place: true)), Is.EqualTo(ClickDecision.Attack));
        }

        [Test]
        public void AnEmptyHostilePlaceIsCaptureAndAFriendlyPlaceIsAMove()
        {
            Assert.That(ClickRules.Resolve(Click(canCapture: true, place: true, hostile: true)), Is.EqualTo(ClickDecision.Capture));
            Assert.That(ClickRules.Resolve(Click(canCapture: true, place: true)), Is.EqualTo(ClickDecision.OrderPlace));
            Assert.That(ClickRules.Resolve(Click(place: true, hostile: true)), Is.EqualTo(ClickDecision.OrderPlace), "an actor that cannot capture walks or sails there");
        }

        [Test]
        public void AGuardianUnitIsAttackedAndTheEmptyPostIsCaptured()
        {
            // Clicking the guardian unit is Attack, including for a mixed selection:
            // one decision, and each actor that can attack that unit receives it.
            // Actors that cannot attack it do not capture the post under the unit.
            Assert.That(ClickRules.Resolve(Click(canAttack: true, canCapture: true, enemy: true, place: true, hostile: true)), Is.EqualTo(ClickDecision.Attack));
            Assert.That(ClickRules.Resolve(Click(canCapture: true, place: true, hostile: true)), Is.EqualTo(ClickDecision.Capture));
        }

        [Test]
        public void OwnTransportBoardsAndAnAllyIsFollowed()
        {
            Assert.That(ClickRules.Resolve(Click(canEmbark: true, canCapture: true, transport: true, place: true, hostile: true)), Is.EqualTo(ClickDecision.Board));
            Assert.That(ClickRules.Resolve(Click(canFollow: true, ally: true, place: true)), Is.EqualTo(ClickDecision.Follow));
            Assert.That(ClickRules.Resolve(Click(ally: true, place: true, hostile: true, canCapture: true)), Is.EqualTo(ClickDecision.Capture), "no follow capability leaves the place");
        }

        [Test]
        public void AnEnemyNobodyCanAttackFallsThroughToThePlace()
        {
            Assert.That(ClickRules.Resolve(Click(canCapture: true, enemy: true, place: true, hostile: true)), Is.EqualTo(ClickDecision.Capture));
            Assert.That(ClickRules.Resolve(Click(enemy: true, place: true)), Is.EqualTo(ClickDecision.OrderPlace));
        }

        [Test]
        public void ArmedClickAttacksAUnitCapturesAHostilePlaceOrMoves()
        {
            Assert.That(ClickRules.Resolve(Click(canAttack: true, enemy: true, place: true, hostile: true, armed: true)), Is.EqualTo(ClickDecision.Attack));
            Assert.That(ClickRules.Resolve(Click(canCapture: true, place: true, hostile: true, armed: true)), Is.EqualTo(ClickDecision.Capture));
            Assert.That(ClickRules.Resolve(Click(armed: true)), Is.EqualTo(ClickDecision.OrderGround));
            Assert.That(ClickRules.Resolve(Click(canAttack: true, canCapture: true)), Is.EqualTo(ClickDecision.OrderGround));
        }

        [Test]
        public void TheClickTableIsTheContract()
        {
            Assert.That(ClickRules.Resolve(Click(canAttack: true, canCapture: true, enemy: true, place: true, hostile: true)), Is.EqualTo(ClickDecision.Attack));
            Assert.That(ClickRules.Resolve(Click(canCapture: true, place: true, hostile: true)), Is.EqualTo(ClickDecision.Capture));
            Assert.That(ClickRules.Resolve(Click(canCapture: true, place: true)), Is.EqualTo(ClickDecision.OrderPlace));
            Assert.That(ClickRules.Resolve(Click(place: true, hostile: true)), Is.EqualTo(ClickDecision.OrderPlace));
            Assert.That(ClickRules.Resolve(Click(canEmbark: true, transport: true)), Is.EqualTo(ClickDecision.Board));
            Assert.That(ClickRules.Resolve(Click(canFollow: true, ally: true)), Is.EqualTo(ClickDecision.Follow));
            Assert.That(ClickRules.Resolve(Click()), Is.EqualTo(ClickDecision.OrderGround));
        }
    }
}
