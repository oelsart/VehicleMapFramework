using System;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class CrossMapRegionProcessorClosestThingReachable : RegionProcessorClosestThingReachable
{
    private Map rootMap;

    public void SetParameters(TraverseParms traverseParams, float maxDistance, IntVec3 root, bool ignoreEntirelyForbiddenRegions, ThingRequest req, PathEndMode peMode, Func<Thing, float> priorityGetter, Predicate<Thing> validator, int minRegions, float closestDistSquared = 9999999f, int regionsSeenScan = 0, float bestPrio = -3.4028235E+38f, Thing closestThing = null, bool lookInHaulSources = false, Map rootMap = null)
    {
        base.SetParameters(traverseParams, maxDistance, root, ignoreEntirelyForbiddenRegions, req, peMode, priorityGetter, validator, minRegions, closestDistSquared, regionsSeenScan, bestPrio, closestThing, lookInHaulSources);
        this.rootMap = rootMap;
    }

    new public void Clear()
    {
        base.Clear();
        rootMap = null;
    }

    protected override bool RegionProcessor(Region reg)
    {
        if (reg.Map == rootMap)
        {
            if (RegionTraverser.ShouldCountRegion(reg))
            {
                regionsSeenScan++;
            }
            return false;
        }
        return this.RegionProcessorBaseMapCoord(reg);
    }
}
