using System.Linq;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class Building_AttackTargetByComps : Building, IAttackTarget
{
    Thing IAttackTarget.Thing => this;

    LocalTargetInfo IAttackTarget.TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;
    
    float IAttackTarget.TargetPriorityFactor =>
        AllComps.OfType<IAttackTarget>().Select(a => a.TargetPriorityFactor).Aggregate((a, b) =>  a * b);

    bool IAttackTarget.ThreatDisabled(IAttackTargetSearcher disabledFor) =>
        AllComps.OfType<IAttackTarget>().Any(attackTarget => attackTarget.ThreatDisabled(disabledFor));
}