using RimWorld;
using Verse;

namespace VehicleInteriors
{
    public class WorkGiver_RefuelTurretAcrossMaps : WorkGiver_RefuelAcrossMaps
    {
        public override JobDef JobStandard
        {
            get
            {
                return VIF_DefOf.VIF_RearmTurretAcrossMaps;
            }
        }

        public override JobDef JobAtomic
        {
            get
            {
                return VIF_DefOf.VIF_RearmTurretAtomicAcrossMaps;
            }
        }

        public override bool CanRefuelThing(Thing t)
        {
            return t is Building_Turret;
        }
    }
}
