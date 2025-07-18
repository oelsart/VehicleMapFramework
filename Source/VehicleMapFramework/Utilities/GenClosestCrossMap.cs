using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class GenClosestCrossMap
{
    private static bool EarlyOutSearch(IntVec3 start, Map map, ThingRequest thingReq, IEnumerable<Thing> customGlobalSearchSet, Predicate<Thing> validator)
    {
        if (thingReq.group == ThingRequestGroup.Everything)
        {
            Log.Error("Cannot do ClosestThingReachable searching everything without restriction.");
            return true;
        }
        if (!start.InBounds(map))
        {
            Log.Error(string.Concat(
            [
                "Did FindClosestThing with start out of bounds (",
                start,
                "), thingReq=",
                thingReq
            ]));
            return true;
        }
        return thingReq.group == ThingRequestGroup.Nothing || ((thingReq.IsUndefined || !map.BaseMapAndVehicleMaps().SelectMany(m => m.listerThings.ThingsMatching(thingReq)).Any()) && customGlobalSearchSet.EnumerableNullOrEmpty());
    }

    public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, IEnumerable<Thing> customGlobalSearchSet = null, int searchRegionsMin = 0, int searchRegionsMax = -1, bool forceAllowGlobalSearch = false, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false)
    {
        return ClosestThingReachable(root, map, thingReq, peMode, traverseParams, maxDistance, validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax, forceAllowGlobalSearch, traversableRegionTypes, ignoreEntirelyForbiddenRegions, false);
    }

    public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, IEnumerable<Thing> customGlobalSearchSet = null, int searchRegionsMin = 0, int searchRegionsMax = -1, bool forceAllowGlobalSearch = false, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false, bool lookInHaulSources = false)
    {
        return ClosestThingReachable(root, map, thingReq, peMode, traverseParams, maxDistance, validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax, forceAllowGlobalSearch, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources, out _, out _);
    }

    public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, IEnumerable<Thing> customGlobalSearchSet, int searchRegionsMin, int searchRegionsMax, bool forceAllowGlobalSearch, RegionType traversableRegionTypes, bool ignoreEntirelyForbiddenRegions, bool lookInHaulSources, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        tmpExitSpot = TargetInfo.Invalid;
        tmpEnterSpot = TargetInfo.Invalid;
        exitSpotResult = TargetInfo.Invalid;
        enterSpotResult = TargetInfo.Invalid;
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        bool flag = searchRegionsMax < 0 || forceAllowGlobalSearch;
        if (!flag && customGlobalSearchSet != null)
        {
            Log.ErrorOnce("searchRegionsMax >= 0 && customGlobalSearchSet != null && !forceAllowGlobalSearch. customGlobalSearchSet will never be used.", 634984);
        }
        if (!flag && !thingReq.IsUndefined && !thingReq.CanBeFoundInRegion)
        {
            Log.ErrorOnce("ClosestThingReachable with thing request group " + thingReq.group + " and global search not allowed. This will never find anything because this group is never stored in regions. Either allow global search or don't call this method at all.", 518498981);
            return null;
        }
        if (map == null)
        {
            return null;
        }
        if (EarlyOutSearch(root, map, thingReq, customGlobalSearchSet, validator))
        {
            return null;
        }
        Thing thing = null;
        bool flag2 = false;
        if (!thingReq.IsUndefined && thingReq.CanBeFoundInRegion)
        {
            int num = (searchRegionsMax > 0) ? searchRegionsMax : 30;
            thing = RegionwiseBFSWorker(root, map, thingReq, peMode, traverseParams, validator, null, searchRegionsMin, num, maxDistance, out int num2, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources);
            if (thing != null && CrossMapReachabilityUtility.CanReach(map, root, thing, peMode, traverseParams, thing.MapHeld, out var exitSpot2, out var enterSpot2))
            {
                exitSpot = exitSpot2;
                enterSpot = enterSpot2;
                return thing;
            }
            else
            {
                thing = null;
            }
            flag2 = thing == null && num2 < num;
        }
        if (thing == null && flag && !flag2)
        {
            if (traversableRegionTypes != RegionType.Set_Passable)
            {
                Log.ErrorOnce("ClosestThingReachable had to do a global search, but traversableRegionTypes is not set to passable only. It's not supported, because Reachability is based on passable regions only.", 14384767);
            }

            var basePos = map.IsVehicleMapOf(out var vehicle) ? root.ToBaseMapCoord(vehicle) : root;
            var searchSet = customGlobalSearchSet ?? map.BaseMapAndVehicleMaps().SelectMany(m => m.listerThings.ThingsMatching(thingReq));
            bool Validator(Thing t)
            {
                if (!CrossMapReachabilityUtility.CanReach(map, root, t, peMode, traverseParams, t.MapHeld, out var exitSpot2, out var enterSpot2))
                {
                    return false;
                }
                if (validator != null && !validator(t))
                {
                    return false;
                }
                tmpExitSpot = exitSpot2;
                tmpEnterSpot = enterSpot2;
                return true;
            }
            thing = ClosestThing_Global(basePos, searchSet, maxDistance, Validator, null);
        }
        exitSpot = exitSpotResult;
        enterSpot = enterSpotResult;
        return thing;
    }

    public static Thing ClosestThing_Regionwise_ReachablePrioritized(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null, int minRegions = 24, int maxRegions = 30, bool lookInHaulSources = false)
    {
        if (!thingReq.IsUndefined && !thingReq.CanBeFoundInRegion)
        {
            Log.ErrorOnce("ClosestThing_Regionwise_ReachablePrioritized with thing request group " + thingReq.group.ToString() + ". This will never find anything because this group is never stored in regions. Most likely a global search should have been used.", 738476712);
            return null;
        }

        if (EarlyOutSearch(root, map, thingReq, null, validator))
        {
            return null;
        }

        if (maxRegions < minRegions)
        {
            Log.ErrorOnce("maxRegions < minRegions", 754343);
        }

        Thing result = null;
        if (!thingReq.IsUndefined)
        {
            result = RegionwiseBFSWorker(root, map, thingReq, peMode, traverseParams, validator, priorityGetter, minRegions, maxRegions, maxDistance, out var _, RegionType.Set_Passable, ignoreEntirelyForbiddenRegions: false, lookInHaulSources);
        }

        return result;
    }

    public static Thing RegionwiseBFSWorker(IntVec3 root, Map map, ThingRequest req, PathEndMode peMode, TraverseParms traverseParams, Predicate<Thing> validator, Func<Thing, float> priorityGetter, int minRegions, int maxRegions, float maxDistance, out int regionsSeen, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false)
    {
        return RegionwiseBFSWorker(root, map, req, peMode, traverseParams, validator, priorityGetter, minRegions, maxRegions, maxDistance, out regionsSeen, traversableRegionTypes, ignoreEntirelyForbiddenRegions);
    }

    public static Thing RegionwiseBFSWorker(IntVec3 root, Map map, ThingRequest req, PathEndMode peMode, TraverseParms traverseParams, Predicate<Thing> validator, Func<Thing, float> priorityGetter, int minRegions, int maxRegions, float maxDistance, out int regionsSeen, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false, bool lookInHaulSources = false)
    {
        regionsSeen = 0;
        if (traverseParams.mode == TraverseMode.PassAllDestroyableThings)
        {
            Log.Error("RegionwiseBFSWorker with traverseParams.mode PassAllDestroyableThings. Use ClosestThingGlobal.");
            return null;
        }

        if (traverseParams.mode == TraverseMode.PassAllDestroyablePlayerOwnedThings)
        {
            Log.Error("RegionwiseBFSWorker with traverseParams.mode PassAllDestroyablePlayerOwnedThings. Use ClosestThingGlobal.");
            return null;
        }

        if (traverseParams.mode == TraverseMode.PassAllDestroyableThingsNotWater)
        {
            Log.Error("RegionwiseBFSWorker with traverseParams.mode PassAllDestroyableThingsNotWater. Use ClosestThingGlobal.");
            return null;
        }

        if (!req.IsUndefined && !req.CanBeFoundInRegion)
        {
            Log.ErrorOnce(string.Concat("RegionwiseBFSWorker with thing request group ", req.group, ". This group is never stored in regions. Most likely a global search should have been used."), 385766189);
            return null;
        }

        Region region = root.GetRegion(map, traversableRegionTypes);
        if (region == null)
        {
            return null;
        }

        RegionProcessorClosestThingReachable regionProcessorClosestThingReachable = SimplePool<RegionProcessorClosestThingReachable>.Get();
        var basePos = map.IsVehicleMapOf(out var vehicle) ? root.ToBaseMapCoord(vehicle) : root;
        regionProcessorClosestThingReachable.SetParameters(traverseParams, maxDistance, basePos, ignoreEntirelyForbiddenRegions, req, peMode, priorityGetter, validator, minRegions, 9999999f, 0, float.MinValue, null, lookInHaulSources);
        RegionTraverserAcrossMaps.BreadthFirstTraverse(region, regionProcessorClosestThingReachable, maxRegions, traversableRegionTypes);
        regionsSeen = regionProcessorClosestThingReachable.regionsSeenScan;
        Thing closestThing = regionProcessorClosestThingReachable.closestThing;
        regionProcessorClosestThingReachable.Clear();
        SimplePool<RegionProcessorClosestThingReachable>.Return(regionProcessorClosestThingReachable);
        return closestThing;
    }

    public static Thing ClosestThing_Global(IntVec3 center, IEnumerable searchSet, float maxDistance = 99999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null)
    {
        return ClosestThing_Global(center, searchSet, maxDistance, validator, priorityGetter, false);
    }

    public static Thing ClosestThing_Global(IntVec3 center, IEnumerable searchSet, float maxDistance = 99999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null, bool lookInHaulSources = false)
    {
        if (searchSet == null)
        {
            return null;
        }
        var closestDistSquared = 2.1474836E+09f;
        Thing chosen = null;
        var bestPrio = float.MinValue;
        var maxDistanceSquared = maxDistance * maxDistance;
        if (searchSet is IList<Thing> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Process(list[i]);
            }
        }
        else if (searchSet is IList<Pawn> list2)
        {
            for (int i = 0; i < list2.Count; i++)
            {
                Process(list2[i]);
            }
        }
        else if (searchSet is IList<Building> list3)
        {
            for (int i = 0; i < list3.Count; i++)
            {
                Process(list3[i]);
            }
        }
        else if (searchSet is IList<IAttackTarget> list4)
        {
            for (int i = 0; i < list4.Count; i++)
            {
                Process((Thing)list4[i]);
            }
        }
        else
        {
            foreach (var target in searchSet)
            {
                Process((Thing)target);
            }
        }
        return chosen;

        void Process(Thing t)
        {
            if (!t.Spawned && !HaulAIUtility.IsInHaulableInventory(t))
            {
                return;
            }
            float num = (center - t.PositionHeldOnBaseMap()).LengthHorizontalSquared;
            if (num > maxDistanceSquared)
            {
                return;
            }
            if (priorityGetter != null || num < closestDistSquared)
            {
                ValidateThing(t, num);
                if (lookInHaulSources)
                {
                    if (t is IHaulSource haulSource)
                    {
                        ThingOwner directlyHeldThings = haulSource.GetDirectlyHeldThings();
                        for (int i = 0; i < directlyHeldThings.Count; i++)
                        {
                            ValidateThing(directlyHeldThings[i], num);
                        }
                    }
                }
            }
        }

        void ValidateThing(Thing t, float distSquared)
        {
            if (validator != null)
            {
                if (!validator(t))
                {
                    return;
                }
            }
            float num = 0f;
            if (priorityGetter != null)
            {
                num = priorityGetter(t);
                if (num < bestPrio)
                {
                    return;
                }
                if (Mathf.Approximately(num, bestPrio) && distSquared >= closestDistSquared)
                {
                    return;
                }
            }
            exitSpotResult = tmpExitSpot;
            enterSpotResult = tmpEnterSpot;
            chosen = t;
            closestDistSquared = distSquared;
            bestPrio = num;
        }
    }

    private static readonly object _lock = new();

    public static Thing ClosestThing_Global_Reachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null)
    {
        return ClosestThing_Global_Reachable(center, map, searchSet, peMode, traverseParams, maxDistance, validator, priorityGetter, false);
    }

    public static Thing ClosestThing_Global_Reachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null, bool canLookInHaulableSources = false)
    {
        return ClosestThing_Global_Reachable(center, map, searchSet, peMode, traverseParams, maxDistance, validator, priorityGetter, canLookInHaulableSources, out _, out _);
    }

    public static Thing ClosestThing_Global_Reachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, bool canLookInHaulableSources, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
        tmpExitSpot = TargetInfo.Invalid;
        tmpEnterSpot = TargetInfo.Invalid;
        exitSpotResult = TargetInfo.Invalid;
        enterSpotResult = TargetInfo.Invalid;
        exitSpot = TargetInfo.Invalid;
        enterSpot = TargetInfo.Invalid;
        if (searchSet == null)
        {
            return null;
        }
        var basePos = map.IsVehicleMapOf(out var vehicle) ? center.ToBaseMapCoord(vehicle) : center;
        var debug_changeCount = 0;
        var debug_scanCount = 0;
        Thing bestThing = null;
        var bestPrio = float.MinValue;
        var maxDistanceSquared = maxDistance * maxDistance;
        var closestDistSquared = 2.1474836E+09f;
        if (searchSet is IList<Thing> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Process(list[i]);
            }
        }
        else if (searchSet is IList<Pawn> list2)
        {
            for (int j = 0; j < list2.Count; j++)
            {
                Process(list2[j]);
            }
        }
        else if (searchSet is IList<Building> list3)
        {
            for (int k = 0; k < list3.Count; k++)
            {
                Process(list3[k]);
            }
        }
        else
        {
            foreach (Thing t in searchSet)
            {
                Process(t);
            }
        }
        exitSpot = exitSpotResult;
        enterSpot = enterSpotResult;
        return bestThing;

        void Process(Thing t)
        {
            if (t == null)
            {
                return;
            }
            if (!t.Spawned)
            {
                return;
            }
            debug_scanCount++;
            float num = (basePos - t.PositionHeldOnBaseMap()).LengthHorizontalSquared;
            if (num > maxDistanceSquared)
            {
                return;
            }
            if (priorityGetter != null || num < closestDistSquared)
            {
                ValidateThing(t, num);
                if (canLookInHaulableSources && t is IHaulSource haulSource)
                {
                    ThingOwner directlyHeldThings = haulSource.GetDirectlyHeldThings();
                    for (int i = 0; i < directlyHeldThings.Count; i++)
                    {
                        ValidateThing(directlyHeldThings[i], num);
                    }
                }
            }
        }

        void ValidateThing(Thing t, float distSquared)
        {
            if (!CrossMapReachabilityUtility.CanReach(map, center, t.SpawnedParentOrMe, peMode, traverseParams, t.MapHeld, out tmpExitSpot, out tmpEnterSpot))
            {
                return;
            }
            if (validator != null && !validator(t))
            {
                return;
            }
            float num = 0f;
            if (priorityGetter != null)
            {
                num = priorityGetter(t);
                if (num < bestPrio)
                {
                    return;
                }
                if (Mathf.Approximately(num, bestPrio) && distSquared >= closestDistSquared)
                {
                    return;
                }
            }
            exitSpotResult = tmpExitSpot;
            enterSpotResult = tmpEnterSpot;
            bestThing = t;
            closestDistSquared = distSquared;
            bestPrio = num;
            debug_changeCount++;
        }
    }


    private static TargetInfo tmpExitSpot;

    private static TargetInfo tmpEnterSpot;

    private static TargetInfo exitSpotResult;

    private static TargetInfo enterSpotResult;
}