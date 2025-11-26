using System.Collections.Generic;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobDriver_GotoShipCombat : JobDriver_Goto
{
    protected override IEnumerable<Toil> MakeNewToils()
    {
        var toil = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
        toil.FailOn(() => job.GetTarget(TargetIndex.A).Thing is Pawn { ParentHolder: Corpse });
        toil.FailOn(() => job.GetTarget(TargetIndex.A).Thing is { Destroyed: true });
        toil.tickIntervalAction += _ =>
        {
            if (toil.actor is not VehiclePawn vehicle ||
                !CombatPositionUtility.TryFindShipCombatPosition(vehicle, out var dest, out var endRot)) return;
            
            var curTarget = vehicle.jobs.curJob.GetTarget(TargetIndex.A);
            if (curTarget != dest)
            {
                vehicle.jobs.curJob.SetTarget(TargetIndex.A, dest);
                if (vehicle.Position == dest)
                {
                    vehicle.jobs.curDriver.ReadyForNextToil();
                    return;
                }
        
                if (endRot.IsValid)
                    vehicle.vehiclePather.SetEndRotation(endRot);
                vehicle.vehiclePather.StartPath(dest, PathEndMode.OnCell);
            }
        };
        yield return toil;
    }
}