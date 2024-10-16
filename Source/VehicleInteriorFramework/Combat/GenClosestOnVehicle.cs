using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using RimWorld;
using UnityEngine;
using Vehicles;

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
            var baseMap = map.BaseMap();
            return thingReq.group == ThingRequestGroup.Nothing || ((thingReq.IsUndefined || baseMap.listerThings.ThingsMatching(thingReq).ConcatIfNotNull(VehiclePawnWithMapCache.allVehicles[baseMap].SelectMany((VehiclePawnWithInterior v) => v.interiorMap.listerThings.ThingsMatching(thingReq))).Count() == 0) && customGlobalSearchSet.EnumerableNullOrEmpty<Thing>());
        }

        public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, IEnumerable<Thing> customGlobalSearchSet = null, int searchRegionsMin = 0, int searchRegionsMax = -1, bool forceAllowGlobalSearch = false, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false)
        {
            return GenClosestOnVehicle.ClosestThingReachable(root, map, thingReq, peMode, traverseParams, maxDistance, validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax, forceAllowGlobalSearch, traversableRegionTypes, ignoreEntirelyForbiddenRegions, false);
        }

        public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, IEnumerable<Thing> customGlobalSearchSet = null, int searchRegionsMin = 0, int searchRegionsMax = -1, bool forceAllowGlobalSearch = false, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false, bool lookInHaulSources = false)
        {
            return GenClosestOnVehicle.ClosestThingReachable(root, map, thingReq, peMode, traverseParams, maxDistance, validator, customGlobalSearchSet, searchRegionsMin, searchRegionsMax, forceAllowGlobalSearch, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources, out _, out _);
        }

        public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, IEnumerable<Thing> customGlobalSearchSet, int searchRegionsMin, int searchRegionsMax, bool forceAllowGlobalSearch, RegionType traversableRegionTypes, bool ignoreEntirelyForbiddenRegions, bool lookInHaulSources, out LocalTargetInfo exitSpot, out LocalTargetInfo enterSpot)
        {
            GenClosestOnVehicle.tmpExitSpot = null;
            GenClosestOnVehicle.tmpEnterSpot = null;
            GenClosestOnVehicle.exitSpotResult = null;
            GenClosestOnVehicle.enterSpotResult = null;
            exitSpot = null;
            enterSpot = null;
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
            var basePos = map.IsVehicleMapOf(out var vehicle) ? root.OrigToVehicleMap(vehicle) : root;
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
                flag2 = (thing == null && num2 < num);
            }
            if (thing == null && flag && !flag2)
            {
                if (traversableRegionTypes != RegionType.Set_Passable)
                {
                    Log.ErrorOnce("ClosestThingReachable had to do a global search, but traversableRegionTypes is not set to passable only. It's not supported, because Reachability is based on passable regions only.", 14384767);
                }
                Predicate<Thing> validator2 = (Thing t) =>
                {
                    if(traverseParams.pawn.CanReach(t, peMode, traverseParams.maxDanger, true, true, traverseParams.mode, t.Map, out var exitSpot2, out var enterSpot2) && (validator == null || validator(t)))
                    {
                        GenClosestOnVehicle.tmpExitSpot = exitSpot2;
                        GenClosestOnVehicle.tmpEnterSpot = enterSpot2;
                        return true;
                    }
                    return false;
                };
                thing = GenClosestOnVehicle.ClosestThing_Global(basePos, customGlobalSearchSet ?? baseMap.listerThings.ThingsMatching(thingReq).ConcatIfNotNull(VehiclePawnWithMapCache.allVehicles[baseMap].SelectMany((VehiclePawnWithInterior v) => v.interiorMap.listerThings.ThingsMatching(thingReq))), maxDistance, validator2, null);
            }
            exitSpot = GenClosestOnVehicle.exitSpotResult;
            enterSpot = GenClosestOnVehicle.enterSpotResult;
            return thing;
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
            var finder = new Finder
            {
                center = center,
                priorityGetter = priorityGetter,
                lookInHaulSources = lookInHaulSources,
                validator = validator,
                closestDistSquared = 2.1474836E+09f,
                chosen = null,
                bestPrio = float.MinValue,
                maxDistanceSquared = maxDistance * maxDistance
            };
            if (searchSet is IList<Thing> list)
            {
                foreach (var target in list)
                {
                    GenClosestOnVehicle.Process(target, finder);
                }
            }
            else if (searchSet is IList<Pawn> list2)
            {
                foreach (var target in list2)
                {
                    GenClosestOnVehicle.Process(target, finder);
                }
            }
            else if (searchSet is IList<Building> list3)
            {
                foreach (var target in list3)
                {
                    GenClosestOnVehicle.Process(target, finder);
                }
            }
            else if (searchSet is IList<IAttackTarget> list4)
            {
                foreach (var target in list4)
                {
                    GenClosestOnVehicle.Process((Thing)target, finder);
                }
            }
            else
            {
                foreach (var target in searchSet)
                {
                    GenClosestOnVehicle.Process((Thing)target, finder);
                }
            }
            return finder.chosen;
        }

        private static void Process(Thing t, Finder finder)
        {
            if (!t.Spawned && !HaulAIUtility.IsInHaulableInventory(t))
			{
				return;
            }
            float num = (float)(finder.center - t.PositionHeldOnBaseMap()).LengthHorizontalSquared;
			if (num > finder.maxDistanceSquared)
			{
				return;
            }
            if (finder.priorityGetter != null || num < finder.closestDistSquared)
            {
                GenClosestOnVehicle.ValidateThing(t, num, finder);
				if (finder.lookInHaulSources)
                {
                    if (t is IHaulSource haulSource)
                    {
                        ThingOwner directlyHeldThings = haulSource.GetDirectlyHeldThings();
                        for (int i = 0; i < directlyHeldThings.Count; i++)
                        {
                            GenClosestOnVehicle.ValidateThing(directlyHeldThings[i], num, finder);
                        }
                    }
                }
			}
		}

        private static void ValidateThing(Thing t, float distSquared, Finder finder)
		{
			if (finder.validator != null && !finder.validator(t))
			{
				return;
            }
            float num = 0f;
			if (finder.priorityGetter != null)
            {
                num = finder.priorityGetter(t);
				if (num< finder.bestPrio)
				{
					return;
                }
                if (Mathf.Approximately(num, finder.bestPrio) && distSquared >= finder.closestDistSquared)
				{
					return;
				}
			}
            GenClosestOnVehicle.exitSpotResult = GenClosestOnVehicle.tmpExitSpot;
            GenClosestOnVehicle.enterSpotResult = GenClosestOnVehicle.tmpEnterSpot;
            finder.chosen = t;
            finder.closestDistSquared = distSquared;
            finder.bestPrio = num;
		}

        public static Thing ClosestThing_Global_Reachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null)
        {
            return GenClosestOnVehicle.ClosestThing_Global_Reachable(center, map, searchSet, peMode, traverseParams, maxDistance, validator, priorityGetter, false);
        }

        public static Thing ClosestThing_Global_Reachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, Func<Thing, float> priorityGetter = null, bool canLookInHaulableSources = false)
        {
            return GenClosestOnVehicle.ClosestThing_Global_Reachable(center, map, searchSet, peMode, traverseParams, maxDistance, validator, priorityGetter, canLookInHaulableSources, out _, out _);
        }

        public static Thing ClosestThing_Global_Reachable(IntVec3 center, Map map, IEnumerable<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> validator, Func<Thing, float> priorityGetter, bool canLookInHaulableSources, out LocalTargetInfo exitSpot, out LocalTargetInfo enterSpot)
        {
            GenClosestOnVehicle.tmpExitSpot = null;
            GenClosestOnVehicle.tmpEnterSpot = null;
            GenClosestOnVehicle.exitSpotResult = null;
            GenClosestOnVehicle.enterSpotResult = null;
            exitSpot = null;
            enterSpot = null;
            if (searchSet == null)
            {
                return null;
            }
            var finder = new FinderReachable
            {
                center = center,
                priorityGetter = priorityGetter,
                canLookInHaulableSources = canLookInHaulableSources,
                map = map,
                peMode = peMode,
                traverseParms = traverseParams,
                validator = validator,
                debug_changeCount = 0,
                debug_scanCount = 0,
                bestThing = null,
                bestPrio = float.MinValue,
                maxDistanceSquared = maxDistance * maxDistance,
                closestDistSquared = 2.1474836E+09f
            };
            IList<Thing> list;
            IList<Pawn> list2;
            IList<Building> list3;
            if ((list = (searchSet as IList<Thing>)) != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    GenClosestOnVehicle.Process(list[i], ref finder);
                }
            }
            else if ((list2 = (searchSet as IList<Pawn>)) != null)
            {
                for (int j = 0; j < list2.Count; j++)
                {
                    GenClosestOnVehicle.Process(list2[j], ref finder);
                }
            }
            else if ((list3 = (searchSet as IList<Building>)) != null)
            {
                for (int k = 0; k < list3.Count; k++)
                {
                    GenClosestOnVehicle.Process(list3[k], ref finder);
                }
            }
            else
            {
                foreach (Thing t in searchSet)
                {
                    GenClosestOnVehicle.Process(t, ref finder);
                }
            }
            exitSpot = GenClosestOnVehicle.exitSpotResult;
            enterSpot = GenClosestOnVehicle.enterSpotResult;
            return finder.bestThing;
        }

        private static void Process(Thing t, ref FinderReachable finder)
        {
            if (t == null)
            {
                return;
            }
            if (!t.Spawned)
            {
                return;
            }
            int debug_scanCount = finder.debug_scanCount;
            finder.debug_scanCount = debug_scanCount + 1;
            float num = (float)(finder.center - t.PositionHeldOnBaseMap()).LengthHorizontalSquared;
            if (num > finder.maxDistanceSquared)
            {
                return;
            }
            if (finder.priorityGetter != null || num < finder.closestDistSquared)
            {
                GenClosestOnVehicle.ValidateThing(t, num, ref finder);
                IHaulSource haulSource;
                if (finder.canLookInHaulableSources && (haulSource = (t as IHaulSource)) != null)
                {
                    ThingOwner directlyHeldThings = haulSource.GetDirectlyHeldThings();
                    for (int i = 0; i < directlyHeldThings.Count; i++)
                    {
                        GenClosestOnVehicle.ValidateThing(directlyHeldThings[i], num, ref finder);
                    }
                }
            }
        }

        private static void ValidateThing(Thing t, float distSquared, ref FinderReachable finder)
		{
			if (!ReachabilityUtilityOnVehicle.CanReach(finder.map, finder.center, t.SpawnedParentOrMe, finder.peMode, finder.traverseParms, t.MapHeld, out GenClosestOnVehicle.tmpExitSpot, out GenClosestOnVehicle.tmpEnterSpot))
			{
				return;
			}
			if (finder.validator != null && !finder.validator(t))
			{
				return;
			}
			float num = 0f;
			if (finder.priorityGetter != null)
			{
				num = finder.priorityGetter(t);
				if (num< finder.bestPrio)
				{
					return;
				}
				if (Mathf.Approximately(num, finder.bestPrio) && distSquared >= finder.closestDistSquared)
				{
					return;
				}
            }
            GenClosestOnVehicle.exitSpotResult = GenClosestOnVehicle.tmpExitSpot;
            GenClosestOnVehicle.enterSpotResult = GenClosestOnVehicle.tmpEnterSpot;
            finder.bestThing = t;
            finder.closestDistSquared = distSquared;
            finder.bestPrio = num;
            int debug_changeCount = finder.debug_changeCount;
            finder.debug_changeCount = debug_changeCount + 1;
		}

        private class Finder
        {
            public IntVec3 center;

            public Func<Thing, float> priorityGetter;

            public bool lookInHaulSources;

            public Predicate<Thing> validator;

            public float closestDistSquared;

            public Thing chosen;

            public float bestPrio;

            public float maxDistanceSquared;
        }

        private class FinderReachable
        {
            public IntVec3 center;

            public Func<Thing, float> priorityGetter;

            public bool canLookInHaulableSources;

            public Map map;

            public PathEndMode peMode;

            public TraverseParms traverseParms;

            public Predicate<Thing> validator;

            public int debug_changeCount;

            public int debug_scanCount;

            public Thing bestThing;

            public float bestPrio;

            public float maxDistanceSquared;

            public float closestDistSquared;
        }

        private static LocalTargetInfo tmpExitSpot;

        private static LocalTargetInfo tmpEnterSpot;

        private static LocalTargetInfo exitSpotResult;

        private static LocalTargetInfo enterSpotResult;
    }
}