using RiskAI.Core;

namespace RiskAI
{
    /// <summary>
    /// One admission check for every actor. Target, team, transport and capture-post rules live here.
    /// The actor adds only its post release and its motor (NavMesh or sea path).
    /// </summary>
    public static class OrderValidation
    {
        public static bool Check(BattleSession session, IOrderable actor, in UnitCommand command, bool plan, out string error)
        {
            error = null;
            if (actor == null) { error = OrderQueue.InvalidError; return false; }
            if (session == null || session.Paused || session.Winner >= 0)
            {
                error = "La partida está detenida.";
                return false;
            }
            // Relief is only checked here. The post is released when the order starts.
            if (!actor.ReleasePost(command, false, out error)) return false;
            if (!Aim(session, actor, command, out error)) return false;
            return actor.Reach(command, plan, out error);
        }

        static bool Aim(BattleSession session, IOrderable actor, in UnitCommand command, out string error)
        {
            error = null;
            if (!UnitRules.KindAllowed(actor.Type.Domain, command.Kind))
            {
                error = OrderQueue.InvalidError;
                return false;
            }
            switch (command.Kind)
            {
                case UnitCommandKind.Attack:
                {
                    var enemy = session.FindTarget(command.TargetId);
                    if (!enemy || !enemy.IsAlive || !enemy.CanBeAttacked || enemy.Team == actor.Team)
                    {
                        error = OrderQueue.InvalidError;
                        return false;
                    }
                    return true;
                }
                case UnitCommandKind.Follow:
                {
                    var ally = session.FindTarget(command.TargetId);
                    if (!ally || !ally.IsAlive || ally.Type.Domain != UnitDomain.Land || ally.Team != actor.Team || ally.EntityId == actor.EntityId)
                    {
                        error = OrderQueue.InvalidError;
                        return false;
                    }
                    return true;
                }
                case UnitCommandKind.Embark:
                {
                    var transport = session.FindTarget(command.TargetId);
                    if (!transport || !transport.IsAlive || !transport.Type.CanTransport || transport.Team != actor.Team)
                    {
                        error = "Selecciona un transporte.";
                        return false;
                    }
                    return true;
                }
                case UnitCommandKind.Capture:
                {
                    var view = CapturePlan.Look(session, command);
                    bool harbor = view.Harbor || (view.Town && view.Town.Port);
                    if (!view.Found || (actor.Type.Domain == UnitDomain.Sea && !harbor))
                    {
                        error = actor.Type.Domain == UnitDomain.Sea
                            ? "Elige un puerto de desembarco."
                            : "Elige una ciudad o un puerto.";
                        return false;
                    }
                    return true;
                }
                default:
                    return true;
            }
        }
    }
}
