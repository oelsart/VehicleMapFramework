using RimWorld;
using SmartFarming;
using System.Collections.Generic;
using UnityEngine;
using VehicleInteriors;
using Verse;
using static SmartFarming.Mod_SmartFarming;

namespace VMF_SmartFarmingPatch
{
    public static class GenDrawOnVehicleSF
    {
        public static void DrawFieldEdges(List<IntVec3> cells, Zone zone, Map map)
        {
            if (zone is Zone_Growing gZone &&
                compCache.TryGetValue(map?.uniqueID ?? -1, out MapComponent_SmartFarming mapComp) &&
                mapComp.growZoneRegistry.TryGetValue(gZone.ID, out ZoneData zoneData))
            {
                Color color;
                switch (zoneData.priority)
                {
                    case SmartFarming.ZoneData.Priority.Low:
                        {
                            color = ResourceBank.grey; break;
                        }
                    case SmartFarming.ZoneData.Priority.Preferred:
                        {
                            color = ResourceBank.green; break;
                        }
                    case SmartFarming.ZoneData.Priority.Important:
                        {
                            color = ResourceBank.yellow; break;
                        }
                    case SmartFarming.ZoneData.Priority.Critical:
                        {
                            color = ResourceBank.red; break;
                        }
                    default:
                        {
                            color = ResourceBank.white; break;
                        }
                }

                GenDrawOnVehicle.DrawFieldEdges(cells, color, null, map);
            }
            GenDrawOnVehicle.DrawFieldEdges(cells, map);
        }
    }
}
