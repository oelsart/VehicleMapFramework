using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobGiver_AIDefendPointOnVehicle : JobGiver_AIFightEnemiesOnVehicle
    {
        protected override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
        {
            Thing enemyTarget = pawn.mindState.enemyTarget;
            Verb verb = verbToUse ?? pawn.TryGetAttackVerb(enemyTarget, !pawn.IsColonist, false);
            if (verb == null)
            {
                dest = IntVec3.Invalid;
                return false;
            }
            return CastPositionFinderOnVehicle.TryFindCastPosition(new CastPositionRequest
            {
                caster = pawn,
                target = enemyTarget,
                verb = verb,
                maxRangeFromTarget = 9999f,
                locus = (IntVec3)pawn.mindState.duty.focus,
                maxRangeFromLocus = pawn.mindState.duty.radius,
                wantCoverFromTarget = (verb.verbProps.range > 7f)
            }, out dest);
        }
    }
}
