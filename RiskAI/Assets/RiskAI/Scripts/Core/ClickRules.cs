namespace RiskAI.Core
{
    /// <summary>What a right-click (or an armed left-click) does. Priority is the table in <see cref="ClickRules"/>.</summary>
    public enum ClickDecision : byte
    {
        Ignore,
        FleetToHarbor,
        FleetToTownPort,
        Attack,
        Capture,
        Board,
        Follow,
        OrderHarbor,
        OrderTown,
        OrderGround
    }

    /// <summary>Click facts. The controller fills them; the table does not know actor classes.</summary>
    public readonly struct ClickContext
    {
        public readonly bool HasSelection, HasLand, HasFleet, FleetCanAttack;
        public readonly bool EnemyIsShip, HasEnemy, ClickedPost;
        public readonly bool HasHarbor, TownHasPort, HasTown, OwnTransport, HasFollowAlly;
        public readonly bool HarborHostile, TownHostile, AttackArmed;

        public ClickContext(bool hasSelection, bool hasLand, bool hasFleet, bool fleetCanAttack,
            bool enemyIsShip, bool hasEnemy, bool clickedPost, bool hasHarbor, bool townHasPort, bool hasTown,
            bool ownTransport, bool hasFollowAlly, bool harborHostile, bool townHostile, bool attackArmed)
        {
            HasSelection = hasSelection; HasLand = hasLand; HasFleet = hasFleet; FleetCanAttack = fleetCanAttack;
            EnemyIsShip = enemyIsShip; HasEnemy = hasEnemy; ClickedPost = clickedPost;
            HasHarbor = hasHarbor; TownHasPort = townHasPort; HasTown = hasTown;
            OwnTransport = ownTransport; HasFollowAlly = hasFollowAlly;
            HarborHostile = harborHostile; TownHostile = townHostile; AttackArmed = attackArmed;
        }
    }

    /// <summary>Current click priority, as data: an enemy ship beats a harbor, a post becomes capture, a friendly transport boards.</summary>
    public static class ClickRules
    {
        public static ClickDecision Resolve(in ClickContext click)
        {
            if (click.AttackArmed) return click.HasEnemy ? (click.ClickedPost ? ClickDecision.Capture : ClickDecision.Attack) : ClickDecision.OrderGround;
            bool shipTarget = click.EnemyIsShip && click.FleetCanAttack;
            if (!shipTarget && click.HasHarbor && click.HasFleet) return ClickDecision.FleetToHarbor;
            if (!shipTarget && click.TownHasPort && click.HasFleet) return ClickDecision.FleetToTownPort;
            if (click.HasEnemy && click.HasSelection) return click.ClickedPost ? ClickDecision.Capture : ClickDecision.Attack;
            if (click.OwnTransport && click.HasLand) return ClickDecision.Board;
            if (click.HasHarbor && click.HasFleet) return ClickDecision.FleetToHarbor;
            if (click.HasFollowAlly && click.HasLand) return ClickDecision.Follow;
            if (click.ClickedPost) return ClickDecision.Capture;
            if (click.HasHarbor) return click.HarborHostile ? ClickDecision.Capture : ClickDecision.OrderHarbor;
            if (click.HasTown) return click.TownHostile ? ClickDecision.Capture : ClickDecision.OrderTown;
            return ClickDecision.OrderGround;
        }
    }
}
