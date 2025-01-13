using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class ToilFailConditionsAcrossMaps
    {
        public static T FailOnBurningImmobile<T>(this T f, TargetIndex ind, Map map) where T : IJobEndable
        {
            f.AddEndCondition(delegate
            {
                Pawn actor = f.GetActor();
                LocalTargetInfo target = actor.jobs.curJob.GetTarget(ind);
                return (!target.IsValid || !target.ToTargetInfo(map).IsBurning()) ? JobCondition.Ongoing : JobCondition.Incompletable;
            });
            return f;
        }
    }
}
