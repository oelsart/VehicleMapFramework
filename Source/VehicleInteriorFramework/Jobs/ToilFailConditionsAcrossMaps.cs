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

        public static T FailOnSomeonePhysicallyInteracting<T>(this T f, TargetIndex ind) where T : IJobEndable
        {
            f.AddEndCondition(delegate
            {
                Pawn actor = f.GetActor();
                Thing thing = actor.jobs.curJob.GetTarget(ind).Thing;
                return (thing == null || !actor.Map.physicalInteractionReservationManager.IsReserved(thing) || actor.Map.physicalInteractionReservationManager.IsReservedBy(actor, thing)) ? JobCondition.Ongoing : JobCondition.Incompletable;
            });
            return f;
        }
    }
}
