using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VehicleInteriors.Jobs.WorkGivers;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public abstract class WorkGiver_ConstructDeliverResourcesAcrossMaps : WorkGiver_Scanner, IWorkGiverAcrossMaps
    {
        public bool NeedVirtualMapTransfer => false;

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Deadly;
        }

        public static void ResetStaticData()
        {
            WorkGiver_ConstructDeliverResourcesAcrossMaps.ForbiddenLowerTranslated = "ForbiddenLower".Translate();
            WorkGiver_ConstructDeliverResourcesAcrossMaps.NoPathTranslated = "NoPath".Translate();
        }

        private static bool ResourceValidator(Pawn pawn, ThingDefCountClass need, Thing th)
        {
            return th.def == need.thingDef && !th.IsForbidden(pawn) && pawn.CanReserve(th, th.Map, 1, -1, null, false);
        }

        private bool CanUseCarriedResource(Pawn pawn, IConstructible c, ThingDefCountClass need)
        {
            Thing carriedThing = pawn.carryTracker.CarriedThing;
            if (carriedThing?.def != need.thingDef)
            {
                return false;
            }
            if (!KeyBindingDefOf.QueueOrder.IsDownEvent)
            {
                return true;
            }

            bool IsValidJob(Job job)
		    {
			    return job.def != VMF_DefOf.VMF_HaulToContainerAcrossMaps || job.targetA != carriedThing;
		    }
            if (pawn.CurJob != null && !IsValidJob(pawn.CurJob))
			{
                return false;
            }
            foreach(var job in pawn.jobs.jobQueue)
			{
                if (!IsValidJob(job.job))
                {
                    return false;
                }
            }
            return true;
        }

        protected Job ResourceDeliverJobFor(Pawn pawn, IConstructible c, bool canRemoveExistingFloorUnderNearbyNeeders = true, bool forced = false)
        {
            if (c is Blueprint_Install install)
            {
                return this.InstallJob(pawn, install);
            }
            WorkGiver_ConstructDeliverResourcesAcrossMaps.missingResources.Clear();
            foreach(var need in c.TotalMaterialCost())
            {
                int num;
                if (!forced && c is IHaulEnroute enroute)
                {
                    num = enroute.GetSpaceRemainingWithEnroute(need.thingDef, pawn);
                }
                else
                {
                    num = c.ThingCountNeeded(need.thingDef);
                }
                if (num > 0)
                {
                    if (!pawn.BaseMap().itemAvailability.ThingsAvailableAnywhere(need.thingDef, num, pawn))
                    {
                        WorkGiver_ConstructDeliverResourcesAcrossMaps.missingResources.Add(need.thingDef, num);
                        if (FloatMenuMakerMap.makingFor != pawn)
                        {
                            break;
                        }
                    }
                    else
                    {
                        Thing foundRes;
                        var exitSpot = TargetInfo.Invalid;
                        var enterSpot = TargetInfo.Invalid;
                        if (this.CanUseCarriedResource(pawn, c, need))
                        {
                            foundRes = pawn.carryTracker.CarriedThing;
                        }
                        else
                        {
                            foundRes = GenClosestOnVehicle.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(need.thingDef), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, (Thing r) => WorkGiver_ConstructDeliverResourcesAcrossMaps.ResourceValidator(pawn, need, r), null, 0, -1, true, RegionType.Set_Passable, false, false, out exitSpot, out enterSpot);
                        }
                        if (foundRes == null)
                        {
                            WorkGiver_ConstructDeliverResourcesAcrossMaps.missingResources.Add(need.thingDef, num);
                            if (FloatMenuMakerMap.makingFor != pawn)
                            {
                                break;
                            }
                        }
                        else
                        {
                            this.FindAvailableNearbyResources(foundRes, pawn, out int num2);
                            HashSet<Thing> hashSet = this.FindNearbyNeeders(pawn, need.thingDef, c, num, num2, canRemoveExistingFloorUnderNearbyNeeders, out int num3, out Job job);
                            if (job != null)
                            {
                                return job;
                            }
                            hashSet.Add((Thing)c);
                            Thing thing;
                            if (hashSet.Count > 0)
                            {
                                thing = hashSet.MinBy((Thing needer) => IntVec3Utility.ManhattanDistanceFlat(foundRes.Position, needer.Position));
                                hashSet.Remove(thing);
                            }
                            else
                            {
                                thing = (Thing)c;
                            }
                            int num4 = 0;
                            int i = 0;
                            do
                            {
                                num4 += WorkGiver_ConstructDeliverResourcesAcrossMaps.resourcesAvailable[i].stackCount;
                                num4 = Mathf.Min(num4, Mathf.Min(num2, num3));
                                i++;
                            }
                            while (num4 < num3 && num4 < num2 && i < WorkGiver_ConstructDeliverResourcesAcrossMaps.resourcesAvailable.Count);
                            WorkGiver_ConstructDeliverResourcesAcrossMaps.resourcesAvailable.RemoveRange(i, WorkGiver_ConstructDeliverResourcesAcrossMaps.resourcesAvailable.Count - i);
                            WorkGiver_ConstructDeliverResourcesAcrossMaps.resourcesAvailable.Remove(foundRes);
                            if (!ReachabilityUtilityOnVehicle.CanReach(foundRes.Map, foundRes.Position, thing, this.PathEndMode, TraverseParms.For(TraverseMode.PassAllDestroyableThings, this.MaxPathDanger(pawn)), thing.Map, out var exitSpot2, out var enterSpot2))
                            {
                                return null;
                            }
                            Job job2 = JobMaker.MakeJob(VMF_DefOf.VMF_HaulToContainerAcrossMaps);
                            var driver = job2.GetCachedDriver(pawn) as JobDriverAcrossMaps;
                            driver.SetSpots(exitSpot, enterSpot, exitSpot2, enterSpot2);
                            job2.targetA = foundRes;
                            job2.targetQueueA = new List<LocalTargetInfo>();
                            for (i = 0; i < WorkGiver_ConstructDeliverResourcesAcrossMaps.resourcesAvailable.Count; i++)
                            {
                                job2.targetQueueA.Add(WorkGiver_ConstructDeliverResourcesAcrossMaps.resourcesAvailable[i]);
                            }
                            job2.targetC = (Thing)c;
                            job2.targetB = thing;
                            if (hashSet.Count > 0)
                            {
                                job2.targetQueueB = new List<LocalTargetInfo>();
                                foreach (Thing t in hashSet)
                                {
                                    job2.targetQueueB.Add(t);
                                }
                            }
                            job2.count = num4;
                            job2.haulMode = HaulMode.ToContainer;
                            return job2;
                        }
                    }
                }
            }
            if (WorkGiver_ConstructDeliverResourcesAcrossMaps.missingResources.Count > 0 && FloatMenuMakerMap.makingFor == pawn)
            {
                JobFailReason.Is("MissingMaterials".Translate((from kvp in WorkGiver_ConstructDeliverResourcesAcrossMaps.missingResources
                                                               select string.Format("{0}x {1}", kvp.Value, kvp.Key.label)).ToCommaList(false, false)), null);
            }
            return null;
        }

        private void FindAvailableNearbyResources(Thing firstFoundResource, Pawn pawn, out int resTotalAvailable)
        {
            int num = pawn.carryTracker.MaxStackSpaceEver(firstFoundResource.def);
            resTotalAvailable = 0;
            WorkGiver_ConstructDeliverResourcesAcrossMaps.resourcesAvailable.Clear();
            WorkGiver_ConstructDeliverResourcesAcrossMaps.resourcesAvailable.Add(firstFoundResource);
            resTotalAvailable += firstFoundResource.stackCount;
            if (resTotalAvailable < num)
            {
                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(firstFoundResource.PositionHeld, firstFoundResource.MapHeld, MultiPickupRadius, false))
                {
                    if (resTotalAvailable >= num)
                    {
                        resTotalAvailable = num;
                        break;
                    }
                    if (thing.def == firstFoundResource.def && GenAI.CanUseItemForWork(pawn, thing))
                    {
                        WorkGiver_ConstructDeliverResourcesAcrossMaps.resourcesAvailable.Add(thing);
                        resTotalAvailable += thing.stackCount;
                    }
                }
            }
            resTotalAvailable = Mathf.Min(resTotalAvailable, num);
        }

        private HashSet<Thing> FindNearbyNeeders(Pawn pawn, ThingDef stuff, IConstructible c, int resNeeded, int resTotalAvailable, bool canRemoveExistingFloorUnderNearbyNeeders, out int neededTotal, out Job jobToMakeNeederAvailable)
        {
            neededTotal = resNeeded;
            HashSet<Thing> hashSet = new HashSet<Thing>();
            Thing thing = (Thing)c;
            foreach (Thing thing2 in GenRadial.RadialDistinctThingsAround(thing.Position, thing.Map, NearbyConstructScanRadius, true))
            {
                if (neededTotal >= resTotalAvailable)
                {
                    break;
                }
                if (this.IsNewValidNearbyNeeder(thing2, hashSet, c, pawn) && (!(thing2 is Blueprint blue) || !WorkGiver_ConstructDeliverResourcesAcrossMaps.ShouldRemoveExistingFloorFirst(pawn, blue)))
                {
                    int num = 0;
                    if (thing2 is IHaulEnroute enroute)
                    {
                        num = enroute.GetSpaceRemainingWithEnroute(stuff, pawn);
                    }
                    else if (thing2 is IConstructible constructible)
                    {
                        num = constructible.ThingCountNeeded(stuff);
                    }
                    if (num > 0)
                    {
                        hashSet.Add(thing2);
                        neededTotal += num;
                    }
                }
            }
            if (c is Blueprint blueprint && blueprint.def.entityDefToBuild is TerrainDef && canRemoveExistingFloorUnderNearbyNeeders && neededTotal < resTotalAvailable)
            {
                foreach (Thing thing3 in GenRadial.RadialDistinctThingsAround(thing.Position, thing.Map, 3f, false))
                {
                    if (this.IsNewValidNearbyNeeder(thing3, hashSet, c, pawn) && thing3 is Blueprint blue2)
                    {
                        Job job = this.RemoveExistingFloorJob(pawn, blue2);
                        if (job != null)
                        {
                            jobToMakeNeederAvailable = job;
                            return hashSet;
                        }
                    }
                }
            }
            jobToMakeNeederAvailable = null;
            return hashSet;
        }

        private bool IsNewValidNearbyNeeder(Thing t, HashSet<Thing> nearbyNeeders, IConstructible constructible, Pawn pawn)
        {
            return t is IConstructible && t != constructible && t.Faction == pawn.Faction && t.Isnt<Blueprint_Install>() && !nearbyNeeders.Contains(t) && !t.IsForbidden(pawn) && GenConstruct.CanConstruct(t, pawn, false, false, JobDefOf.HaulToContainer);
        }

        protected static bool ShouldRemoveExistingFloorFirst(Pawn pawn, Blueprint blue)
        {
            return blue.def.entityDefToBuild is TerrainDef && blue.Map.terrainGrid.CanRemoveTopLayerAt(blue.Position);
        }

        protected Job RemoveExistingFloorJob(Pawn pawn, Blueprint blue)
        {
            if (!WorkGiver_ConstructDeliverResourcesAcrossMaps.ShouldRemoveExistingFloorFirst(pawn, blue))
            {
                return null;
            }
            if (!pawn.CanReserve(blue.Position, blue.Map, 1, -1, ReservationLayerDefOf.Floor, false))
            {
                return null;
            }
            if (pawn.WorkTypeIsDisabled(WorkGiverDefOf.ConstructRemoveFloors.workType))
            {
                return null;
            }
            Job job = JobMaker.MakeJob(JobDefOf.RemoveFloor, blue.Position);
            job.ignoreDesignations = true;
            return job;
        }

        private Job InstallJob(Pawn pawn, Blueprint_Install install)
        {
            Thing miniToInstallOrBuildingToReinstall = install.MiniToInstallOrBuildingToReinstall;
            IThingHolder parentHolder = miniToInstallOrBuildingToReinstall.ParentHolder;
            if (parentHolder != null && parentHolder is Pawn_CarryTracker pawn_CarryTracker)
            {
                JobFailReason.Is("BeingCarriedBy".Translate(pawn_CarryTracker.pawn), null);
                return null;
            }
            if (miniToInstallOrBuildingToReinstall.IsForbidden(pawn))
            {
                JobFailReason.Is(WorkGiver_ConstructDeliverResourcesAcrossMaps.ForbiddenLowerTranslated, null);
                return null;
            }
            if (!pawn.CanReach(miniToInstallOrBuildingToReinstall, PathEndMode.ClosestTouch, pawn.NormalMaxDanger(), false, false, TraverseMode.ByPawn, install.Map, out var exitSpot, out var enterSpot))
            {
                JobFailReason.Is(WorkGiver_ConstructDeliverResourcesAcrossMaps.NoPathTranslated, null);
                return null;
            }
            if (!pawn.CanReserve(miniToInstallOrBuildingToReinstall, install.Map, 1, -1, null, false))
            {
                Pawn pawn2 = install.Map.reservationManager.FirstRespectedReserver(miniToInstallOrBuildingToReinstall, pawn, null);
                if (pawn2 != null)
                {
                    JobFailReason.Is("ReservedBy".Translate(pawn2.LabelShort, pawn2), null);
                }
                return null;
            }
            var traverseParms = TraverseParms.For(TraverseMode.ByPawn, this.MaxPathDanger(pawn));
            if (!ReachabilityUtilityOnVehicle.CanReach(miniToInstallOrBuildingToReinstall.MapHeld, miniToInstallOrBuildingToReinstall.PositionHeld, install, this.PathEndMode, traverseParms, install.Map, out var exitSpot2, out var enterSpot2))
            {
                return null;
            }
            Job job = JobMaker.MakeJob(VMF_DefOf.VMF_HaulToContainerAcrossMaps, miniToInstallOrBuildingToReinstall, install);
            job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2);
            job.count = 1;
            job.haulMode = HaulMode.ToContainer;
            return job;
        }

		private static readonly List<Thing> resourcesAvailable = new List<Thing>();

        private static readonly Dictionary<ThingDef, int> missingResources = new Dictionary<ThingDef, int>();

        private const float MultiPickupRadius = 5f;

        private const float NearbyConstructScanRadius = 8f;

        protected static string ForbiddenLowerTranslated;

        protected static string NoPathTranslated;
    }
}
