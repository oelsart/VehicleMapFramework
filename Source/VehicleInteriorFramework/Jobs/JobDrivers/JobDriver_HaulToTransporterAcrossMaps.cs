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
            this.pawn.ReserveAsManyAsPossible(this.job.GetTargetQueue(TargetIndex.A), this.job, 1, -1, null);
            this.pawn.ReserveAsManyAsPossible(this.job.GetTargetQueue(TargetIndex.B), this.job, 1, -1, null);
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
                var transporter = base.Container.TryGetComp<CompTransporter>();
                var gatherFromBaseMap = !(transporter is CompBuildableContainer container) || container.GatherFromBaseMap;
                thingCount = LoadTransportersJobOnVehicleUtility.FindThingToLoad(this.pawn, transporter, gatherFromBaseMap, out var exitSpot, out var enterSpot);
                this.SetSpots(exitSpot, enterSpot);
            }
            if (this.job.playerForced && this.pawn.carryTracker.CarriedThing != null && this.pawn.carryTracker.CarriedThing != thingCount.Thing)
            {
                this.pawn.carryTracker.TryDropCarriedThing(this.pawn.Position, ThingPlaceMode.Near, out Thing thing, null);
            }
            this.job.targetA = thingCount.Thing;
            this.job.count = thingCount.Count;
            this.initialCount = thingCount.Count;
            this.pawn.Reserve(thingCount.Thing.MapHeld, thingCount.Thing, this.job, 1, -1, null, true, false);
        }

        public int initialCount;
    }
}
