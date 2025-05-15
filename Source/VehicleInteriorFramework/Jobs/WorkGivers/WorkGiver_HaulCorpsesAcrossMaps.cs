using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class WorkGiver_HaulCorpsesAcrossMaps : WorkGiver_HaulAcrossMaps
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Corpse))
            {
                return null;
            }

            Map map = t.MapHeld ?? pawn.Map;
            Pawn pawn2 = map.physicalInteractionReservationManager.FirstReserverOf(t);
            if (pawn2 != null && pawn2.RaceProps.Animal && pawn2.Faction != Faction.OfPlayer)
            {
                return null;
            }

            return base.JobOnThing(pawn, t, forced);
        }

        public override string PostProcessedGerund(Job job)
        {
            if (job.GetTarget(TargetIndex.B).Thing is Building_Grave)
            {
                return "Burying".Translate();
            }

            return base.PostProcessedGerund(job);
        }
    }
}
