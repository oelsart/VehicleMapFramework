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
        toil.tickIntervalAction = _ =>
        {
            if (toil.actor is not VehiclePawn vehicle ||
                !CombatPositionUtility.TryFindShipCombatPosition(vehicle, out var dest)) return;
            vehicle.jobs.curJob.SetTarget(TargetIndex.A, dest);
            if (vehicle.Position == dest)
            {
                vehicle.jobs.curDriver.ReadyForNextToil();
                return;
            }

            vehicle.vehiclePather.StartPath(dest, PathEndMode.OnCell);
        };
        yield return toil;
    }
}