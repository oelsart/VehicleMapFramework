using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class ToilFailConditionsAcrossMaps
    {

        public static T FailOnForbidden<T>(this T f, TargetIndex ind) where T : IJobEndable
        {
            f.AddEndCondition(delegate
            {
                Pawn actor = f.GetActor();
                if (actor.Faction != Faction.OfPlayer)
                {
                    return JobCondition.Ongoing;
                }
                if (actor.jobs.curJob.ignoreForbidden)
                {
                    return JobCondition.Ongoing;
                }
                Thing thing = actor.jobs.curJob.GetTarget(ind).Thing;
                if (thing == null)
                {
                    return JobCondition.Ongoing;
                }
                if (thing.IsForbidden(actor))
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            return f;
        }

        public static T FailOnDespawnedOrNull<T>(this T f, TargetIndex ind) where T : IJobEndable
        {
            f.AddEndCondition(delegate
            {
                if (ToilFailConditionsAcrossMaps.DespawnedOrNull(f.GetActor().jobs.curJob.GetTarget(ind), f.GetActor()))
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            return f;
        }

        public static T FailOnSelfAndParentsDespawnedOrNull<T>(this T f, TargetIndex ind) where T : IJobEndable
        {
            f.AddEndCondition(delegate
            {
                if (ToilFailConditionsAcrossMaps.SelfAndParentsDespawnedOrNull(f.GetActor().jobs.curJob.GetTarget(ind), f.GetActor()))
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            return f;
        }

        public static T FailOnDespawnedNullOrForbidden<T>(this T f, TargetIndex ind) where T : IJobEndable
        {
            f.FailOnDespawnedOrNull(ind);
            f.FailOnForbidden(ind);
            return f;
        }

        public static bool SelfAndParentsDespawnedOrNull(LocalTargetInfo target, Pawn actor)
        {
            Thing thing = target.Thing;
            return (thing != null || !target.IsValid) && (thing == null || !thing.SpawnedOrAnyParentSpawned || thing.MapHeldBaseMap() != actor.BaseMap());
        }

        public static bool DespawnedOrNull(this LocalTargetInfo target, Pawn actor)
        {
            Thing thing = target.Thing;
            return (thing != null || !target.IsValid) && (thing == null || !thing.Spawned || thing.BaseMap() != actor.BaseMap());
        }

        public static T FailOnSomeonePhysicallyInteracting<T>(this T f, TargetIndex ind) where T : IJobEndable
        {
            f.AddEndCondition(delegate
            {
                Pawn actor = f.GetActor();
                Thing thing = actor.jobs.curJob.GetTarget(ind).Thing;
                if (thing != null && thing.Map.physicalInteractionReservationManager.IsReserved(thing) && !thing.Map.physicalInteractionReservationManager.IsReservedBy(actor, thing))
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            return f;
        }

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
