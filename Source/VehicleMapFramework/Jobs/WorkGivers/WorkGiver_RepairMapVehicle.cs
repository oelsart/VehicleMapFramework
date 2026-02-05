using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class WorkGiver_RepairMapVehicle : WorkGiver_Scanner
{
    public override Danger MaxPathDanger(Pawn pawn) => Danger.Some;
    
    public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return !pawn.IsOnVehicleMapOf(out var vehicle) || !vehicle.statHandler.NeedsRepairs;
    }

    public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
    {
        if (!pawn.IsOnVehicleMapOf(out var vehicle)) yield break;

        var map = vehicle.VehicleMap;
        var offset = VehicleMapUtility.HitboxToMapCell(vehicle);
        foreach (var component in vehicle.statHandler.ComponentsPrioritized)
        {
            if (component.HealthPercent >= 1f) continue;
            foreach (var cell in component.props.hitbox.Hitbox)
            {
                var cell2 = cell.ToIntVec3 + offset;
                if (cell2.InBounds(map))
                    yield return cell2;
            }
        }
    }

    public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
    {
        return pawn.CanReserveNew(c);
    }

    public override Job JobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
    {
        return pawn.IsOnVehicleMapOf(out var vehicle)
            ? JobMaker.MakeJob(VMF_DefOf.VMF_RepairMapVehicle, vehicle, c)
            : null;
    }
    
}