using System.Collections.Generic;
using RiskAI.Core;
using UnityEngine;

namespace RiskAI
{
    /// <summary>Validated local command inbox. Network authentication is intentionally outside this adapter.</summary>
    public sealed class BattleCommands
    {
        readonly BattleSession session;
        readonly Queue<UnitCommand> queue = new Queue<UnitCommand>(256);
        public int PendingCount => queue.Count;
        public long AppliedCount { get; private set; }
        public BattleCommands(BattleSession battle) { session=battle; }
        public bool Submit(UnitCommand command)
        {
            if(session.Paused || session.Winner>=0 || queue.Count>=1024 || !Valid(command))return false;
            queue.Enqueue(command);return true;
        }
        bool Valid(UnitCommand command)
        {
            if(command.PlayerId<0 || command.PlayerId>1 || !Finite(command.X) || !Finite(command.Y) || !Finite(command.Z))return false;
            if(command.Kind<UnitCommandKind.Move || command.Kind>UnitCommandKind.Follow)return false;
            var unit=session.FindTarget(command.UnitId) as Soldier;
            if(!unit || !unit.IsAlive || unit.Team!=command.PlayerId || unit.IsGarrison)return false;
            if(command.Kind!=UnitCommandKind.Attack && command.Kind!=UnitCommandKind.Follow)return true;
            var target=session.FindTarget(command.TargetId);
            if(!target || !target.IsAlive)return false;
            return command.Kind==UnitCommandKind.Attack ? target.CanBeAttacked && target.Team!=unit.Team : target is Soldier && target.Team==unit.Team && target!=unit;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public void Tick()
        {
            while(queue.Count>0)
            {
                var command=queue.Dequeue();if(!Valid(command))continue;
                var unit=(Soldier)session.FindTarget(command.UnitId);
                var point=new Vector3(command.X,command.Y,command.Z);
                switch(command.Kind)
                {
                    case UnitCommandKind.Move: unit.MoveTo(point,false,command.Append); break;
                    case UnitCommandKind.AttackMove: unit.MoveTo(point,true,command.Append); break;
                    case UnitCommandKind.Patrol: unit.Patrol(point,command.Append); break;
                    case UnitCommandKind.Attack: unit.Attack(session.FindTarget(command.TargetId)); break;
                    case UnitCommandKind.Follow: unit.Follow(session.FindTarget(command.TargetId) as Soldier); break;
                    case UnitCommandKind.Stop: unit.Stop(); break;
                    case UnitCommandKind.Hold: unit.HoldPosition(); break;
                }
                AppliedCount++;
            }
        }
    }
}
