using System;
using System.Collections.Generic;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleMapFollower(VehiclePawnWithMap vehicle)
{
    public readonly VehiclePawnWithMap vehicle = vehicle;

    private HashSet<IntVec3> prevOccupiedCells = [];

    private HashSet<IntVec3> tmpOccupiedCells = [];

    private IntVec3 prevCell = IntVec3.Invalid;

    private Rot8 prevRot = Rot8.Invalid;

    private float ticksToMove;

    private bool updated;

    public void MapFollowerTick()
    {
        if (!vehicle.Spawned) return;

        if (vehicle.Position != prevCell)
        {
            CrossMapReachabilityCache.ClearCacheFor(vehicle.VehicleMap);
            if (ticksToMove > 0)
            {
                UpdatePositionAndRotation();
            }
            ticksToMove = VehiclePathFollower.MoveTicksAt(vehicle, prevCell, vehicle.Position);
            prevCell = vehicle.Position;
            updated = false;
        }
        else
        {
            ticksToMove--;
        }
        if (!updated && ticksToMove <= 0)
        {
            UpdatePositionAndRotation();
            updated = true;
        }
        if (vehicle.FullRotation != prevRot)
        {
            UpdatePositionAndRotation();
            prevRot = vehicle.FullRotation;
        }
    }

    public void RegisterVehicle()
    {
        CalculateMapCells();
        var component = MapComponentCache<VehicleMapGrid>.GetComponent(vehicle.Map);
        foreach (var c in tmpOccupiedCells)
        {
            component.Register(c, vehicle);
        }
    }

    public void DeRegisterVehicle()
    {
        var component = MapComponentCache<VehicleMapGrid>.GetComponent(vehicle.Map);
        foreach (var c in prevOccupiedCells)
        {
            component.DeRegister(c, vehicle);
        }
    }

    private void UpdatePositionAndRotation()
    {
        CalculateMapCells();
        var component = MapComponentCache<VehicleMapGrid>.GetComponent(vehicle.Map);
        foreach (var c in tmpOccupiedCells)
        {
            if (!prevOccupiedCells.Contains(c))
                component.Register(c, vehicle);
        }
        foreach (var c in prevOccupiedCells)
        {
            if (!tmpOccupiedCells.Contains(c))
                component.DeRegister(c, vehicle);
        }
        (prevOccupiedCells, tmpOccupiedCells) = (tmpOccupiedCells, prevOccupiedCells);
    }

    private void CalculateMapCells()
    {
        tmpOccupiedCells.Clear();
        var mapSize = vehicle.VehicleMap.Size;
        var c1 = new IntVec3(0, 0, 0).ToBaseMapCoord(vehicle);
        var c2 = new IntVec3(mapSize.x - 1, 0, 0).ToBaseMapCoord(vehicle);
        var c3 = new IntVec3(0, 0, mapSize.z - 1).ToBaseMapCoord(vehicle);
        var c4 = new IntVec3(mapSize.x - 1, 0, mapSize.z - 1).ToBaseMapCoord(vehicle);
        var cellRect = CellRect.FromLimits(Mathf.Min(c1.x, c2.x, c3.x, c4.x), Mathf.Min(c1.z, c2.z, c3.z, c4.z), Mathf.Max(c1.x, c2.x, c3.x, c4.x), Mathf.Max(c1.z, c2.z, c3.z, c4.z));
        
        Span<IntVec3> adjBuffer = stackalloc IntVec3[4];
        var map = vehicle.Map;
        foreach (var cell in cellRect)
        {
            if (cell.ToVector3Shifted().TryGetVehicleMap(map, vehicle))
            {
                var adjCount = cell.AdjacentCellsCardinalNonAlloc(map, adjBuffer);
                for (var i = 0; i < adjCount; i++)
                {
                    tmpOccupiedCells.Add(adjBuffer[i]);
                }
            }
        }
    }
}
