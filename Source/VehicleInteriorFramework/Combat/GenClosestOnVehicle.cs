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
        private static bool EarlyOutSearch(IntVec3 start, Map map, ThingRequest thingReq, IEnumerable<Thing> searchSet, IEnumerable<Thing> customGlobalSearchSet, Predicate<Thing> validator)
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
            return thingReq.group == ThingRequestGroup.Nothing || ((thingReq.IsUndefined || searchSet.Count() == 0 && customGlobalSearchSet.EnumerableNullOrEmpty<Thing>()));
        }

        public static Thing ClosestThingReachable(IntVec3 root, Map map, ThingRequest thingReq, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null, IEnumerable<Thing> customGlobalSearchSet = null, int searchRegionsMin = 0, int searchRegionsMax = -1, bool forceAllowGlobalSearch = false, RegionType traversableRegionTypes = RegionType.Set_Passable, bool ignoreEntirelyForbiddenRegions = false, bool lookInHaulSources = false)
        {
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

            var baseMap = map.Parent is MapParent_Vehicle parentVehicle ? parentVehicle.vehicle.Map : map;
            var basePos = traverseParams.pawn.PositionOnBaseMap();
            var searchSet = baseMap.listerThings.ThingsMatching(thingReq).ConcatIfNotNull(baseMap.listerThings.GetThingsOfType<VehiclePawnWithInterior>().SelectMany((VehiclePawnWithInterior v) => v.interiorMap.listerThings.ThingsMatching(thingReq)));
            if (GenClosestOnVehicle.EarlyOutSearch(root, map, thingReq, searchSet, customGlobalSearchSet, validator))
            {
                return null;
            }
            Thing thing = null;
            if (!thingReq.IsUndefined && thingReq.CanBeFoundInRegion)
            {
                int num = (searchRegionsMax > 0) ? searchRegionsMax : 30;
                thing = GenClosest.RegionwiseBFSWorker_NewTemp(root, map, thingReq, peMode, traverseParams, validator, null, searchRegionsMin, num, maxDistance, out int num2, traversableRegionTypes, ignoreEntirelyForbiddenRegions, lookInHaulSources);
            }
            if (thing == null && flag)
            {
                if (traversableRegionTypes != RegionType.Set_Passable)
                {
                    Log.ErrorOnce("ClosestThingReachable had to do a global search, but traversableRegionTypes is not set to passable only. It's not supported, because Reachability is based on passable regions only.", 14384767);
                }
                Predicate<Thing> validator2 = (Thing t) =>
                {
                    return traverseParams.pawn.CanReach(t, peMode, traverseParams.maxDanger, true, true, traverseParams.mode, t.Map, out _, out _) && (validator == null || validator(t));
                };
                thing = GenClosestOnVehicle.ClosestThing_Global(basePos, customGlobalSearchSet ?? searchSet, maxDistance, validator2, null);
            }
            return thing;
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
            finder.chosen = t;
            finder.closestDistSquared = distSquared;
            finder.bestPrio = num;
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
    }
}
