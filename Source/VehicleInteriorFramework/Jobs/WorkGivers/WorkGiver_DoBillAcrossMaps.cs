using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VehicleInteriors.Jobs.WorkGivers;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class WorkGiver_DoBillAcrossMaps : WorkGiver_DoBill, IWorkGiverAcrossMaps
    {
        public bool NeedVirtualMapTransfer => false;

        private class DefCountList
        {
            private readonly List<ThingDef> defs = new List<ThingDef>();

            private readonly List<float> counts = new List<float>();

            public int Count => defs.Count;

            public float this[ThingDef def]
            {
                get
                {
                    int num = defs.IndexOf(def);
                    if (num < 0)
                    {
                        return 0f;
                    }

                    return counts[num];
                }
                set
                {
                    int num = defs.IndexOf(def);
                    if (num < 0)
                    {
                        defs.Add(def);
                        counts.Add(value);
                        num = defs.Count - 1;
                    }
                    else
                    {
                        counts[num] = value;
                    }

                    CheckRemove(num);
                }
            }

            public float GetCount(int index)
            {
                return counts[index];
            }

            public void SetCount(int index, float val)
            {
                counts[index] = val;
                CheckRemove(index);
            }

            public ThingDef GetDef(int index)
            {
                return defs[index];
            }

            private void CheckRemove(int index)
            {
                if (counts[index] == 0f)
                {
                    counts.RemoveAt(index);
                    defs.RemoveAt(index);
                }
            }

            public void Clear()
            {
                defs.Clear();
                counts.Clear();
            }

            public void GenerateFrom(List<Thing> things)
            {
                Clear();
                for (int i = 0; i < things.Count; i++)
                {
                    this[things[i].def] += things[i].stackCount;
                }
            }
        }

        private readonly List<ThingCount> chosenIngThings = new List<ThingCount>();

        private static readonly List<IngredientCount> missingIngredients = new List<IngredientCount>();

        private static readonly List<Thing> tmpMissingUniqueIngredients = new List<Thing>();

        private static readonly IntRange ReCheckFailedBillTicksRange = new IntRange(500, 600);

        private static readonly List<Thing> relevantThings = new List<Thing>();

        private static readonly HashSet<Thing> processedThings = new HashSet<Thing>();

        private static readonly List<Thing> newRelevantThings = new List<Thing>();

        private static readonly List<Thing> tmpMedicine = new List<Thing>();

        private static readonly DefCountList availableCounts = new DefCountList();

        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override ThingRequest PotentialWorkThingRequest
        {
            get
            {
                if (def.fixedBillGiverDefs != null && def.fixedBillGiverDefs.Count == 1)
                {
                    return ThingRequest.ForDef(def.fixedBillGiverDefs[0]);
                }

                return ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver);
            }
        }

        public override Danger MaxPathDanger(Pawn pawn)
        {
            return Danger.Some;
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            if (!(thing is IBillGiver billGiver) || !ThingIsUsableBillGiver(thing) || !billGiver.BillStack.AnyShouldDoNow || !billGiver.UsableForBillsAfterFueling() || !pawn.CanReserve(thing, thing.Map, 1, -1, null, forced) || thing.IsBurning() || thing.IsForbidden(pawn))
            {
                return null;
            }

            if (thing.def.hasInteractionCell && !pawn.CanReserveSittableOrSpot_NewTemp(thing.Map, thing.InteractionCell, thing, forced))
            {
                return null;
            }

            CompRefuelable compRefuelable = thing.TryGetComp<CompRefuelable>();
            if (compRefuelable != null && !compRefuelable.HasFuel)
            {
                if (!RefuelWorkGiverUtilityOnVehicle.CanRefuel(pawn, thing, forced))
                {
                    return null;
                }

                return RefuelWorkGiverUtilityOnVehicle.RefuelJob(pawn, thing, forced);
            }

            billGiver.BillStack.RemoveIncompletableBills();
            return StartOrResumeBillJob(pawn, billGiver, forced);
        }

        private static UnfinishedThing ClosestUnfinishedThingForBill(Pawn pawn, Bill_ProductionWithUft bill, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            bool validator(Thing t) => !t.IsForbidden(pawn) && ((UnfinishedThing)t).Recipe == bill.recipe && ((UnfinishedThing)t).Creator == pawn && ((UnfinishedThing)t).ingredients.TrueForAll((Thing x) => bill.IsFixedOrAllowedIngredient(x.def)) && pawn.CanReserve(t, t.Map);
            return (UnfinishedThing)GenClosestOnVehicle.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(bill.recipe.unfinishedThingDef), PathEndMode.InteractionCell, TraverseParms.For(pawn, pawn.NormalMaxDanger()), 9999f, validator, null, 0, -1, false, RegionType.Set_Passable, false, false, out exitSpot, out enterSpot);
        }

        private static Job FinishUftJob(Pawn pawn, UnfinishedThing uft, Bill_ProductionWithUft bill, TargetInfo exitSpot, TargetInfo enterSpot)
        {
            if (uft.Creator != pawn)
            {
                Log.Error(string.Concat("Tried to get FinishUftJob for ", pawn, " finishing ", uft, " but its creator is ", uft.Creator));
                return null;
            }

            Job job = WorkGiverUtilityOnVehicle.HaulStuffOffBillGiverJob(pawn, bill.billStack.billGiver, uft);
            if (job != null && job.targetA.Thing != uft)
            {
                return job;
            }
            
            var giver = (Thing)bill.billStack.billGiver;
            if (ReachabilityUtilityOnVehicle.CanReach(uft.Map, uft.Position, giver, PathEndMode.InteractionCell, TraverseParms.For(pawn), giver.Map, out var exitSpot2, out var enterSpot2))
            {
                Job job2 = JobMaker.MakeJob(VMF_DefOf.VMF_DoBillAcrossMaps, giver).SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2);
                job2.bill = bill;
                job2.targetQueueB = new List<LocalTargetInfo> { uft };
                job2.countQueue = new List<int> { 1 };
                job2.haulMode = HaulMode.ToCellNonStorage;
                return job2;
            }
            return null;
        }

        private Job StartOrResumeBillJob(Pawn pawn, IBillGiver giver, bool forced = false)
        {
            bool flag = FloatMenuMakerMap.makingFor == pawn;
            for (int i = 0; i < giver.BillStack.Count; i++)
            {
                Bill bill = giver.BillStack[i];
                if ((bill.recipe.requiredGiverWorkType != null && bill.recipe.requiredGiverWorkType != def.workType) || (Find.TickManager.TicksGame <= bill.nextTickToSearchForIngredients && FloatMenuMakerMap.makingFor != pawn) || !bill.ShouldDoNow() || !bill.PawnAllowedToStartAnew(pawn))
                {
                    continue;
                }

                SkillRequirement skillRequirement = bill.recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn);
                if (skillRequirement != null)
                {
                    JobFailReason.Is("UnderRequiredSkill".Translate(skillRequirement.minLevel), bill.Label);
                    continue;
                }

                if (bill is Bill_Medical bill_Medical)
                {
                    if (bill_Medical.IsSurgeryViolationOnExtraFactionMember(pawn))
                    {
                        JobFailReason.Is("SurgeryViolationFellowFactionMember".Translate());
                        continue;
                    }

                    if (!pawn.CanReserve(bill_Medical.GiverPawn, giver.Map, 1, -1, null, forced))
                    {
                        Pawn pawn2 = giver.Map.reservationManager.FirstRespectedReserver(bill_Medical.GiverPawn, pawn);
                        JobFailReason.Is("IsReservedBy".Translate(bill_Medical.GiverPawn.LabelShort, pawn2.LabelShort));
                        continue;
                    }
                }

                if (bill is Bill_Mech bill_Mech && bill_Mech.Gestator.WasteProducer.Waste != null && bill_Mech.Gestator.GestatingMech == null)
                {
                    JobFailReason.Is("WasteContainerFull".Translate());
                    continue;
                }

                if (bill is Bill_ProductionWithUft bill_ProductionWithUft)
                {
                    if (bill_ProductionWithUft.BoundUft != null)
                    {
                        if (bill_ProductionWithUft.BoundWorker == pawn && pawn.CanReserveAndReach(bill_ProductionWithUft.BoundUft.Map, bill_ProductionWithUft.BoundUft, PathEndMode.Touch, Danger.Deadly, 1, -1, null, false, out var exitSpot, out var enterSpot) && !bill_ProductionWithUft.BoundUft.IsForbidden(pawn))
                        {
                            return FinishUftJob(pawn, bill_ProductionWithUft.BoundUft, bill_ProductionWithUft, exitSpot, enterSpot);
                        }

                        continue;
                    }

                    UnfinishedThing unfinishedThing = ClosestUnfinishedThingForBill(pawn, bill_ProductionWithUft, out var exitSpot2, out var enterSpot2);
                    if (unfinishedThing != null)
                    {
                        return FinishUftJob(pawn, unfinishedThing, bill_ProductionWithUft, exitSpot2, enterSpot2);
                    }
                }

                if (bill is Bill_Autonomous bill_Autonomous && bill_Autonomous.State != 0)
                {
                    return WorkOnFormedBill((Thing)giver, bill_Autonomous);
                }

                List<IngredientCount> list = null;
                if (flag)
                {
                    list = missingIngredients;
                    list.Clear();
                    tmpMissingUniqueIngredients.Clear();
                }

                Bill_Medical bill_Medical2 = bill as Bill_Medical;
                TargetInfo exitSpot3 = TargetInfo.Invalid;
                TargetInfo enterSpot3 = TargetInfo.Invalid;
                if (bill_Medical2 != null && bill_Medical2.uniqueRequiredIngredients?.NullOrEmpty() == false)
                {
                    foreach (Thing uniqueRequiredIngredient in bill_Medical2.uniqueRequiredIngredients)
                    {
                        if (uniqueRequiredIngredient.IsForbidden(pawn) || !pawn.CanReserveAndReach(uniqueRequiredIngredient.Map, uniqueRequiredIngredient, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, false, out var tmpExitSpot, out var tmpEnterSpot))
                        {
                            tmpMissingUniqueIngredients.Add(uniqueRequiredIngredient);
                        }
                        else
                        {
                            exitSpot3 = tmpExitSpot;
                            enterSpot3 = tmpEnterSpot;
                        }
                    }
                }

                if (!TryFindBestBillIngredients(bill, pawn, (Thing)giver, chosenIngThings, list) || !tmpMissingUniqueIngredients.NullOrEmpty())
                {
                    if (FloatMenuMakerMap.makingFor != pawn)
                    {
                        bill.nextTickToSearchForIngredients = Find.TickManager.TicksGame + ReCheckFailedBillTicksRange.RandomInRange;
                    }
                    else if (flag)
                    {
                        if (CannotDoBillDueToMedicineRestriction(giver, bill, list))
                        {
                            JobFailReason.Is("NoMedicineMatchingCategory".Translate(GetMedicalCareCategory((Thing)giver).GetLabel().Named("CATEGORY")), bill.Label);
                        }
                        else
                        {
                            string text = list.Select((IngredientCount missing) => missing.Summary).Concat(tmpMissingUniqueIngredients.Select((Thing t) => t.Label)).ToCommaList();
                            JobFailReason.Is("MissingMaterials".Translate(text), bill.Label);
                        }

                        flag = false;
                    }

                    chosenIngThings.Clear();
                    continue;
                }

                flag = false;
                if (bill_Medical2 != null && bill_Medical2.uniqueRequiredIngredients?.NullOrEmpty() == false)
                {
                    foreach (Thing uniqueRequiredIngredient2 in bill_Medical2.uniqueRequiredIngredients)
                    {
                        chosenIngThings.Add(new ThingCount(uniqueRequiredIngredient2, 1));
                    }
                }

                Job result = TryStartNewDoBillJob(pawn, bill, giver, chosenIngThings, out Job haulOffJob, true, exitSpot3, enterSpot3);
                chosenIngThings.Clear();
                return result;
            }

            chosenIngThings.Clear();
            return null;
        }

        private static bool CannotDoBillDueToMedicineRestriction(IBillGiver giver, Bill bill, List<IngredientCount> missingIngredients)
        {
            if (!(giver is Pawn pawn))
            {
                return false;
            }

            bool flag = false;
            foreach (IngredientCount missingIngredient in missingIngredients)
            {
                if (missingIngredient.filter.Allows(ThingDefOf.MedicineIndustrial))
                {
                    flag = true;
                    break;
                }
            }

            if (flag)
            {
                MedicalCareCategory medicalCareCategory = GetMedicalCareCategory(pawn);
                foreach (Thing item in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine))
                {
                    if (IsUsableIngredient(item, bill) && medicalCareCategory.AllowsMedicine(item.def))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public static Job TryStartNewDoBillJob(Pawn pawn, Bill bill, IBillGiver giver, List<ThingCount> chosenIngThings, out Job haulOffJob, bool dontCreateJobIfHaulOffRequired, TargetInfo exitSpot, TargetInfo enterSpot)
        {
            haulOffJob = WorkGiverUtilityOnVehicle.HaulStuffOffBillGiverJob(pawn, giver, null);
            if (haulOffJob != null && dontCreateJobIfHaulOffRequired)
            {
                return haulOffJob;
            }
            var firstThing = chosenIngThings.First().Thing;
            var giverThing = (Thing)giver;
            if (ReachabilityUtilityOnVehicle.CanReach(firstThing.Map, firstThing.Position, giverThing, PathEndMode.InteractionCell, TraverseParms.For(pawn), giver.Map, out var exitSpot2, out var enterSpot2))
            {
                Job job = JobMaker.MakeJob(VMF_DefOf.VMF_DoBillAcrossMaps, (Thing)giver).SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2);
                job.targetQueueB = new List<LocalTargetInfo>(chosenIngThings.Count);
                job.countQueue = new List<int>(chosenIngThings.Count);
                for (int i = 0; i < chosenIngThings.Count; i++)
                {
                    job.targetQueueB.Add(chosenIngThings[i].Thing);
                    job.countQueue.Add(chosenIngThings[i].Count);
                }

                if (bill.xenogerm != null)
                {
                    job.targetQueueB.Add(bill.xenogerm);
                    job.countQueue.Add(1);
                }

                job.haulMode = HaulMode.ToCellNonStorage;
                job.bill = bill;
                return job;
            }
            return null;
        }

        private static Job WorkOnFormedBill(Thing giver, Bill_Autonomous bill)
        {
            Job job = JobMaker.MakeJob(JobDefOf.DoBill, giver);
            job.bill = bill;
            return job;
        }

        private static bool IsUsableIngredient(Thing t, Bill bill)
        {
            if (!bill.IsFixedOrAllowedIngredient(t))
            {
                return false;
            }

            foreach (IngredientCount ingredient in bill.recipe.ingredients)
            {
                if (ingredient.filter.Allows(t))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindBestBillIngredients(Bill bill, Pawn pawn, Thing billGiver, List<ThingCount> chosen, List<IngredientCount> missingIngredients)
        {
            return TryFindBestIngredientsHelper((Thing t) => IsUsableIngredient(t, bill), (List<Thing> foundThings) => TryFindBestBillIngredientsInSet(foundThings, bill, chosen, GetBillGiverRootCell(billGiver, pawn), billGiver is Pawn, missingIngredients), bill.recipe.ingredients, pawn, billGiver, chosen, bill.ingredientSearchRadius);
        }

        private static bool TryFindBestIngredientsHelper(Predicate<Thing> thingValidator, Predicate<List<Thing>> foundAllIngredientsAndChoose, List<IngredientCount> ingredients, Pawn pawn, Thing billGiver, List<ThingCount> chosen, float searchRadius)
        {
            chosen.Clear();
            newRelevantThings.Clear();
            if (ingredients.Count == 0)
            {
                return true;
            }

            IntVec3 billGiverRootCell = GetBillGiverRootCell(billGiver, pawn);
            Region rootReg = billGiverRootCell.GetRegion(billGiver.Map);
            if (rootReg == null)
            {
                return false;
            }

            relevantThings.Clear();
            processedThings.Clear();
            bool foundAll = false;
            float radiusSq = searchRadius * searchRadius;
            bool baseValidator(Thing t) => t.Spawned && thingValidator(t) && (float)(t.PositionOnBaseMap() - billGiver.PositionOnBaseMap()).LengthHorizontalSquared < radiusSq && !t.IsForbidden(pawn) && pawn.CanReserve(t);
            bool billGiverIsPawn = billGiver is Pawn;
            if (billGiverIsPawn)
            {
                AddEveryMedicineToRelevantThings(pawn, billGiver, relevantThings, baseValidator, pawn.Map);
                if (foundAllIngredientsAndChoose(relevantThings))
                {
                    relevantThings.Clear();
                    return true;
                }
            }

            if (billGiver is Building_WorkTableAutonomous building_WorkTableAutonomous)
            {
                relevantThings.AddRange(building_WorkTableAutonomous.innerContainer);
                if (foundAllIngredientsAndChoose(relevantThings))
                {
                    relevantThings.Clear();
                    return true;
                }
            }

            TraverseParms traverseParams = TraverseParms.For(pawn);
            RegionEntryPredicate entryCondition = null;
            if (Math.Abs(999f - searchRadius) >= 1f)
            {
                entryCondition = delegate (Region from, Region r)
                {
                    if (!r.Allows(traverseParams, isDestination: false))
                    {
                        return false;
                    }

                    CellRect extentsClose = r.extentsClose;
                    int num2 = Math.Abs(billGiver.Position.x - Math.Max(extentsClose.minX, Math.Min(billGiver.Position.x, extentsClose.maxX)));
                    if ((float)num2 > searchRadius)
                    {
                        return false;
                    }

                    int num3 = Math.Abs(billGiver.Position.z - Math.Max(extentsClose.minZ, Math.Min(billGiver.Position.z, extentsClose.maxZ)));
                    return !((float)num3 > searchRadius) && (float)(num2 * num2 + num3 * num3) <= radiusSq;
                };
            }
            else
            {
                entryCondition = (Region from, Region r) => r.Allows(traverseParams, isDestination: false);
            }

            int adjacentRegionsAvailable = rootReg.Neighbors.Count((Region region) => entryCondition(rootReg, region));
            int regionsProcessed = 0;
            processedThings.AddRange(relevantThings);
            foundAllIngredientsAndChoose(relevantThings);
            bool regionProcessor(Region r)
            {
                List<Thing> list = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));
                for (int i = 0; i < list.Count; i++)
                {
                    Thing thing = list[i];
                    if (!processedThings.Contains(thing) && ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, r, PathEndMode.ClosestTouch, pawn) && baseValidator(thing) && !(thing.def.IsMedicine && billGiverIsPawn))
                    {
                        newRelevantThings.Add(thing);
                        processedThings.Add(thing);
                    }
                }

                int num = regionsProcessed + 1;
                regionsProcessed = num;
                if (newRelevantThings.Count > 0 && regionsProcessed > adjacentRegionsAvailable)
                {
                    relevantThings.AddRange(newRelevantThings);
                    newRelevantThings.Clear();
                    if (foundAllIngredientsAndChoose(relevantThings))
                    {
                        foundAll = true;
                        return true;
                    }
                }

                return false;
            }
            RegionTraverserAcrossMaps.BreadthFirstTraverse(rootReg, entryCondition, regionProcessor, 99999);
            relevantThings.Clear();
            newRelevantThings.Clear();
            processedThings.Clear();
            return foundAll;
        }

        private static IntVec3 GetBillGiverRootCell(Thing billGiver, Pawn forPawn)
        {
            if (billGiver is Building building)
            {
                if (building.def.hasInteractionCell)
                {
                    return building.InteractionCell;
                }

                Log.Error(string.Concat("Tried to find bill ingredients for ", billGiver, " which has no interaction cell."));
                return forPawn.Position;
            }

            return billGiver.Position;
        }

        private static void AddEveryMedicineToRelevantThings(Pawn pawn, Thing billGiver, List<Thing> relevantThings, Predicate<Thing> baseValidator, Map map)
        {
            MedicalCareCategory medicalCareCategory = GetMedicalCareCategory(billGiver);
            List<Thing> list = map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine);
            tmpMedicine.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                if (medicalCareCategory.AllowsMedicine(thing.def) && baseValidator(thing) && pawn.CanReach(thing, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, thing.MapHeld, out _, out _))
                {
                    tmpMedicine.Add(thing);
                }
            }

            tmpMedicine.SortBy((Thing x) => 0f - x.GetStatValue(StatDefOf.MedicalPotency), (Thing x) => x.PositionOnBaseMap().DistanceToSquared(billGiver.PositionOnBaseMap()));
            relevantThings.AddRange(tmpMedicine);
            tmpMedicine.Clear();
        }


        private static bool TryFindBestBillIngredientsInSet(List<Thing> availableThings, Bill bill, List<ThingCount> chosen, IntVec3 rootCell, bool alreadySorted, List<IngredientCount> missingIngredients)
        {
            if (bill.recipe.allowMixingIngredients)
            {
                return TryFindBestBillIngredientsInSet_AllowMix(availableThings, bill, chosen, rootCell, missingIngredients);
            }

            return TryFindBestBillIngredientsInSet_NoMix(availableThings, bill, chosen, rootCell, alreadySorted, missingIngredients);
        }

        private static bool TryFindBestBillIngredientsInSet_NoMix(List<Thing> availableThings, Bill bill, List<ThingCount> chosen, IntVec3 rootCell, bool alreadySorted, List<IngredientCount> missingIngredients)
        {
            return TryFindBestIngredientsInSet_NoMixHelper(availableThings, bill.recipe.ingredients, chosen, rootCell, alreadySorted, missingIngredients, bill);
        }

        private static bool TryFindBestIngredientsInSet_NoMixHelper(List<Thing> availableThings, List<IngredientCount> ingredients, List<ThingCount> chosen, IntVec3 rootCell, bool alreadySorted, List<IngredientCount> missingIngredients, Bill bill = null)
        {
            if (!alreadySorted)
            {
                Comparison<Thing> comparison = delegate (Thing t1, Thing t2)
                {
                    float num4 = (t1.PositionHeldOnBaseMap() - rootCell).LengthHorizontalSquared;
                    float value = (t2.PositionHeldOnBaseMap() - rootCell).LengthHorizontalSquared;
                    return num4.CompareTo(value);
                };
                availableThings.Sort(comparison);
            }

            chosen.Clear();
            availableCounts.Clear();
            missingIngredients?.Clear();
            availableCounts.GenerateFrom(availableThings);
            for (int i = 0; i < ingredients.Count; i++)
            {
                IngredientCount ingredientCount = ingredients[i];
                bool flag = false;
                for (int j = 0; j < availableCounts.Count; j++)
                {
                    float num = ((bill != null) ? ((float)ingredientCount.CountRequiredOfFor(availableCounts.GetDef(j), bill.recipe, bill)) : ingredientCount.GetBaseCount());
                    if ((bill != null && !bill.recipe.ignoreIngredientCountTakeEntireStacks && num > availableCounts.GetCount(j)) || !ingredientCount.filter.Allows(availableCounts.GetDef(j)) || (bill != null && !ingredientCount.IsFixedIngredient && !bill.ingredientFilter.Allows(availableCounts.GetDef(j))))
                    {
                        continue;
                    }

                    for (int k = 0; k < availableThings.Count; k++)
                    {
                        if (availableThings[k].def != availableCounts.GetDef(j))
                        {
                            continue;
                        }

                        int num2 = availableThings[k].stackCount - ThingCountUtility.CountOf(chosen, availableThings[k]);
                        if (num2 > 0)
                        {
                            if (bill != null && bill.recipe.ignoreIngredientCountTakeEntireStacks)
                            {
                                ThingCountUtility.AddToList(chosen, availableThings[k], num2);
                                return true;
                            }

                            int num3 = Mathf.Min(Mathf.FloorToInt(num), num2);
                            ThingCountUtility.AddToList(chosen, availableThings[k], num3);
                            num -= (float)num3;
                            if (num < 0.001f)
                            {
                                flag = true;
                                float count = availableCounts.GetCount(j);
                                count -= num;
                                availableCounts.SetCount(j, count);
                                break;
                            }
                        }
                    }

                    if (flag)
                    {
                        break;
                    }
                }

                if (!flag)
                {
                    if (missingIngredients == null)
                    {
                        return false;
                    }

                    missingIngredients.Add(ingredientCount);
                }
            }

            if (missingIngredients != null)
            {
                return missingIngredients.Count == 0;
            }

            return true;
        }

        private static bool TryFindBestBillIngredientsInSet_AllowMix(List<Thing> availableThings, Bill bill, List<ThingCount> chosen, IntVec3 rootCell, List<IngredientCount> missingIngredients)
        {
            chosen.Clear();
            missingIngredients?.Clear();
            availableThings.SortBy((Thing t) => bill.recipe.IngredientValueGetter.ValuePerUnitOf(t.def), (Thing t) => (t.Position - rootCell).LengthHorizontalSquared);
            for (int i = 0; i < bill.recipe.ingredients.Count; i++)
            {
                IngredientCount ingredientCount = bill.recipe.ingredients[i];
                float num = ingredientCount.GetBaseCount();
                for (int j = 0; j < availableThings.Count; j++)
                {
                    Thing thing = availableThings[j];
                    if (ingredientCount.filter.Allows(thing) && (ingredientCount.IsFixedIngredient || bill.ingredientFilter.Allows(thing)))
                    {
                        float num2 = bill.recipe.IngredientValueGetter.ValuePerUnitOf(thing.def);
                        int num3 = Mathf.Min(Mathf.CeilToInt(num / num2), thing.stackCount);
                        ThingCountUtility.AddToList(chosen, thing, num3);
                        num -= (float)num3 * num2;
                        if (num <= 0.0001f)
                        {
                            break;
                        }
                    }
                }

                if (num > 0.0001f)
                {
                    if (missingIngredients == null)
                    {
                        return false;
                    }

                    missingIngredients.Add(ingredientCount);
                }
            }

            if (missingIngredients != null)
            {
                return missingIngredients.Count == 0;
            }

            return true;
        }
    }
}
