using System;
using System.Linq;
using SmashTools;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompMapExpander : ThingComp
{
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        if (parent.IsOnVehicleMapOf(out var vehicle))
            ResizeVehicle(vehicle);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        if (map.IsVehicleMapOf(out var vehicle))
            ResizeVehicle(vehicle);
    }

    private static void ResizeVehicle(VehiclePawnWithMap vehicle)
    {
        var curSize = vehicle.def.size;
        var mapRect = CellRect.WholeMap(vehicle.VehicleMap);
        var newRect = CellRect.FromCellList(mapRect.Except(vehicle.CachedStructureCells));
        var newSize = newRect.Size;
        if (curSize != newSize)
        {
            vehicle.def.size = newSize;
            var offset = mapRect.CenterVector3 - newRect.CenterVector3;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                var data = vehicle.VehicleGraphic.DataRgb;
                var prevOffset = data.drawOffset;
                data.drawOffset = offset;
                data.drawOffsetNorth = offset;
                data.drawOffsetEast = offset.RotatedBy(Rot4.East);
                data.drawOffsetSouth = offset.RotatedBy(Rot4.South);
                data.drawOffsetWest = offset.RotatedBy(Rot4.West);
                if (vehicle.Spawned)
                {
                    var diff = prevOffset - offset;
                    var opp = Convert.ToInt32(vehicle.Rotation.AsInt > 1);
                    if ((diff.x < 0f) == (newSize.x % 2 == opp))
                    {
                        vehicle.Position += (IntVec3.East * (int)(diff.x * 2f)).RotatedBy(vehicle.Rotation);
                    }
                    if ((diff.z < 0f) == (newSize.z % 2 == opp))
                    {
                        vehicle.Position += (IntVec3.North * (int)(diff.z * 2f)).RotatedBy(vehicle.Rotation);
                    }
                    vehicle.DrawTracker.tweener.ResetTweenedPosToRoot();
                }
            });
            if (vehicle.Spawned)
            {
                vehicle.Map.GetCachedMapComponent<VehiclePathingSystem>().RequestGridsFor(vehicle);
            }
        }
    }
}
