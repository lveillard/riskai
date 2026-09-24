namespace RiskAI.Core
{
    /// <summary>What one click does for the whole selection. Each actor then runs it with its own motor.</summary>
    public enum ClickDecision : byte
    {
        Attack,
        Capture,
        Board,
        Follow,
        /// <summary>Plain move to a post, harbour or town. The motor picks the claim point or the berth.</summary>
        OrderPlace,
        OrderGround
    }

    /// <summary>
    /// Click facts from the rules layer. Nothing here is a unit class or a domain:
    /// the selection contributes attack, capture, embark and follow capabilities.
    /// </summary>
    public readonly struct ClickContext
    {
        public readonly bool CanAttackTarget, CanCapture, CanEmbark, CanFollow;
        public readonly bool HasEnemy, OwnTransport, HasAlly, HasPlace, PlaceHostile, AttackArmed;

        public ClickContext(bool canAttackTarget, bool canCapture, bool canEmbark, bool canFollow,
            bool hasEnemy, bool ownTransport, bool hasAlly, bool hasPlace, bool placeHostile, bool attackArmed)
        {
            CanAttackTarget = canAttackTarget; CanCapture = canCapture; CanEmbark = canEmbark; CanFollow = canFollow;
            HasEnemy = hasEnemy; OwnTransport = ownTransport; HasAlly = hasAlly; HasPlace = hasPlace;
            PlaceHostile = placeHostile; AttackArmed = attackArmed;
        }
    }

    /// <summary>
    /// One priority for every actor. An attackable enemy unit beats the place under it.
    /// Capture is only the place itself, and only when someone selected can capture.
    /// </summary>
    public static class ClickRules
    {
        public static ClickDecision Resolve(in ClickContext click)
        {
            if (click.AttackArmed)
            {
                if (click.HasEnemy && click.CanAttackTarget) return ClickDecision.Attack;
                if (click.HasPlace && click.PlaceHostile && click.CanCapture) return ClickDecision.Capture;
                return ClickDecision.OrderGround;
            }
            if (click.HasEnemy && click.CanAttackTarget) return ClickDecision.Attack;
            if (click.OwnTransport && click.CanEmbark) return ClickDecision.Board;
            if (click.HasAlly && click.CanFollow) return ClickDecision.Follow;
            if (click.HasPlace) return click.PlaceHostile && click.CanCapture ? ClickDecision.Capture : ClickDecision.OrderPlace;
            return ClickDecision.OrderGround;
        }
    }
}
