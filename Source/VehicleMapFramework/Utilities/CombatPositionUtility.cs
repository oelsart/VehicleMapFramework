using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public static class CombatPositionUtility
{
    public static bool TryFindShipCombatPosition(VehiclePawn vehicle, out IntVec3 dest)
    {
        
        var target = vehicle.mindState.enemyTarget;
        if (target is null)
        {
            dest = vehicle.Position;
            return false;
        }
        var radius = Mathf.Max(target.def.Size.x, target.def.Size.z) * 2;
        if (!PathingHelper.TryFindNearestStandableCell(vehicle, target.Position, out dest, radius))
            return false;

        if (vehicle is VehiclePawnWithMap { CompNpcVehicleMap: { } compNpcVehicleMap })
        {
            var dir = compNpcVehicleMap.Params.preferredDir;
            var angle = (target.PositionOnBaseMap() - dest).AngleFlat;
            var rot = new Rot8(Rot8.FromIntClockwise((Rot8.FromAngle(angle).AsIntClockwise + dir.AsIntClockwise) % 8));
            vehicle.vehiclePather.SetEndRotation(rot);
            var dest2 = dest + rot.Opposite.FacingCell;
            if (vehicle.DrivableRectOnCell(dest2, Ext_Vehicles.DestinationHitboxReq.AnyRotation))
                dest = dest2;
        }
        return true;
    }
}