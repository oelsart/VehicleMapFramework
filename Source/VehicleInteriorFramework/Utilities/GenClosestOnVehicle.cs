using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class GenClosestOnVehicle
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
                Log.Error(string.Concat(new object[]
                {
                    "Did FindClosestThing with start out of bounds (",
                    start,
                    "), thingReq=",
                    thingReq
                }));
                return true;
            }
            return thingReq.group == ThingRequestGroup.Nothing || ((thingReq.IsUndefined || map.BaseMapAndVehicleMaps().SelectMany(m => m.listerThings.ThingsMatching(thingReq)).Count() == 0) && customGlobalSearchSet.EnumerableNullOrEmpty<Thing>());
        }

        public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, IEnumerable<Thing> customGlobalSearchSet = null, int searchRegionsMin = 0, int searchRegionsMax = -1, bool forceAllowGlobalSearch = false, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false)
        {
            return GenClosestOnVehicle.ClosestThingReachable(root, map, thingReq, peMode, traverseParams, maxDistance, validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax, forceAllowGlobalSearch, traversableRegionTypes, ignoreEntirelyForbiddenRegions, false);
        }

        public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, IEnumerable<Thing> customGlobalSearchSet = null, int searchRegionsMin = 0, int searchRegionsMax = -1, bool forceAllowGlobalSearch = false, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false, bool lookInHaulSources = false)
        {
            return GenClosestOnVehicle.ClosestThingReachable(root, map, thingReq, peMode, traverseParams, maxDistance, validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax, forceAllowGlobalSearch, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources, out _, out _);
        }

        public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, IEnumerable<Thing> customGlobalSearchSet, int searchRegionsMin, int searchRegionsMax, bool forceAllowGlobalSearch, RegionType traversableRegionTypes, bool ignoreEntirelyForbiddenRegions, bool lookInHaulSources, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            GenClosestOnVehicle.tmpExitSpot = TargetInfo.Invalid;
            GenClosestOnVehicle.tmpEnterSpot = TargetInfo.Invalid;
            GenClosestOnVehicle.exitSpotResult = TargetInfo.Invalid;
            GenClosestOnVehicle.enterSpotResult = TargetInfo.Invalid;
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
            var baseMap = map.BaseMap();
            var basePos = map.IsVehicleMapOf(out var vehicle) ? root.ToBaseMapCoord(vehicle) : root;
            if (GenClosestOnVehicle.EarlyOutSearch(root, map, thingReq, customGlobalSearchSet, validator))
            {
                return null;
            }
            Thing thing = null;
            bool flag2 = false;
            if (!thingReq.IsUndefined && thingReq.CanBeFoundInRegion)
            {
                int num = (searchRegionsMax > 0) ? searchRegionsMax : 30;
                thing = GenClosest.RegionwiseBFSWorker_NewTemp(root, map, thingReq, peMode, traverseParams, validator, null, searchRegionsMin, num, maxDistance, out int num2, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources);
                flag2 = thing == null && num2 < num && map == baseMap; //車上マップからRegionwiseBFSWorkerを呼んだ場合大抵1regionしか検索せずnum2 < numを満たしやすいため、map比較を条件として追加
            }
            if (thing == null && flag && !flag2)
            {
                if (traversableRegionTypes != RegionType.Set_Passable)
                {
                    Log.ErrorOnce("ClosestThingReachable had to do a global search, but traversableRegionTypes is not set to passable only. It's not supported, because Reachability is based on passable regions only.", 14384767);
                }
                bool validator2(Thing t)
                {
                    if (ReachabilityUtilityOnVehicle.CanReach(map, root, t, peMode, traverseParams, t.Map, out var exitSpot2, out var enterSpot2) && (validator == null || validator(t)))
                    {
                        GenClosestOnVehicle.tmpExitSpot = exitSpot2;
                        GenClosestOnVehicle.tmpEnterSpot = enterSpot2;
                        return true;
                    }
                    return false;
                }
                thing = GenClosestOnVehicle.ClosestThing_Global(basePos, customGlobalSearchSet ?? map.BaseMapAndVehicleMaps().SelectMany(m => m.listerThings.ThingsMatching(thingReq)), maxDistance, validator2, null);
            }
            exitSpot = GenClosestOnVehicle.exitSpotResult;
            enterSpot = GenClosestOnVehicle.enterSpotResult;
            return thing;
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
            regionProcessorClosestThingReachable.SetParameters_NewTemp(traverseParams, maxDistance, root, ignoreEntirelyForbiddenRegions, req, peMode, priorityGetter, validator, minRegions, 9999999f, 0, float.MinValue, null, lookInHaulSources);
            RegionTraverserAcrossMaps.BreadthFirstTraverse(region, regionProcessorClosestThingReachable, maxRegions, traversableRegionTypes);
            regionsSeen = regionProcessorClosestThingReachable.regionsSeenScan;
            Thing closestThing = regionProcessorClosestThingReachable.closestThing;
            regionProcessorClosestThingReachable.Clear();
            SimplePool<RegionProcessorClosestThingReachable>.Return(regionProcessorClosestThingReachable);
            return closestThing;
        }

        public static Thing ClosestThing_Global(IntVec3 center, IEnumerable searchSet, float maxDistance = 99999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null)
        {
            return GenClosestOnVehicle.ClosestThing_Global(center, searchSet, maxDistance, validator, priorityGetter, false);
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
                foreach (var target in list)
                {
                    Process(target);
                }
            }
            else if (searchSet is IList<Pawn> list2)
            {
                foreach (var target in list2)
                {
                    Process(target);
                }
            }
            else if (searchSet is IList<Building> list3)
            {
                foreach (var target in list3)
                {
                    Process(target);
                }
            }
            else if (searchSet is IList<IAttackTarget> list4)
            {
                foreach (var target in list4)
                {
                    Process((Thing)target);
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
                float num = (float)(center - t.PositionHeldOnBaseMap()).LengthHorizontalSquared;
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
                GenClosestOnVehicle.exitSpotResult = GenClosestOnVehicle.tmpExitSpot;
                GenClosestOnVehicle.enterSpotResult = GenClosestOnVehicle.tmpEnterSpot;
                chosen = t;
                closestDistSquared = distSquared;
                bestPrio = num;
            }
        }

        public static Thing ClosestThing_Global_Reachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null)
        {
            return GenClosestOnVehicle.ClosestThing_Global_Reachable(center, map, searchSet, peMode, traverseParams, maxDistance, validator, priorityGetter, false);
        }

        public static Thing ClosestThing_Global_Reachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null, bool canLookInHaulableSources = false)
        {
            return GenClosestOnVehicle.ClosestThing_Global_Reachable(center, map, searchSet, peMode, traverseParams, maxDistance, validator, priorityGetter, canLookInHaulableSources, out _, out _);
        }

        public static Thing ClosestThing_Global_Reachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, bool canLookInHaulableSources, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            GenClosestOnVehicle.tmpExitSpot = TargetInfo.Invalid;
            GenClosestOnVehicle.tmpEnterSpot = TargetInfo.Invalid;
            GenClosestOnVehicle.exitSpotResult = TargetInfo.Invalid;
            GenClosestOnVehicle.enterSpotResult = TargetInfo.Invalid;
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            if (searchSet == null)
            {
                return null;
            }

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
            exitSpot = GenClosestOnVehicle.exitSpotResult;
            enterSpot = GenClosestOnVehicle.enterSpotResult;
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
                float num = (float)(center - t.PositionHeldOnBaseMap()).LengthHorizontalSquared;
                if (num > maxDistanceSquared)
                {
                    return;
                }
                if (priorityGetter != null || num < closestDistSquared)
                {
                    ValidateThing(t, num);
                    IHaulSource haulSource;
                    if (canLookInHaulableSources && (haulSource = (t as IHaulSource)) != null)
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
                if (!ReachabilityUtilityOnVehicle.CanReach(map, center, t.SpawnedParentOrMe, peMode, traverseParams, t.MapHeld, out GenClosestOnVehicle.tmpExitSpot, out GenClosestOnVehicle.tmpEnterSpot))
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
                GenClosestOnVehicle.exitSpotResult = GenClosestOnVehicle.tmpExitSpot;
                GenClosestOnVehicle.enterSpotResult = GenClosestOnVehicle.tmpEnterSpot;
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
}