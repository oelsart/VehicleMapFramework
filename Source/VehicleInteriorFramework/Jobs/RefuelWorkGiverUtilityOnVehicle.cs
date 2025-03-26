using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class RefuelWorkGiverUtilityOnVehicle
    {
        public static bool CanRefuel(Pawn pawn, Thing t, bool forced = false)
        {
            CompRefuelable compRefuelable = t.TryGetComp<CompRefuelable>();
            if (compRefuelable == null || compRefuelable.IsFull || (!forced && !compRefuelable.allowAutoRefuel))
            {
                return false;
            }

            if (compRefuelable.FuelPercentOfMax > 0f && !compRefuelable.Props.allowRefuelIfNotEmpty)
            {
                return false;
            }

            if (!forced && !compRefuelable.ShouldAutoRefuelNow)
            {
                return false;
            }

            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, t.Map, 1, -1, null, forced))
            {
                return false;
            }

            if (t.Faction != pawn.Faction)
            {
                return false;
            }

            CompInteractable compInteractable = t.TryGetComp<CompInteractable>();
            if (compInteractable != null && compInteractable.Props.cooldownPreventsRefuel && compInteractable.OnCooldown)
            {
                JobFailReason.Is(compInteractable.Props.onCooldownString.CapitalizeFirst());
                return false;
            }

            if (FindBestFuel(pawn, t, out _, out _) == null)
            {
                ThingFilter fuelFilter = t.TryGetComp<CompRefuelable>().Props.fuelFilter;
                JobFailReason.Is("NoFuelToRefuel".Translate(fuelFilter.Summary));
                return false;
            }

            if (t.TryGetComp<CompRefuelable>().Props.atomicFueling && FindAllFuel(pawn, t, out _, out _) == null)
            {
                ThingFilter fuelFilter2 = t.TryGetComp<CompRefuelable>().Props.fuelFilter;
                JobFailReason.Is("NoFuelToRefuel".Translate(fuelFilter2.Summary));
                return false;
            }

            return true;
        }

        public static Job RefuelJob(Pawn pawn, Thing t, bool forced = false, JobDef customRefuelJob = null, JobDef customAtomicRefuelJob = null)
        {
            if (!t.TryGetComp<CompRefuelable>().Props.atomicFueling)
            {
                Thing thing = FindBestFuel(pawn, t, out var exitSpot, out var enterSpot);
                if (ReachabilityUtilityOnVehicle.CanReach(thing.Map, thing.Position, t, PathEndMode.ClosestTouch, TraverseParms.For(pawn), t.Map, out var exitSpot2, out var enterSpot2))
                {
                    return JobMaker.MakeJob(customRefuelJob ?? VMF_DefOf.VMF_RefuelAcrossMaps, t, thing).SetSpotsToJobAcrossMaps(pawn, exitSpot2, enterSpot2, exitSpot, enterSpot);
                }
            }

            List<Thing> source = FindAllFuel(pawn, t, out var exitSpot3, out var enterSpot3);
            if (!source.NullOrEmpty() && ReachabilityUtilityOnVehicle.CanReach(source.First().Map, source.First().Position, t, PathEndMode.ClosestTouch, TraverseParms.For(pawn), t.Map, out var exitSpot4, out var enterSpot4))
            {
                var job = JobMaker.MakeJob(customAtomicRefuelJob ?? VMF_DefOf.VMF_RefuelAtomicAcrossMaps, t).SetSpotsToJobAcrossMaps(pawn, exitSpot4, enterSpot4, exitSpot3, enterSpot3);
                job.targetQueueB = source.Select((Thing f) => new LocalTargetInfo(f)).ToList();
                return job;
            }
            return null;
        }

        private static Thing FindBestFuel(Pawn pawn, Thing refuelable, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            ThingFilter filter = refuelable.TryGetComp<CompRefuelable>().Props.fuelFilter;
            bool validator(Thing x)
            {
                if (x.IsForbidden(pawn) || !pawn.CanReserve(x, x.Map))
                {
                    return false;
                }

                return filter.Allows(x);
            }
            return GenClosestOnVehicle.ClosestThingReachable(pawn.Position, pawn.Map, filter.BestThingRequest, PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, (Predicate<Thing>)validator, null, 0, -1, false, RegionType.Set_Passable, false, false, out exitSpot, out enterSpot);
        }

        private static List<Thing> FindAllFuel(Pawn pawn, Thing refuelable, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            int fuelCountToFullyRefuel = refuelable.TryGetComp<CompRefuelable>().GetFuelCountToFullyRefuel();
            ThingFilter filter = refuelable.TryGetComp<CompRefuelable>().Props.fuelFilter;
            return FindEnoughReservableThings(pawn, refuelable.Position, new IntRange(fuelCountToFullyRefuel, fuelCountToFullyRefuel), (Thing t) => filter.Allows(t), filter.BestThingRequest, out exitSpot, out enterSpot);
        }

        public static List<Thing> FindEnoughReservableThings(Pawn pawn, IntVec3 rootCell, IntRange desiredQuantity, Predicate<Thing> validThing, ThingRequest request, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            bool validator(Thing x)
            {
                if (x.IsForbidden(pawn) || !pawn.CanReserve(x, x.Map))
                {
                    return false;
                }

                return validThing(x);
            }
            var firstThing = GenClosestOnVehicle.ClosestThingReachable(pawn.Position, pawn.Map, request, PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, validator, null, 0, -1, false, RegionType.Set_Passable, false, false, out exitSpot, out enterSpot);

            Region region2 = firstThing.Position.GetRegion(firstThing.Map);
            TraverseParms traverseParams = TraverseParms.For(pawn);
            bool entryCondition(Region from, Region r) => r.Allows(traverseParams, isDestination: false);
            List<Thing> chosenThings = new List<Thing>();
            int accumulatedQuantity = 0;
            ThingListProcessor(firstThing.Position.GetThingList(region2.Map), region2);
            if (accumulatedQuantity < desiredQuantity.max)
            {
                RegionTraverser.BreadthFirstTraverse(region2, entryCondition, RegionProcessor, 99999);
            }

            if (accumulatedQuantity >= desiredQuantity.min)
            {
                return chosenThings;
            }

            return null;
            bool RegionProcessor(Region r)
            {
                List<Thing> things2 = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));
                return ThingListProcessor(things2, r);
            }

            bool ThingListProcessor(List<Thing> things, Region region)
            {
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (validator(thing) && !chosenThings.Contains(thing) && ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, region, PathEndMode.ClosestTouch, pawn))
                    {
                        chosenThings.Add(thing);
                        accumulatedQuantity += thing.stackCount;
                        if (accumulatedQuantity >= desiredQuantity.max)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
    }
}
