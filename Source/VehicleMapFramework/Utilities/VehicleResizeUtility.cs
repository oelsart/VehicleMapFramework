using System;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public static class VehicleResizeUtility
{
    public static void Reposition(VehiclePawn vehicle, Vector3 delta)
    {
        if (vehicle.Spawned)
        {
            vehicle.Position += new IntVec3(
                (int)MathF.Truncate(delta.x),
                0,
                (int)MathF.Truncate(delta.z)).RotatedBy(vehicle.Rotation);
            var opp = Convert.ToInt32(vehicle.Rotation.AsInt > 1);
            if ((delta.x < 0f) == (vehicle.VehicleDef.Size.x % 2 == opp))
            {
                vehicle.Position += (IntVec3.East * (int)(delta.x % 1f * 2f)).RotatedBy(vehicle.Rotation);
            }
            if ((delta.z < 0f) == (vehicle.VehicleDef.Size.z % 2 == opp))
            {
                vehicle.Position += (IntVec3.North * (int)(delta.z % 1f * 2f)).RotatedBy(vehicle.Rotation);
            }

            vehicle.DrawTracker.tweener.ResetTweenedPosToRoot();
            if (!vehicle.vehiclePather.Moving)
            {
                vehicle.vehiclePather.nextCell = vehicle.Position;
            }
        }
    }

    public static void RefreshVehiclePather(VehiclePawn vehicle)
    {
        var component = vehicle.Map.GetCachedMapComponent<VehiclePathingSystem>();
        UniqueVehicleUtility.PathData?.Invoke(vehicle.vehiclePather, SingleParam.Get(component[vehicle.VehicleDef]));
#if DEV
        if (!component.ThreadAvailable ||
            component.dedicatedThread.State == DedicatedThread.ThreadState.Running)
        {
            component.RequestGridsFor(vehicleDef, DeferredGridGeneration.Urgency.Urgent);
        }
        else
        {
            component.RequestGridsFor(this);
        }
#else
        component.RequestGridsFor(vehicle.VehicleDef, DeferredGridGeneration.Urgency.Urgent);
#endif
    }
}
