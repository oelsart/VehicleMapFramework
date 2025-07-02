using Verse;
using Verse.AI;

namespace VehicleInteriors;

public class WorkGiver_HaulGeneralAcrossMaps : WorkGiver_HaulAcrossMaps
{
    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is Corpse)
        {
            return null;
        }
        return base.JobOnThing(pawn, t, forced);
    }
}
