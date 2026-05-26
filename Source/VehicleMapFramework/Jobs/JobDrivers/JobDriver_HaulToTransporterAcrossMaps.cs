using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

[Obsolete]
public class JobDriver_HaulToTransporterAcrossMaps : JobDriver_HaulToContainer
{
    public CompTransporter Transporter => Container?.TryGetComp<CompTransporter>();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref initialCount, "initialCount");
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.A), job);
        pawn.ReserveAsManyAsPossible(job.GetTargetQueue(TargetIndex.B), job);
        return true;
    }

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        ThingCount thingCount;
        if (job.targetA.IsValid)
        {
            thingCount = new ThingCount(job.targetA.Thing, job.targetA.Thing.stackCount);
        }
        else
        {
            var transporter = Container.TryGetComp<CompTransporter>();
            var gatherFromBaseMap = transporter is not CompBuildableContainer container || container.GatherFromBaseMap;
            thingCount = LoadTransportersJobOnVehicleUtility.FindThingToLoad(pawn, transporter, gatherFromBaseMap);
        }
        if (job.playerForced && pawn.carryTracker.CarriedThing != null && pawn.carryTracker.CarriedThing != thingCount.Thing)
        {
            pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
        }
        job.targetA = thingCount.Thing;
        job.count = thingCount.Count;
        initialCount = thingCount.Count;
        pawn.Reserve(thingCount.Thing.MapHeld, thingCount.Thing, job);
    }

    public int initialCount;
}
