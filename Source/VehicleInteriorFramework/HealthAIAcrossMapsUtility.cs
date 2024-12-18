using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using VehicleInteriors.Jobs;

namespace VehicleInteriors
{
    public static class HealthAIAcrossMapsUtility
    {
        public static bool CanRescueNow(Pawn rescuer, Pawn patient, bool forced, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            exitSpot = null;
            enterSpot = null;
            return (forced || patient.Faction == rescuer.Faction) && HealthAIUtility.WantsToBeRescued(patient) && (forced ||
                !patient.IsForbidden(rescuer)) && (forced || !HealthAIAcrossMapsUtility.EnemyIsNear(patient, 25f, out _, false)) &&
                rescuer.CanReserveAndReach(patient.Map, patient, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, forced, out exitSpot, out enterSpot);
        }

        public static bool EnemyIsNear(Pawn p, float radius, out Thing threat, bool meleeOnly = false, bool requireLos = false)
        {
            threat = null;
            if (!p.Spawned)
            {
                return false;
            }
            var baseMap = p.BaseMap();
            bool flag = p.PositionOnBaseMap().Fogged(baseMap);
            List<IAttackTarget> potentialTargetsFor = baseMap.attackTargetsCache.GetPotentialTargetsFor(p);
            for (int i = 0; i < potentialTargetsFor.Count; i++)
            {
                IAttackTarget attackTarget = potentialTargetsFor[i];
                if (!attackTarget.ThreatDisabled(p) && (flag || !attackTarget.Thing.PositionOnBaseMap().Fogged(baseMap)) && (!requireLos || GenSightOnVehicle.LineOfSightThingToThing(p, attackTarget.Thing, false, null)))
                {
                    Pawn pawn;
                    if (meleeOnly && (pawn = (attackTarget as Pawn)) != null && pawn.equipment != null)
                    {
                        CompEquippable primaryEq = pawn.equipment.PrimaryEq;
                        if (primaryEq != null && !primaryEq.PrimaryVerb.IsMeleeAttack)
                        {
                            continue;
                        }
                    }
                    if (p.PositionOnBaseMap().InHorDistOf(((Thing)attackTarget).PositionOnBaseMap(), radius))
                    {
                        threat = (Thing)attackTarget;
                        return true;
                    }
                }
            }
            return false;
        }

        public static Thing FindBestMedicine(Pawn healer, Pawn patient, bool onlyUseInventory, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            exitSpot = null;
            enterSpot = null;
            if (patient.playerSettings != null && patient.playerSettings.medCare <= MedicalCareCategory.NoMeds)
			{
                return null;
            }
            if (Medicine.GetMedicineCountToFullyHeal(patient) <= 0)
            {
                return null;
            }
            Predicate<Thing> validator = (Thing m) =>
            {
                bool flag = ((patient.playerSettings != null) ? patient.playerSettings.medCare : MedicalCareCategory.NoMeds).AllowsMedicine(m.def);
                if (patient.playerSettings == null & onlyUseInventory)
				{
                    flag = true;
                }
                return !m.IsForbidden(healer) && flag && healer.CanReserve(m, m.Map, 10, 1, null, false);
            };
            Func<Thing, bool> FindBestMedicine = (Thing t) =>
            {
                return t.def.IsMedicine && validator(t);
            };
            Func<Thing, float> PriorityOf = (Thing t) =>
            {
                return t.def.GetStatValueAbstract(StatDefOf.MedicalPotency);
            };
            Thing GetBestMedInInventory(ThingOwner<Thing> inventory)
            {
                if (inventory.Count == 0) return null;
                return inventory.Where(FindBestMedicine).OrderByDescending(PriorityOf).FirstOrDefault();
            }
            Thing thing = GetBestMedInInventory(healer.inventory.innerContainer);
            if (onlyUseInventory)
			{
                return thing;
            }
            var baseMap = patient.MapHeldBaseMap();
            var serchSet = baseMap.listerThings.ThingsInGroup(ThingRequestGroup.Medicine).Concat(VehiclePawnWithMapCache.allVehicles[baseMap].SelectMany(v => v.interiorMap.listerThings.ThingsInGroup(ThingRequestGroup.Medicine)));
            Thing thing2 = GenClosestOnVehicle.ClosestThing_Global_Reachable(patient.PositionHeld, patient.MapHeld, serchSet, PathEndMode.ClosestTouch, TraverseParms.For(healer, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, validator, PriorityOf, false, out var exitSpot2, out var enterSpot2);
            if (thing == null || thing2 == null)
            {
                if (thing == null && thing2 == null && healer.IsColonist && healer.Map != null)
				{
                    var mapPawns = baseMap.mapPawns.SpawnedColonyAnimals.Concat(VehiclePawnWithMapCache.allVehicles[baseMap].SelectMany(v => v.interiorMap.mapPawns.SpawnedColonyAnimals));
                    foreach (Pawn pawn in mapPawns)
					{
                        Thing thing3 = GetBestMedInInventory(pawn.inventory.innerContainer);
                        if (thing3 != null && (thing2 == null || PriorityOf(thing2) < PriorityOf(thing3)) && !pawn.IsForbidden(healer) && healer.CanReach(pawn, PathEndMode.OnCell, Danger.Some, false, false, TraverseMode.ByPawn, pawn.Map, out var exitSpot3, out var enterSpot3))
						{
                            thing2 = thing3;
                            exitSpot2 = exitSpot3;
                            enterSpot2 = enterSpot3;
                        }
                    }
                }
                if (thing == null)
                {
                    exitSpot = exitSpot2;
                    enterSpot = enterSpot2;
                    return thing2;
                }
                else
                {
                    return thing;
                }
            }
            if (PriorityOf(thing) < PriorityOf(thing2))
            {
                exitSpot = exitSpot2;
                enterSpot = enterSpot2;
                return thing2;
            }
            return thing;
        }
    }
}
