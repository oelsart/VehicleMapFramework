using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class GenClosestCrossMap
{
    private static readonly List<Thing> tmpThings = [];
    
    private static bool EarlyOutSearch(IntVec3 start, ref Map map, ThingRequest thingReq, IEnumerable<Thing> customGlobalSearchSet)
    {
        if (thingReq.group == ThingRequestGroup.Everything)
        {
            Log.Error("Cannot do ClosestThingReachable searching everything without restriction.");
            return true;
        }
        if (!start.InBounds(map))
        {
            map = map.GroundMap;
            if (!start.InBounds(map))
            {
                Log.Error(string.Concat("Did FindClosestThing with start out of bounds (", start, "), thingReq=", thingReq));
                return true;
            }
        }

        if (thingReq.group == ThingRequestGroup.Nothing)
            return true;
        var flag = thingReq.IsUndefined;
        var flag2 = true;
        if (!flag)
        {
            foreach (var map2 in map.BaseMapAndVehicleMaps(false))
            {
                if (map2.listerThings.ThingsMatching(thingReq).Any())
                {
                    flag2 = false;
                    break;
                }
            }
        }
        return (flag || flag2) && customGlobalSearchSet.EnumerableNullOrEmpty();
    }

    public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, IEnumerable<Thing> customGlobalSearchSet = null, int searchRegionsMin = 0, int searchRegionsMax = -1, bool forceAllowGlobalSearch = false, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false, bool lookInHaulSources = false)
    {
        var flag = searchRegionsMax < 0 || forceAllowGlobalSearch;
        if (!flag && customGlobalSearchSet != null)
        {
            Log.ErrorOnce("searchRegionsMax >= 0 && customGlobalSearchSet != null && !forceAllowGlobalSearch. customGlobalSearchSet will never be used.", 634984);
        }
        if (!flag && thingReq is { IsUndefined: false, CanBeFoundInRegion: false })
        {
            Log.ErrorOnce("ClosestThingReachable with thing request group " + thingReq.group + " and global search not allowed. This will never find anything because this group is never stored in regions. Either allow global search or don't call this method at all.", 518498981);
            return null;
        }
        if (map == null)
        {
            return null;
        }
        if (EarlyOutSearch(root, ref map, thingReq, customGlobalSearchSet))
        {
            return null;
        }
        Thing thing = null;
        var flag2 = false;
        if (thingReq is { IsUndefined: false, CanBeFoundInRegion: true })
        {
            var num = (searchRegionsMax > 0) ? searchRegionsMax : 30;
            thing = RegionwiseBFSWorker(root, map, thingReq, peMode, traverseParams, validator, null, searchRegionsMin, num, maxDistance, out var num2, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources);
            flag2 = thing == null && num2 < num;
        }
        if (thing == null && flag && !flag2)
        {
            if (traversableRegionTypes != RegionType.Set_Passable)
            {
                Log.ErrorOnce("ClosestThingReachable had to do a global search, but traversableRegionTypes is not set to passable only. It's not supported, because Reachability is based on passable regions only.", 14384767);
            }

            var basePos = map.IsVehicleMapOf(out var vehicle) ? root.ToBaseMapCoord(vehicle) : root;
            if (customGlobalSearchSet is null)
            {
                tmpThings.Clear();
                foreach (var map2 in map.BaseMapAndVehicleMaps(false))
                {
                    var list = map2.listerThings.ThingsMatching(thingReq);
                    for (var i = 0; i < list.Count; i++)
                    {
                        tmpThings.Add(list[i]);
                    }
                }
            }
            
            var departMap = traverseParams.pawn?.DepartMap ?? map;
            bool GlobalValidator(Thing t)
            {
                if (!CrossMapReachabilityUtility.CanReach(departMap, root, t, peMode, traverseParams, t.MapHeld))
                {
                    return false;
                }
                return validator == null || validator(t);
            }
            thing = ClosestThing_Global(basePos, customGlobalSearchSet ?? tmpThings, maxDistance, GlobalValidator);
        }
        return thing;
    }

    public static Thing ClosestThing_Regionwise_ReachablePrioritized(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null, int minRegions = 24, int maxRegions = 30, bool lookInHaulSources = false)
    {
        if (thingReq is { IsUndefined: false, CanBeFoundInRegion: false })
        {
            Log.ErrorOnce("ClosestThing_Regionwise_ReachablePrioritized with thing request group " + thingReq.group + ". This will never find anything because this group is never stored in regions. Most likely a global search should have been used.", 738476712);
            return null;
        }

        if (EarlyOutSearch(root, ref map, thingReq, null))
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

    public static Thing RegionwiseBFSWorker(IntVec3 root, Map map, ThingRequest req, PathEndMode peMode, TraverseParms traverseParams, Predicate<Thing> validator, Func<Thing, float> priorityGetter, int minRegions, int maxRegions, float maxDistance, out int regionsSeen, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false, bool lookInHaulSources = false)
    {
        regionsSeen = 0;
        switch (traverseParams.mode)
        {
            case TraverseMode.PassAllDestroyableThings:
                Log.Error("RegionwiseBFSWorker with traverseParams.mode PassAllDestroyableThings. Use ClosestThingGlobal.");
                return null;
            case TraverseMode.PassAllDestroyablePlayerOwnedThings:
                Log.Error("RegionwiseBFSWorker with traverseParams.mode PassAllDestroyablePlayerOwnedThings. Use ClosestThingGlobal.");
                return null;
            case TraverseMode.PassAllDestroyableThingsNotWater:
                Log.Error("RegionwiseBFSWorker with traverseParams.mode PassAllDestroyableThingsNotWater. Use ClosestThingGlobal.");
                return null;
            case TraverseMode.ByPawn:
            case TraverseMode.PassDoors:
            case TraverseMode.NoPassClosedDoors:
            case TraverseMode.NoPassClosedDoorsOrWater:
            default: break;
        }

        if (req is { IsUndefined: false, CanBeFoundInRegion: false })
        {
            Log.ErrorOnce(string.Concat("RegionwiseBFSWorker with thing request group ", req.group, ". This group is never stored in regions. Most likely a global search should have been used."), 385766189);
            return null;
        }

        var region = root.GetRegion(map, traversableRegionTypes);
        if (region is null)
        {
            return null;
        }

        var regionProcessorClosestThingReachable = SimplePool<CrossMapRegionProcessorClosestThingReachable>.Get();
        var basePos = map.IsVehicleMapOf(out var vehicle) ? root.ToBaseMapCoord(vehicle) : root;
        regionProcessorClosestThingReachable.SetParameters(traverseParams, maxDistance, basePos, ignoreEntirelyForbiddenRegions, req, peMode, priorityGetter, validator, minRegions, 9999999f, 0, float.MinValue, null, lookInHaulSources, map);
        RegionTraverserAcrossMaps.BreadthFirstTraverse(region, regionProcessorClosestThingReachable, maxRegions, traversableRegionTypes);
        regionsSeen = regionProcessorClosestThingReachable.regionsSeenScan;
        var closestThing = regionProcessorClosestThingReachable.closestThing;
        regionProcessorClosestThingReachable.Clear();
        SimplePool<CrossMapRegionProcessorClosestThingReachable>.Return(regionProcessorClosestThingReachable);
        return closestThing;
    }

    public static Thing ClosestThing_Global(IntVec3 centerOnBaseMap, IEnumerable searchSet, float maxDistance = 99999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null, bool lookInHaulSources = false)
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
            for (var i = 0; i < list.Count; i++)
            {
                Process(list[i]);
            }
        }
        else if (searchSet is IList<Pawn> list2)
        {
            for (var i = 0; i < list2.Count; i++)
            {
                Process(list2[i]);
            }
        }
        else if (searchSet is IList<Building> list3)
        {
            for (var i = 0; i < list3.Count; i++)
            {
                Process(list3[i]);
            }
        }
        else if (searchSet is IList<IAttackTarget> list4)
        {
            for (var i = 0; i < list4.Count; i++)
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
            float num = (centerOnBaseMap - t.PositionHeldOnBaseMap()).LengthHorizontalSquared;
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
                        var directlyHeldThings = haulSource.GetDirectlyHeldThings();
                        for (var i = 0; i < directlyHeldThings.Count; i++)
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
            var num = 0f;
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
            chosen = t;
            closestDistSquared = distSquared;
            bestPrio = num;
        }
    }

    public static Thing ClosestThing_Global_Reachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null, bool canLookInHaulableSources = false)
    {
        if (searchSet == null)
        {
            return null;
        }
        var basePos = map.IsVehicleMapOf(out var vehicle) ? center.ToBaseMapCoord(vehicle) : center;
        Thing bestThing = null;
        var bestPrio = float.MinValue;
        var maxDistanceSquared = maxDistance * maxDistance;
        var closestDistSquared = 2.1474836E+09f;
        var careAboutHaulSourceEnabled = canLookInHaulableSources && traverseParams.pawn is { IsColonist: true };
        var departMap = traverseParams.pawn?.DepartMap ?? map;
        if (searchSet is IList<Thing> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                Process(list[i]);
            }
        }
        else if (searchSet is IList<Pawn> list2)
        {
            for (var j = 0; j < list2.Count; j++)
            {
                Process(list2[j]);
            }
        }
        else if (searchSet is IList<Building> list3)
        {
            for (var k = 0; k < list3.Count; k++)
            {
                Process(list3[k]);
            }
        }
        else
        {
            foreach (var t in searchSet)
            {
                Process(t);
            }
        }
        return bestThing;

        void Process(Thing t)
        {
            if (t is null || !t.Spawned)
                return;
            float num = (basePos - t.PositionHeldOnBaseMap()).LengthHorizontalSquared;
            if (num > maxDistanceSquared)
                return;
            if (priorityGetter != null || num < closestDistSquared)
            {
                ValidateThing(t, num);
                if (canLookInHaulableSources && t is IHaulSource haulSource &&
                    (!careAboutHaulSourceEnabled || haulSource.HaulSourceEnabled))
                {
                    var directlyHeldThings = haulSource.GetDirectlyHeldThings();
                    foreach (var t1 in directlyHeldThings)
                    {
                        ValidateThing(t1, num);
                    }
                }
            }
        }

        void ValidateThing(Thing t, float distSquared)
        {
            if (!CrossMapReachabilityUtility.CanReach(departMap, center, t.SpawnedParentOrMe, peMode, traverseParams, t.MapHeld))
                return;
            if (validator != null && !validator(t))
                return;
            var num = 0f;
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
            bestThing = t;
            closestDistSquared = distSquared;
            bestPrio = num;
        }
    }
}