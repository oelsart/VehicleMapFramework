using System;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class CrossMapRegionProcessorClosestThingReachable : RegionProcessorClosestThingReachable
{
    private Map rootMap;
    private TraverseParms traverseParams;
    private float maxDistance;
    private float maxDistSquared;
    private IntVec3 root;

    public void SetParameters(TraverseParms _traverseParams, float _maxDistance, IntVec3 _root, bool ignoreEntirelyForbiddenRegions, ThingRequest req, PathEndMode peMode, Func<Thing, float> priorityGetter, Predicate<Thing> validator, int minRegions, float closestDistSquared = 9999999f, int _regionsSeenScan = 0, float bestPrio = -3.4028235E+38f, Thing _closestThing = null, bool lookInHaulSources = false, Map _rootMap = null)
    {
        base.SetParameters(traverseParams, maxDistance, root, ignoreEntirelyForbiddenRegions, req, peMode, priorityGetter, validator, minRegions, closestDistSquared, _regionsSeenScan, bestPrio, _closestThing, lookInHaulSources);
        rootMap = _rootMap;
        traverseParams = _traverseParams;
        maxDistance = _maxDistance;
        root = _root;
        maxDistSquared = _maxDistance * _maxDistance;
    }

    public new void Clear()
    {
        base.Clear();
        rootMap = null;
    }

    protected override bool RegionEntryPredicate(Region from, Region to)
    {
        return to.Allows(traverseParams, false) && (maxDistance > 5000f ||
                                                    to.extentsClose.ClosestDistSquaredTo(
                                                        to.Map.IsVehicleMapOf(out var vehicle)
                                                            ? root.ToVehicleMapCoord(vehicle)
                                                            : root) < maxDistSquared);
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
