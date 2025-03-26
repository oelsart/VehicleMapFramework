using RimWorld;
using System;
using VehicleInteriors.Jobs.WorkGivers;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    [Obsolete]
    public class WorkGiver_RefuelAcrossMaps : WorkGiver_Scanner, IWorkGiverAcrossMaps
    {
        public bool NeedVirtualMapTransfer => false;

        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                return ThingRequest.ForGroup(ThingRequestGroup.Refuelable);
            }
        }

        public override PathEndMode PathEndMode
        {
            get
            {
                return PathEndMode.Touch;
            }
        }

        public virtual JobDef JobStandard
        {
            get
            {
                return VMF_DefOf.VMF_RefuelAcrossMaps;
            }
        }

        public virtual JobDef JobAtomic
        {
            get
            {
                return VMF_DefOf.VMF_RefuelAtomicAcrossMaps;
            }
        }

        public virtual bool CanRefuelThing(Thing t)
        {
            return !(t is Building_Turret);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return this.CanRefuelThing(t) && RefuelWorkGiverUtilityOnVehicle.CanRefuel(pawn, t, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return RefuelWorkGiverUtilityOnVehicle.RefuelJob(pawn, t, forced, this.JobStandard, this.JobAtomic);
        }
    }
}