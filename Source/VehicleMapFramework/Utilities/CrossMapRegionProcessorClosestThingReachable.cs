using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;

public class CrossMapRegionProcessorClosestThingReachable : RegionProcessorClosestThingReachable
{
    protected override bool RegionProcessor(Region reg)
    {
        return this.RegionProcessorBaseMapCoord(reg);
    }
}
