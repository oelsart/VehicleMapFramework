using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class FindBuildingUtility
    {
        public static Building_GibbetCage FindGibbetCageFor(Corpse c, Pawn traveler, bool ignoreOtherReservations, out LocalTargetInfo exitSpot, out LocalTargetInfo enterSpot)
        {
            if (FindBuildingUtility.cachedCages == null)
            {
                FindBuildingUtility.cachedCages = (from def in DefDatabase<ThingDef>.AllDefs
                                                   where def.IsGibbetCage
                                                   select def).ToList<ThingDef>();
            }
            foreach (ThingDef singleDef in FindBuildingUtility.cachedCages)
            {
                IntVec3 position = c.Position;
                Map map = c.Map;
                ThingRequest thingReq = ThingRequest.ForDef(singleDef);
                PathEndMode peMode = PathEndMode.InteractionCell;
                TraverseParms traverseParams = TraverseParms.For(traveler, Danger.Deadly, TraverseMode.ByPawn, false, false, false);
                float maxDistance = 9999f;
                Predicate<Thing> validator =(Thing x) => !((Building_GibbetCage)x).HasCorpse && ((Building_GibbetCage)x).Accepts(x) && traveler.CanReserve(x, 1, -1, null, ignoreOtherReservations);
                Building_GibbetCage building_GibbetCage = (Building_GibbetCage)GenClosestOnVehicle.ClosestThingReachable(position, map, thingReq, peMode, traverseParams, maxDistance, validator, null, 0, -1, false, RegionType.Set_Passable, false, false, out exitSpot, out enterSpot);
                if (building_GibbetCage != null)
                {
                    return building_GibbetCage;
                }
            }
            exitSpot = null;
            enterSpot = null;
            return null;
        }

        public static Building_CryptosleepCasket FindCryptosleepCasketFor(Pawn p, Pawn traveler, bool ignoreOtherReservations, out LocalTargetInfo exitSpot, out LocalTargetInfo enterSpot)
        {
            if (FindBuildingUtility.cachedCaskets == null)
            {
                FindBuildingUtility.cachedCaskets = (from def in DefDatabase<ThingDef>.AllDefs
                                                            where def.IsCryptosleepCasket
                                                            select def).ToList<ThingDef>();
            }
            foreach (ThingDef singleDef in FindBuildingUtility.cachedCaskets)
            {
                var queuing = KeyBindingDefOf.QueueOrder.IsDownEvent;
                Predicate<Thing> validator = (Thing t) =>
                {
                    var casket = (Building_Casket)t;
                    if (casket.HasAnyContents || (queuing && traveler.HasReserved(t))) return false;
                    return traveler.CanReserve(t, t.Map, 1, -1, null, ignoreOtherReservations);
                };
                Building_CryptosleepCasket building_CryptosleepCasket = (Building_CryptosleepCasket)GenClosestOnVehicle.ClosestThingReachable(p.PositionHeld, p.MapHeld, ThingRequest.ForDef(singleDef), PathEndMode.InteractionCell, TraverseParms.For(traveler, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, validator, null, 0, -1, false, RegionType.Set_Passable, false, false, out exitSpot, out enterSpot);
                if (building_CryptosleepCasket != null)
                {
                    return building_CryptosleepCasket;
                }
            }
            exitSpot = null;
            enterSpot = null;
            return null;
        }

        public static Building_MechCharger GetClosestCharger(Pawn mech, Pawn carrier, bool forced, out LocalTargetInfo exitSpot, out LocalTargetInfo enterSpot)
        {
            Danger danger = forced ? Danger.Deadly : Danger.Some;
            return (Building_MechCharger)GenClosestOnVehicle.ClosestThingReachable(mech.Position, mech.Map, ThingRequest.ForGroup(ThingRequestGroup.MechCharger), PathEndMode.InteractionCell, TraverseParms.For(carrier, danger, TraverseMode.ByPawn, false, false, false), 9999f, delegate (Thing t)
            {
                Building_MechCharger building_MechCharger = (Building_MechCharger)t;
                if (!carrier.CanReach(t, PathEndMode.InteractionCell, danger, false, false, TraverseMode.ByPawn, t.Map, out _, out _))
                {
                    return false;
                }
                if (carrier != mech)
                {
                    if (!forced && building_MechCharger.Map.reservationManager.ReservedBy(building_MechCharger, carrier, null))
                    {
                        return false;
                    }
                    if (forced && KeyBindingDefOf.QueueOrder.IsDownEvent && building_MechCharger.Map.reservationManager.ReservedBy(building_MechCharger, carrier, null))
                    {
                        return false;
                    }
                }
                return !t.IsForbidden(carrier) && carrier.CanReserve(t, t.Map, 1, -1, null, forced) && building_MechCharger.CanPawnChargeCurrently(mech);
            }, null, 0, -1, false, RegionType.Set_Passable, false, false, out exitSpot, out enterSpot);
        }

        public static bool TryFindExitPortal(Pawn pawn, Thing thing, out Thing portal, out LocalTargetInfo exitSpot2, out LocalTargetInfo enterSpot2)
        {
            portal = null;
            exitSpot2 = null;
            enterSpot2 = null;
            var baseMap = pawn.BaseMapOfThing();
            if (!baseMap.IsPocketMap)
            {
                return false;
            }
            IEnumerable<Thing> list = baseMap.listerThings.ThingsInGroup(ThingRequestGroup.MapPortal).Concat(VehiclePawnWithMapCache.allVehicles[baseMap].SelectMany(v => v.interiorMap.listerThings.ThingsInGroup(ThingRequestGroup.MapPortal)));
            if (list.NullOrEmpty<Thing>())
            {
                return false;
            }
            portal = list.MinBy((Thing x) => x.Position.DistanceToSquared(pawn.Position));
            ReachabilityUtilityOnVehicle.CanReach(thing.Map, thing.Position, portal, PathEndMode.ClosestTouch, TraverseParms.For(pawn), portal.Map, out exitSpot2, out enterSpot2);
            return true;
        }

        private static List<ThingDef> cachedCages = AccessTools.StaticFieldRefAccess<List<ThingDef>>(typeof(Building_GibbetCage), "cachedCages");

        private static List<ThingDef> cachedCaskets = AccessTools.StaticFieldRefAccess<List<ThingDef>>(typeof(Building_CryptosleepCasket), "cachedCaskets");
    }
}
