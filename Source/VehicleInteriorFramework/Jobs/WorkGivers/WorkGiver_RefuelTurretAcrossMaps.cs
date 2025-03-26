using RimWorld;
using System;
using Verse;

namespace VehicleInteriors
{
    [Obsolete]
    public class WorkGiver_RefuelTurretAcrossMaps : WorkGiver_RefuelAcrossMaps
    {
        public override JobDef JobStandard
        {
            get
            {
                return VMF_DefOf.VMF_RearmTurretAcrossMaps;
            }
        }

        public override JobDef JobAtomic
        {
            get
            {
                return VMF_DefOf.VMF_RearmTurretAtomicAcrossMaps;
            }
        }

        public override bool CanRefuelThing(Thing t)
        {
            return t is Building_Turret;
        }
    }
}
