using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class CombatPositionUtility
{
    public static bool TryFindShipCombatPosition(VehiclePawn vehicle, out IntVec3 dest, out Rot8 endRot)
    {
        endRot = Rot8.Invalid;
        dest = vehicle.Position;
        var target = vehicle.mindState.enemyTarget;
        if (target is null)
        {
            dest = vehicle.Position;
            return false;
        }
        var radius = Mathf.Max(target.def.Size.x, target.def.Size.z) * 2;
        var offset = vehicle is VehiclePawnWithMap { CompNpcVehicleMap.Params.preferredDir.IsVertical: true }
            ? vehicle.VehicleDef.Size.z
            : vehicle.VehicleDef.Size.x;
        var collisionRect = target.OccupiedRect().ExpandedBy(offset / 2 + 1);
        var root = target.Position + Rot8.FromAngle((vehicle.Position - target.Position).AngleFlat).FacingCell *
            Mathf.Min(target.def.Size.x, target.def.Size.z) / 2;
        var num = GenRadial.NumCellsInRadius(radius);
        for (var i = 0; i < num; i++)
        {
            var intVec = GenRadial.RadialPattern[i] + root;
            if (collisionRect.Contains(intVec)) continue;
            if (vehicle.CanReachVehicle(intVec, PathEndMode.OnCell, Danger.Deadly))
            {
                if (vehicle is VehiclePawnWithMap { CompNpcVehicleMap: { } compNpcVehicleMap })
                {
                    var dir = compNpcVehicleMap.Params.preferredDir;
                    var angle = (intVec - target.PositionOnBaseMap).AngleFlat;
                    var rot2 = Rot8.FromAngle(angle);
                    endRot = new Rot8(Rot8.FromIntClockwise((rot2.AsIntClockwise + dir.AsIntClockwise) % 8));
                }

                return PathingHelper.TryFindNearestStandableCell(vehicle, intVec, out dest);
            }
        }

        return false;
    }
}