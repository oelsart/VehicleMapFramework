using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobDriver_HaulToTransporterAcrossMaps : JobDriver_HaulToContainer
{
    public CompTransporter Transporter
    {
        get
        {
            if (Container == null)
            {
                return null;
            }
            return Container.TryGetComp<CompTransporter>();
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look<int>(ref initialCount, "initialCount", 0, false);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.A), job, 1, -1, null);
        pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.B), job, 1, -1, null);
        return true;
    }

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        ThingCount thingCount;
        if (job.targetA.IsValid)
        {
            thingCount = new ThingCount(job.targetA.Thing, job.targetA.Thing.stackCount, false);
        }
        else
        {
            var transporter = Container.TryGetComp<CompTransporter>();
            var gatherFromBaseMap = transporter is not CompBuildableContainer container || container.GatherFromBaseMap;
            thingCount = LoadTransportersJobOnVehicleUtility.FindThingToLoad(pawn, transporter, gatherFromBaseMap);
        }
        if (job.playerForced && pawn.carryTracker.CarriedThing != null && pawn.carryTracker.CarriedThing != thingCount.Thing)
        {
            pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out Thing thing, null);
        }
        job.targetA = thingCount.Thing;
        job.count = thingCount.Count;
        initialCount = thingCount.Count;
        pawn.Reserve(thingCount.Thing.MapHeld, thingCount.Thing, job, 1, -1, null, true, false);
    }

    public int initialCount;
}
