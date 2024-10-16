using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobDriver_HaulToTransporterAcrossMaps : JobDriver_HaulToContainerAcrossMaps
    {
        public CompTransporter Transporter
        {
            get
            {
                if (base.Container == null)
                {
                    return null;
                }
                return base.Container.TryGetComp<CompTransporter>();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref this.initialCount, "initialCount", 0, false);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (this.job.targetA.HasThing)
            {
                this.pawn.ReserveAsManyAsPossible(this.job.targetA.Thing.Map, this.job.GetTargetQueue(TargetIndex.A), this.job, 1, -1, null);
            }
            if (this.job.targetB.HasThing)
            {
                this.pawn.ReserveAsManyAsPossible(this.job.targetB.Thing.Map, this.job.GetTargetQueue(TargetIndex.B), this.job, 1, -1, null);
            }
            return true;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            ThingCount thingCount;
            if (this.job.targetA.IsValid)
            {
                thingCount = new ThingCount(this.job.targetA.Thing, this.job.targetA.Thing.stackCount, false);
            }
            else
            {
                thingCount = LoadTransportersJobOnVehicleUtility.FindThingToLoad(this.pawn, base.Container.TryGetComp<CompTransporter>(), out var exitSpot, out var enterSpot);
                var gotoDestMap = JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps);
                var driver = gotoDestMap.GetCachedDriver(this.pawn) as JobDriverAcrossMaps;
                driver.SetSpots(exitSpot, enterSpot);
                this.pawn.jobs.StartJob(gotoDestMap, JobCondition.Ongoing, null, true);
            }
            if (this.job.playerForced && this.pawn.carryTracker.CarriedThing != null && this.pawn.carryTracker.CarriedThing != thingCount.Thing)
            {
                Thing thing;
                this.pawn.carryTracker.TryDropCarriedThing(this.pawn.Position, ThingPlaceMode.Near, out thing, null);
            }
            this.job.targetA = thingCount.Thing;
            this.job.count = thingCount.Count;
            this.initialCount = thingCount.Count;
            this.pawn.Reserve(thingCount.Thing.Map, thingCount.Thing, this.job, 1, -1, null, true, false);
        }

        public int initialCount;
    }
}
