using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class RestUtilityOnVehicle
    {
        public static Building_Bed FindBedFor(Pawn sleeper, Pawn traveler, bool checkSocialProperness, bool ignoreOtherReservations, GuestStatus? guestStatus, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            if (sleeper.RaceProps.IsMechanoid)
			{
                return null;
            }
            if (ModsConfig.BiotechActive && sleeper.Deathresting)
			{
                Building_Bed assignedDeathrestCasket = sleeper.ownership.AssignedDeathrestCasket;
                if (assignedDeathrestCasket != null && RestUtility.IsValidBedFor(assignedDeathrestCasket, sleeper, traveler, true, false, false, null))
                {
                    CompDeathrestBindable compDeathrestBindable = assignedDeathrestCasket.TryGetComp<CompDeathrestBindable>();
                    if (compDeathrestBindable != null && (compDeathrestBindable.BoundPawn == sleeper || compDeathrestBindable.BoundPawn == null))
					{
                        return assignedDeathrestCasket;
                    }
                }
            }
            bool flag = false;
            if (sleeper.Ideo != null)
			{
                foreach(var precept in sleeper.Ideo.PreceptsListForReading)
				{
                    if (precept.def.prefersSlabBed)
                    {
                        flag = true;
                        break;
                    }
                }
            }
            List<ThingDef> list = flag ? RestUtilityOnVehicle.bedDefsBestToWorst_SlabBed_Medical : RestUtilityOnVehicle.bedDefsBestToWorst_Medical;
            List<ThingDef> list2 = flag ? RestUtilityOnVehicle.bedDefsBestToWorst_SlabBed_RestEffectiveness : RestUtilityOnVehicle.bedDefsBestToWorst_RestEffectiveness;
            if (HealthAIUtility.ShouldSeekMedicalRest(sleeper))
            {
                if (sleeper.InBed() && sleeper.CurrentBed().Medical && RestUtility.IsValidBedFor(sleeper.CurrentBed(), sleeper, traveler, checkSocialProperness, false, ignoreOtherReservations, guestStatus))
				{
                    return sleeper.CurrentBed();
                }
                for (int i = 0; i < list.Count; i++)
                {
                    ThingDef thingDef = list[i];
                    if (RestUtility.CanUseBedEver(sleeper, thingDef))
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            Danger maxDanger = (j == 0) ? Danger.None : Danger.Deadly;
                            Building_Bed building_Bed = (Building_Bed)GenClosestOnVehicle.ClosestThingReachable(sleeper.Position, sleeper.MapHeld, ThingRequest.ForDef(thingDef), PathEndMode.OnCell, TraverseParms.For(traveler, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, (Thing b) => ((Building_Bed)b).Medical && b.Position.GetDangerFor(sleeper, sleeper.Map) <= maxDanger && RestUtility.IsValidBedFor(b, sleeper, traveler, checkSocialProperness, false, ignoreOtherReservations, guestStatus), null, 0, -1, false, RegionType.Set_Passable, false, false, out exitSpot, out enterSpot);
                            if (building_Bed != null)
                            {
                                return building_Bed;
                            }
                        }
                    }
                }
            }
            if (sleeper.RaceProps.Dryad)
			{
                return null;
            }
            if (sleeper.ownership != null && sleeper.ownership.OwnedBed != null && RestUtility.IsValidBedFor(sleeper.ownership.OwnedBed, sleeper, traveler, checkSocialProperness, false, ignoreOtherReservations, guestStatus))
			{
                return sleeper.ownership.OwnedBed;
            }
            DirectPawnRelation directPawnRelation = LovePartnerRelationUtility.ExistingMostLikedLovePartnerRel(sleeper, false);
            if (directPawnRelation != null)
            {
                Building_Bed ownedBed = directPawnRelation.otherPawn.ownership.OwnedBed;
                if (ownedBed != null && RestUtility.IsValidBedFor(ownedBed, sleeper, traveler, checkSocialProperness, false, ignoreOtherReservations, guestStatus))
                {
                    return ownedBed;
                }
            }
            int dg;
            int dg2;
            for (dg = 0; dg < 3; dg = dg2 + 1)
            {
                Danger maxDanger = (dg <= 1) ? Danger.None : Danger.Deadly;
                for (int k = 0; k < list2.Count; k++)
                {
                    ThingDef thingDef2 = list2[k];
                    if (RestUtility.CanUseBedEver(sleeper, thingDef2))
                    {
                        IntVec3 positionHeld = sleeper.PositionHeld;
                        Map mapHeld = sleeper.MapHeld;
                        ThingRequest thingReq = ThingRequest.ForDef(thingDef2);
                        PathEndMode peMode = PathEndMode.OnCell;
                        TraverseParms traverseParams = TraverseParms.For(traveler, Danger.Deadly, TraverseMode.ByPawn, false, false, false);
                        float maxDistance = 9999f;
                        bool validator(Thing b)
                        {
                            if (((Building_Bed)b).Medical || b.Position.GetDangerFor(sleeper, b.MapHeld) > maxDanger || !RestUtility.IsValidBedFor(b, sleeper, traveler, checkSocialProperness, false, ignoreOtherReservations, guestStatus))
                            {
                                return false;
                            }
                            if (dg <= 0)
                            {
                                return !b.Position.GetItems(b.Map).Any((Thing thing) => thing.def.IsCorpse);
                            }
                            return true;
                        }
                        Building_Bed building_Bed2 = (Building_Bed)GenClosestOnVehicle.ClosestThingReachable(positionHeld, mapHeld, thingReq, peMode, traverseParams, maxDistance, validator, null, 0, -1, false, RegionType.Set_Passable, false, false, out exitSpot, out enterSpot);
                        if (building_Bed2 != null)
                        {
                            return building_Bed2;
                        }
                    }
                }
                dg2 = dg;
            }
            return null;
        }

        private static List<ThingDef> bedDefsBestToWorst_RestEffectiveness = AccessTools.StaticFieldRefAccess<List<ThingDef>>(typeof(RestUtility), "bedDefsBestToWorst_RestEffectiveness");

        private static List<ThingDef> bedDefsBestToWorst_Medical = AccessTools.StaticFieldRefAccess<List<ThingDef>>(typeof(RestUtility), "bedDefsBestToWorst_Medical");
       
        private static List<ThingDef> bedDefsBestToWorst_SlabBed_RestEffectiveness = AccessTools.StaticFieldRefAccess<List<ThingDef>>(typeof(RestUtility), "bedDefsBestToWorst_SlabBed_RestEffectiveness");
       
        private static List<ThingDef> bedDefsBestToWorst_SlabBed_Medical = AccessTools.StaticFieldRefAccess<List<ThingDef>>(typeof(RestUtility), "bedDefsBestToWorst_SlabBed_Medical");
    }
}
