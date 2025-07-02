using RimWorld;
using SmartFarming;
using System.Collections.Generic;
using VehicleInteriors;
using Verse;

namespace VMF_SmartFarmingPatch;

public static class GenDrawOnVehicleSF
{
    public static void DrawFieldEdges(List<IntVec3> cells, Zone zone, Map map)
    {
        if (zone is Zone_Growing gZone && Mod_SmartFarming.compCache.TryGetValue(Find.CurrentMap?.uniqueID ?? (-1), out var mapComp) && mapComp.growZoneRegistry.TryGetValue(gZone.ID, out var zoneData))
        {
            GenDrawOnVehicle.DrawFieldEdges(cells, zoneData.priority switch
            {
                ZoneData.Priority.Low => ResourceBank.grey,
                ZoneData.Priority.Preferred => ResourceBank.green,
                ZoneData.Priority.Important => ResourceBank.yellow,
                ZoneData.Priority.Critical => ResourceBank.red,
                _ => ResourceBank.white,
            }, map: map);
        }
        GenDrawOnVehicle.DrawFieldEdges(cells, map: map);
    }
}
