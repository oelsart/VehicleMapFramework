using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;
using VehicleInteriors.VIF_HarmonyPatches;

using RimWorld.Planet;
using UnityEngine;

namespace VehicleInteriors
{
    public static class FloatMenuMakerOnVehicle
    {
        public static FloatMenuOption GotoLocationOption(IntVec3 clickCell, Pawn pawn, bool suppressAutoTakeableGoto)
        {
            if (suppressAutoTakeableGoto)
            {
                return null;
            }
            IntVec3 curLoc = ReachabilityUtilityOnVehicle.StandableCellNear(clickCell, pawn.Map, 2.9f, null, out var map);
            if (!curLoc.IsValid || !(curLoc != pawn.Position))
            {
                return null;
            }
            if (ModsConfig.BiotechActive && pawn.IsColonyMech && !MechanitorUtility.InMechanitorCommandRange(pawn, curLoc))
            {
                return new FloatMenuOption("CannotGoOutOfRange".Translate() + ": " + "OutOfCommandRange".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
            }
            if (!pawn.CanReach(curLoc, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var dest1, out var dest2))
            {
                return new FloatMenuOption("CannotGoNoPath".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
            }
            Action action = delegate ()
            {
                var cell = ReachabilityUtilityOnVehicle.BestOrderedGotoDestNear(curLoc, pawn, (IntVec3 c) => c.InBounds(map), map);
                FloatMenuMakerOnVehicle.PawnGotoAction(clickCell, pawn, dest1, dest2, new LocalTargetInfo(cell));
            };
            return new FloatMenuOption("GoHere".Translate(), action, MenuOptionPriority.GoHere, null, null, 0f, null, null, true, 0)
            {
                autoTakeable = true,
                autoTakeablePriority = 10f
            };
        }

        public static void PawnGotoAction(IntVec3 clickCell, Pawn pawn, LocalTargetInfo dest1, LocalTargetInfo dest2, LocalTargetInfo dest3)
        {
            bool flag;
            if ((!dest1.IsValid && !dest2.IsValid && pawn.Position == dest3.Cell) || (pawn.CurJobDef == VIF_DefOf.VIF_GotoAcrossMaps && pawn.CurJob.targetA == dest1 && pawn.CurJob.targetB == dest2 && pawn.CurJob.targetC == dest3))
            {
                flag = true;
            }
            else
            {
                var baseMap = pawn.BaseMapOfThing();
                Job job = JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps, dest1, dest2, dest3);
                if (baseMap.exitMapGrid.IsExitCell(clickCell))
                {
                    job.exitMapOnArrival = !pawn.IsColonyMech;
                }
                else if (!baseMap.IsPlayerHome && !baseMap.exitMapGrid.MapUsesExitGrid && CellRect.WholeMap(baseMap).IsOnEdge(clickCell, 3) && baseMap.Parent.GetComponent<FormCaravanComp>() != null && MessagesRepeatAvoider.MessageShowAllowed("MessagePlayerTriedToLeaveMapViaExitGrid-" + baseMap.uniqueID, 60f))
                {
                    if (baseMap.Parent.GetComponent<FormCaravanComp>().CanFormOrReformCaravanNow)
                    {
                        Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CanReform".Translate(), baseMap.Parent, MessageTypeDefOf.RejectInput, false);
                    }
                    else
                    {
                        Messages.Message("MessagePlayerTriedToLeaveMapViaExitGrid_CantReform".Translate(), baseMap.Parent, MessageTypeDefOf.RejectInput, false);
                    }
                }
                flag = pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
            }
            if (flag)
            {
                FleckMaker.Static(dest3.Cell, pawn.Map, FleckDefOf.FeedbackGoto, 1f);
            }
        }

        public static void AddDraftedOrders(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts, bool suppressAutoTakeableGoto)
        {
            var clickPosOnVehicle = clickPos.VehicleMapToOrig(SelectorOnVehicleUtility.vehicleForSelector);
            IntVec3 clickCell = IntVec3.FromVector3(clickPosOnVehicle);
            foreach (LocalTargetInfo attackTarg2 in SelectorOnVehicleUtility.TargetsAt(clickPos, TargetingParameters.ForAttackHostile(), true, null))
            {
                LocalTargetInfo attackTarg = attackTarg2;
                if (!ModsConfig.BiotechActive || !pawn.IsColonyMech || MechanitorUtility.InMechanitorCommandRange(pawn, attackTarg))
                {
                    if (pawn.equipment.Primary != null && !pawn.equipment.PrimaryEq.PrimaryVerb.verbProps.IsMeleeAttack)
                    {
                        string str;
                        Action rangedAct = FloatMenuOnVehicleUtility.GetRangedAttackAction(pawn, attackTarg, out str);
                        string text = "FireAt".Translate(attackTarg.Thing.Label, attackTarg.Thing);
                        MenuOptionPriority priority = (attackTarg.HasThing && pawn.HostileTo(attackTarg.Thing)) ? MenuOptionPriority.AttackEnemy : MenuOptionPriority.VeryLow;
                        FloatMenuOption floatMenuOption = new FloatMenuOption("", null, priority, null, attackTarg2.Thing, 0f, null, null, true, 0);
                        if (rangedAct == null)
                        {
                            text = text + ": " + str;
                        }
                        else
                        {
                            floatMenuOption.autoTakeable = (!attackTarg.HasThing || attackTarg.Thing.HostileTo(Faction.OfPlayer));
                            floatMenuOption.autoTakeablePriority = 40f;
                            floatMenuOption.action = delegate ()
                            {
                                FleckMaker.Static(attackTarg.Thing.DrawPos, attackTarg.Thing.BaseMapOfThing(), FleckDefOf.FeedbackShoot, 1f);
                                rangedAct();
                            };
                        }
                        floatMenuOption.Label = text;
                        opts.Add(floatMenuOption);
                    }
                    string str2;
                    Action meleeAct = FloatMenuOnVehicleUtility.GetMeleeAttackAction(pawn, attackTarg, out str2);
                    Pawn pawn2;
                    string text2;
                    if ((pawn2 = (attackTarg.Thing as Pawn)) != null && pawn2.Downed)
                    {
                        text2 = "MeleeAttackToDeath".Translate(attackTarg.Thing.Label, attackTarg.Thing);
                    }
                    else
                    {
                        text2 = "MeleeAttack".Translate(attackTarg.Thing.Label, attackTarg.Thing);
                    }
                    MenuOptionPriority priority2 = (attackTarg.HasThing && pawn.HostileTo(attackTarg.Thing)) ? MenuOptionPriority.AttackEnemy : MenuOptionPriority.VeryLow;
                    FloatMenuOption floatMenuOption2 = new FloatMenuOption("", null, priority2, null, attackTarg.Thing, 0f, null, null, true, 0);
                    if (meleeAct == null)
                    {
                        text2 = text2 + ": " + str2.CapitalizeFirst();
                    }
                    else
                    {
                        floatMenuOption2.autoTakeable = (!attackTarg.HasThing || attackTarg.Thing.HostileTo(Faction.OfPlayer));
                        floatMenuOption2.autoTakeablePriority = 30f;
                        floatMenuOption2.action = delegate ()
                        {
                            FleckMaker.Static(attackTarg.Thing.DrawPos, attackTarg.Thing.BaseMapOfThing(), FleckDefOf.FeedbackMelee, 1f);
                            meleeAct();
                        };
                    }
                    floatMenuOption2.Label = text2;
                    opts.Add(floatMenuOption2);
                }
            }
            if (!pawn.RaceProps.IsMechanoid && !pawn.IsMutant)
            {
                if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                {
                    foreach (LocalTargetInfo carryTarget in SelectorOnVehicleUtility.TargetsAt(clickPos, TargetingParameters.ForCarry(pawn), true, null))
                    {
                        FloatMenuOption item;
                        Map map = carryTarget.HasThing ? carryTarget.Thing.Map : pawn.Map;
                        if (!pawn.CanReach(carryTarget, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                        {
                            item = new FloatMenuOption("CannotCarry".Translate(carryTarget.Thing) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                        }
                        else
                        {
                            item = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Carry".Translate(carryTarget.Thing), delegate ()
                            {
                                carryTarget.Thing.SetForbidden(false, false);
                                Job job = JobMaker.MakeJob(VIF_DefOf.VIF_CarryDownedPawnDraftedAcrossMaps, carryTarget);
                                job.count = 1;
                                pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
                                var driver = job.GetCachedDriverDirect as JobDriver_CarryDownedPawnAcrossMaps;
                                Job gotoJob = JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps, exitSpot, enterSpot);
                                driver.SetSpots(exitSpot, enterSpot);
                            }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, carryTarget, "ReservedBy", null);
                        }
                        opts.Add(item);
                    }
                }
                if (pawn.IsCarryingPawn(null))
                {
                    Pawn carriedPawn = (Pawn)pawn.carryTracker.CarriedThing;
                    if (!carriedPawn.IsPrisonerOfColony)
                    {
                        foreach (LocalTargetInfo destTarget in SelectorOnVehicleUtility.TargetsAt(clickPos, TargetingParameters.ForDraftedCarryBed(carriedPawn, pawn, carriedPawn.GuestStatus), true, null))
                        {
                            FloatMenuOption item2;
                            Map map = destTarget.HasThing ? destTarget.Thing.Map : pawn.Map;
                            if (!pawn.CanReach(destTarget, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                            {
                                item2 = new FloatMenuOption("CannotPlaceIn".Translate(carriedPawn, destTarget.Thing) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                            }
                            else if (pawn.HostileTo(carriedPawn))
                            {
                                item2 = new FloatMenuOption("CannotPlaceIn".Translate(carriedPawn, destTarget.Thing) + ": " + "CarriedPawnHostile".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                            }
                            else
                            {
                                item2 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("PlaceIn".Translate(carriedPawn, destTarget.Thing), delegate ()
                                {
                                    destTarget.Thing.SetForbidden(false, false);
                                    Job job = JobMaker.MakeJob(VIF_DefOf.VIF_TakeDownedPawnToBedDraftedAcrossMaps, pawn.carryTracker.CarriedThing, destTarget);
                                    job.count = 1;
                                    pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
                                    var driver = job.GetCachedDriverDirect as JobDriver_TakeToBedAcrossMaps;
                                    driver.SetSpots(default, default, exitSpot, enterSpot);
                                }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, destTarget, "ReservedBy", null);
                            }
                            opts.Add(item2);
                        }
                    }
                    if (carriedPawn.CanBeCaptured())
                    {
                        foreach (var localTargetInfo in SelectorOnVehicleUtility.TargetsAt(clickPos, TargetingParameters.ForDraftedCarryBed(carriedPawn, pawn, new GuestStatus?(GuestStatus.Prisoner)), true, null))
                        {
                            Building_Bed bed = (Building_Bed)localTargetInfo.Thing;
                            FloatMenuOption item3;
                            Map map = bed.Map;
                            if (!pawn.CanReach(bed, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                            {
                                item3 = new FloatMenuOption("CannotPlaceIn".Translate(carriedPawn, bed) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                            }
                            else
                            {
                                TaggedString taggedString = "Capture".Translate(carriedPawn.LabelCap, carriedPawn);
                                if (!carriedPawn.guest.Recruitable)
                                {
                                    taggedString += string.Format(" ({0})", "Unrecruitable".Translate());
                                }
                                if (carriedPawn.Faction != null && carriedPawn.Faction != Faction.OfPlayer && !carriedPawn.Faction.Hidden && !carriedPawn.Faction.HostileTo(Faction.OfPlayer) && !carriedPawn.IsPrisonerOfColony)
                                {
                                    taggedString += string.Format(": {0}", "AngersFaction".Translate().CapitalizeFirst());
                                }
                                item3 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(taggedString, delegate ()
                                {
                                    bed.SetForbidden(false, false);
                                    Job job = JobMaker.MakeJob(VIF_DefOf.VIF_TakeDownedPawnToBedDraftedAcrossMaps, pawn.carryTracker.CarriedThing, bed);
                                    job.count = 1;
                                    pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
                                    var driver = job.GetCachedDriverDirect as JobDriver_TakeToBedAcrossMaps;
                                    driver.SetSpots(default, default, exitSpot, enterSpot);
                                }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, bed, "ReservedBy", null);
                            }
                            opts.Add(item3);
                        }
                    }
                    CompHoldingPlatformTarget compHoldingPlatformTarget;
                    if (ModsConfig.AnomalyActive && carriedPawn.TryGetComp(out compHoldingPlatformTarget) && compHoldingPlatformTarget.CanBeCaptured)
                    {
                        foreach (LocalTargetInfo localTargetInfo2 in SelectorOnVehicleUtility.TargetsAt(clickPos, TargetingParameters.ForBuilding(null), true, null))
                        {
                            CompEntityHolder compEntityHolder;
                            if (localTargetInfo2.Thing.TryGetComp(out compEntityHolder) && compEntityHolder.Available)
                            {
                                Thing thing = localTargetInfo2.Thing;
                                FloatMenuOption item4;
                                if (!pawn.CanReach(thing, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, thing.Map, out var exitSpot, out var enterSpot))
                                {
                                    item4 = new FloatMenuOption("CannotPlaceIn".Translate(carriedPawn, thing) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                                }
                                else
                                {
                                    TaggedString taggedString2 = "Capture".Translate(carriedPawn.LabelCap, carriedPawn);
                                    if (!localTargetInfo2.Thing.SafelyContains(carriedPawn))
                                    {
                                        float statValue = carriedPawn.GetStatValue(StatDefOf.MinimumContainmentStrength, true, -1);
                                        taggedString2 += string.Format(" ({0} {1:F0}, {2} {3:F0})", new object[]
                                        {
                                            "FloatMenuContainmentStrength".Translate().ToLower(),
                                            compEntityHolder.ContainmentStrength,
                                            "FloatMenuContainmentRequires".Translate(carriedPawn).ToLower(),
                                            statValue
                                        });
                                    }
                                    item4 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(taggedString2, delegate ()
                                    {
                                        thing.SetForbidden(false, false);
                                        Job job = JobMaker.MakeJob(VIF_DefOf.VIF_CarryToEntityHolderAlreadyHoldingAcrossMaps, thing, pawn.carryTracker.CarriedThing);
                                        job.count = 1;
                                        pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
                                        var driver = job.GetCachedDriverDirect as JobDriver_CarryToEntityHolderAlreadyHoldingAcrossMaps;
                                        driver.SetSpots(default, default, exitSpot, enterSpot);
                                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, thing, "ReservedBy", null);
                                }
                                opts.Add(item4);
                            }
                        }
                    }
                }
            }
        }
    }
}
