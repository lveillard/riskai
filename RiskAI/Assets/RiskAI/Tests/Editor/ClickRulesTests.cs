using NUnit.Framework;
using RiskAI.Core;

namespace RiskAI.Tests
{
    /// <summary>The click table: same priority the controller used to spell out in branches.</summary>
    public sealed class ClickRulesTests
    {
        static ClickContext Click(bool fleet = false, bool fleetCanAttack = false, bool land = false, bool enemy = false,
            bool enemyShip = false, bool post = false, bool harbor = false, bool townPort = false, bool town = false,
            bool transport = false, bool ally = false, bool harborHostile = false, bool townHostile = false, bool armed = false) =>
            new ClickContext(land || fleet, land, fleet, fleetCanAttack, enemyShip, enemy, post, harbor, townPort, town,
                transport, ally, harborHostile, townHostile, armed);

        [Test]
        public void EnemyShipBeatsTheHarborItIsDockedBeside()
        {
            var decision = ClickRules.Resolve(Click(fleet: true, fleetCanAttack: true, enemy: true, enemyShip: true, harbor: true));
            Assert.That(decision, Is.EqualTo(ClickDecision.Attack));
        }

        [Test]
        public void FleetRightClickOnAHarborDocks()
        {
            Assert.That(ClickRules.Resolve(Click(fleet: true, harbor: true)), Is.EqualTo(ClickDecision.FleetToHarbor));
            Assert.That(ClickRules.Resolve(Click(fleet: true, townPort: true)), Is.EqualTo(ClickDecision.FleetToTownPort));
        }

        [Test]
        public void APostClickIsCaptureAndASoldierClickIsAttack()
        {
            Assert.That(ClickRules.Resolve(Click(land: true, enemy: true, post: true)), Is.EqualTo(ClickDecision.Capture));
            Assert.That(ClickRules.Resolve(Click(land: true, enemy: true)), Is.EqualTo(ClickDecision.Attack));
        }

        [Test]
        public void OwnTransportBoardsBeforeAHarborMove()
        {
            Assert.That(ClickRules.Resolve(Click(land: true, transport: true, harbor: true)), Is.EqualTo(ClickDecision.Board));
        }

        [Test]
        public void HostileGroundOnATownCapturesAndFriendlyGroundMoves()
        {
            Assert.That(ClickRules.Resolve(Click(land: true, town: true, townHostile: true)), Is.EqualTo(ClickDecision.Capture));
            Assert.That(ClickRules.Resolve(Click(land: true, town: true)), Is.EqualTo(ClickDecision.OrderTown));
            Assert.That(ClickRules.Resolve(Click(land: true)), Is.EqualTo(ClickDecision.OrderGround));
        }

        [Test]
        public void ArmedClickAttacksOrCaptureMoves()
        {
            Assert.That(ClickRules.Resolve(Click(land: true, enemy: true, armed: true)), Is.EqualTo(ClickDecision.Attack));
            Assert.That(ClickRules.Resolve(Click(land: true, enemy: true, post: true, armed: true)), Is.EqualTo(ClickDecision.Capture));
            Assert.That(ClickRules.Resolve(Click(land: true, armed: true)), Is.EqualTo(ClickDecision.OrderGround));
        }

        [Test]
        public void TheClickTableIsTheContract()
        {
            Assert.That(ClickRules.Resolve(Click(fleet: true, fleetCanAttack: true, enemy: true, enemyShip: true, harbor: true)), Is.EqualTo(ClickDecision.Attack));
            Assert.That(ClickRules.Resolve(Click(fleet: true, harbor: true)), Is.EqualTo(ClickDecision.FleetToHarbor));
            Assert.That(ClickRules.Resolve(Click(fleet: true, townPort: true)), Is.EqualTo(ClickDecision.FleetToTownPort));
            Assert.That(ClickRules.Resolve(Click(land: true, fleet: true, enemy: true)), Is.EqualTo(ClickDecision.Attack));
            Assert.That(ClickRules.Resolve(Click(land: true, transport: true)), Is.EqualTo(ClickDecision.Board));
            Assert.That(ClickRules.Resolve(Click(land: true, ally: true)), Is.EqualTo(ClickDecision.Follow));
            Assert.That(ClickRules.Resolve(Click(land: true, enemy: true, post: true)), Is.EqualTo(ClickDecision.Capture));
            Assert.That(ClickRules.Resolve(Click(land: true, town: true, townHostile: true)), Is.EqualTo(ClickDecision.Capture));
            Assert.That(ClickRules.Resolve(Click(land: true)), Is.EqualTo(ClickDecision.OrderGround));
        }

        [Test]
        public void MoveCursorDoesNotAttackOrCaptureWhatTheAttackCursorDoes()
        {
            Assert.That(ClickRules.Resolve(Click(land: true, enemy: true, armed: true)), Is.EqualTo(ClickDecision.Attack));
            Assert.That(ClickRules.Resolve(Click(land: true, enemy: true, post: true, armed: true)), Is.EqualTo(ClickDecision.Capture));
            // The move cursor passes armed: false and does not hand the click an enemy or a post.
            Assert.That(ClickRules.Resolve(Click(land: true)), Is.EqualTo(ClickDecision.OrderGround));
            Assert.That(ClickRules.Resolve(Click(land: true, enemy: true, post: true)), Is.EqualTo(ClickDecision.Capture), "a right-click on a post still captures");
        }
    }
}
