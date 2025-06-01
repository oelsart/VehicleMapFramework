using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.Utility;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VehicleInteriors.VMF_HarmonyPatches;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using static VehicleInteriors.ModCompat;

namespace VehicleInteriors
{
    public static class FloatMenuMakerOnVehicle
    {
        private static bool CanTakeOrder(Pawn pawn)
        {
            return pawn.IsColonistPlayerControlled || pawn.IsColonyMech || pawn.IsColonyMutantPlayerControlled || pawn is VehiclePawn;
        }

        private static bool LordBlocksFloatMenu(Pawn pawn)
        {
            Lord lord = pawn.GetLord();
            return lord != null && !lord.AllowsFloatMenu(pawn);
        }

        public static List<FloatMenuOption> ChoicesAtFor(Vector3 clickPos, Pawn pawn, bool suppressAutoTakeableGoto = false)
        {
            IntVec3 intVec = IntVec3.FromVector3(clickPos);
            IntVec3 clickCell;
            Map map;
            if (GenUIOnVehicle.vehicleForSelector != null)
            {
                clickCell = IntVec3.FromVector3(clickPos).ToVehicleMapCoord(GenUIOnVehicle.vehicleForSelector);
                map = GenUIOnVehicle.vehicleForSelector.VehicleMap;
            }
            else
            {
                clickCell = IntVec3.FromVector3(clickPos);
                map = pawn.BaseMap();
            }
            var baseMap = map.BaseMap();
            List<FloatMenuOption> list = new List<FloatMenuOption>();
            pawn.GetLord();
            if ((!intVec.InBounds(baseMap) && !clickCell.InBounds(map)) || !FloatMenuMakerOnVehicle.CanTakeOrder(pawn) || FloatMenuMakerOnVehicle.LordBlocksFloatMenu(pawn))
            {
                return list;
            }
            if (pawn.BaseMap() != Find.CurrentMap)
            {
                return list;
            }
            FloatMenuMakerMap.makingFor = pawn;
            try
            {
                if (intVec.Fogged(baseMap))
                {
                    if (pawn.Drafted)
                    {
                        FloatMenuOption floatMenuOption = FloatMenuMakerOnVehicle.GotoLocationOption(clickCell, pawn, suppressAutoTakeableGoto, map);
                        if (floatMenuOption != null && !floatMenuOption.Disabled)
                        {
                            list.Add(floatMenuOption);
                        }
                    }
                }
                else
                {
                    if (pawn.Drafted)
                    {
                        FloatMenuMakerOnVehicle.AddDraftedOrders(clickPos, pawn, list, suppressAutoTakeableGoto);
                    }
                    if (pawn.RaceProps.Humanlike && !pawn.IsMutant)
                    {
                        FloatMenuMakerOnVehicle.AddHumanlikeOrders(clickPos, pawn, list);
                    }
                    if (ModsConfig.AnomalyActive && pawn.IsMutant)
                    {
                        FloatMenuMakerOnVehicle.AddMutantOrders(clickPos, pawn, list);
                    }
                    if (!pawn.Drafted && (!pawn.RaceProps.IsMechanoid || DebugSettings.allowUndraftedMechOrders) && !pawn.IsMutant)
                    {
                        FloatMenuMakerOnVehicle.AddUndraftedOrders(clickPos, pawn, list);
                    }
                    foreach (FloatMenuOption item in pawn.GetExtraFloatMenuOptionsFor(intVec))
                    {
                        list.Add(item);
                    }
                    if (!Find.CurrentMap.IsVehicleMapOf(out _))
                    {
                        FloatMenuOption floatMenuOptFor = EnterPortalUtility.GetFloatMenuOptFor(pawn, intVec);
                        if (floatMenuOptFor != null)
                        {
                            list.Add(floatMenuOptFor);
                        }
                    }
                }
            }
            finally
            {
                FloatMenuMakerMap.makingFor = null;
                GenUIOnVehicle.vehicleForSelector = null;
            }
            return list;
        }

        public static FloatMenuOption GotoLocationOption(IntVec3 clickCell, Pawn pawn, bool suppressAutoTakeableGoto, Map map)
        {
            if (suppressAutoTakeableGoto)
            {
                return null;
            }

            if (pawn is VehiclePawn vehicle)
            {
                if (vehicle.Faction != Faction.OfPlayer || !vehicle.CanMoveFinal)
                {
                    return null;
                }
                if (vehicle.Deploying || (vehicle.CompVehicleTurrets != null && vehicle.CompVehicleTurrets.Deployed))
                {
                    Messages.Message("VF_VehicleImmobileDeployed".Translate(vehicle), MessageTypeDefOf.RejectInput);
                    return null;
                }

                if (vehicle.CompFueledTravel != null && vehicle.CompFueledTravel.EmptyTank)
                {
                    Messages.Message("VF_OutOfFuel".Translate(vehicle), MessageTypeDefOf.RejectInput);
                    return null;
                }

                int num = GenRadial.NumCellsInRadius(2.9f);
                IntVec3 curLoc;
                for (int i = 0; i < num; i++)
                {
                    curLoc = GenRadial.RadialPattern[i] + clickCell;

                    if (GenGridVehicles.Standable(curLoc, vehicle, map) && (!VehicleMod.settings.main.fullVehiclePathing || vehicle.DrivableRectOnCell(curLoc, false, map)))
                    {
                        if (curLoc == vehicle.Position || vehicle.beached)
                        {
                            return null;
                        }
                        if (!vehicle.CanReachVehicle(curLoc, PathEndMode.OnCell, Danger.Deadly, TraverseMode.ByPawn, map, out var dest1, out var dest2))
                        {
                            return new FloatMenuOption("VF_CannotMoveToCell".Translate(vehicle.LabelCap), null);
                        }

                        return new FloatMenuOption("GoHere".Translate(), delegate ()
                        {
                            VehicleOrientationControllerAcrossMaps.StartOrienting(vehicle, curLoc, clickCell, map, dest1, dest2);
                        }, MenuOptionPriority.GoHere, null, null, 0f, null, null)
                        {
                            autoTakeable = true,
                            autoTakeablePriority = 10f
                        };
                    }
                }
                return null;
            }
            else
            {
                if (PathingHelper.VehicleImpassableInCell(map, clickCell))
                {
                    return new FloatMenuOption("CannotGoNoPath".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null);
                }

                IntVec3 curLoc = CellFinder.StandableCellNear(clickCell, map, 2.9f, null);
                if (!curLoc.IsValid || (pawn.Map == map && curLoc == pawn.Position))
                {
                    return null;
                }
                var baseLoc = map.IsVehicleMapOf(out var vehicle2) ? curLoc.ToBaseMapCoord(vehicle2) : curLoc;
                if (ModsConfig.BiotechActive && pawn.IsColonyMech && !MechanitorUtility.InMechanitorCommandRange(pawn, baseLoc))
                {
                    return new FloatMenuOption("CannotGoOutOfRange".Translate() + ": " + "OutOfCommandRange".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                }
                bool allowsGetOff = false;
                pawn.IsOnVehicleMapOf(out var vehicle3);
                if (vehicle3 != null)
                {
                    allowsGetOff = vehicle3.AllowsGetOff;
                    vehicle3.AllowsGetOff = true;
                }
                var canReach = pawn.CanReach(curLoc, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var dest1, out var dest2);
                if (vehicle3 != null)
                {
                    vehicle3.AllowsGetOff = allowsGetOff;
                }
                if (!canReach)
                {
                    return new FloatMenuOption("CannotGoNoPath".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                }
                void action()
                {
                    if (vehicle3 != null)
                    {
                        allowsGetOff = vehicle3.AllowsGetOff;
                        vehicle3.AllowsGetOff = true;
                    }
                    var cell = ReachabilityUtilityOnVehicle.BestOrderedGotoDestNear(curLoc, pawn, (IntVec3 c) => c.InBounds(map), map, out dest1, out dest2);
                    if (vehicle3 != null)
                    {
                        vehicle3.AllowsGetOff = allowsGetOff;
                    }
                    FloatMenuMakerOnVehicle.PawnGotoAction(clickCell, pawn, map, dest1, dest2, cell);
                }
                return new FloatMenuOption("GoHere".Translate(), action, MenuOptionPriority.GoHere, null, null, 0f, null, null, true, 0)
                {
                    autoTakeable = true,
                    autoTakeablePriority = 10f
                };
            }
        }

        public static void PawnGotoAction(IntVec3 clickCell, Pawn pawn, Map map, TargetInfo dest1, TargetInfo dest2, LocalTargetInfo dest3)
        {
            bool flag;
            var baseMap = map.BaseMap();
            if ((!dest1.IsValid && !dest2.IsValid && pawn.Map == map && pawn.Position == dest3.Cell) || (pawn.CurJobDef == VMF_DefOf.VMF_GotoAcrossMaps && pawn.Map == map && pawn.CurJob.targetA == dest3))
            {
                flag = true;
            }
            else
            {
                Job job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps, dest3).SetSpotsToJobAcrossMaps(pawn, dest1, dest2);
                if (pawn.Map == baseMap && baseMap.exitMapGrid.IsExitCell(clickCell))
                {
                    job.exitMapOnArrival = !pawn.IsColonyMech;
                }
                else if (!baseMap.IsPlayerHome && !baseMap.exitMapGrid.MapUsesExitGrid && pawn.Map == baseMap && CellRect.WholeMap(baseMap).IsOnEdge(clickCell, 3) && baseMap.Parent.GetComponent<FormCaravanComp>() != null && MessagesRepeatAvoider.MessageShowAllowed("MessagePlayerTriedToLeaveMapViaExitGrid-" + baseMap.uniqueID, 60f))
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
                var drawPos = map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned ? dest3.Cell.ToVector3Shifted().ToBaseMapCoord(vehicle) : dest3.Cell.ToVector3Shifted();
                FleckMaker.Static(drawPos, baseMap, FleckDefOf.FeedbackGoto, 1f);
            }
        }

        public static void AddDraftedOrders(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts, bool suppressAutoTakeableGoto)
        {
            IntVec3 clickCell;
            Map map;
            if (GenUIOnVehicle.vehicleForSelector != null)
            {
                clickCell = IntVec3.FromVector3(clickPos.ToVehicleMapCoord(GenUIOnVehicle.vehicleForSelector));
                map = GenUIOnVehicle.vehicleForSelector.VehicleMap;
            }
            else
            {
                clickCell = IntVec3.FromVector3(clickPos);
                map = pawn.BaseMap();
            }
            foreach (LocalTargetInfo attackTarg2 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForAttackHostile(), true, null))
            {
                LocalTargetInfo attackTarg = attackTarg2;
                if (!ModsConfig.BiotechActive || !pawn.IsColonyMech || MechanitorUtility.InMechanitorCommandRange(pawn, attackTarg))
                {
                    if (pawn.equipment.Primary != null && !pawn.equipment.PrimaryEq.PrimaryVerb.verbProps.IsMeleeAttack)
                    {
                        Action rangedAct = FloatMenuOnVehicleUtility.GetRangedAttackAction(pawn, attackTarg, out string str);
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
                                FleckMaker.Static(attackTarg.Thing.DrawPos, attackTarg.Thing.BaseMap(), FleckDefOf.FeedbackShoot, 1f);
                                rangedAct();
                            };
                        }
                        floatMenuOption.Label = text;
                        opts.Add(floatMenuOption);
                    }
                    Action meleeAct = FloatMenuOnVehicleUtility.GetMeleeAttackAction(pawn, attackTarg, out string str2);
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
                            FleckMaker.Static(attackTarg.Thing.DrawPos, attackTarg.Thing.BaseMap(), FleckDefOf.FeedbackMelee, 1f);
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
                    foreach (LocalTargetInfo carryTarget in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForCarry(pawn), true, null))
                    {
                        FloatMenuOption item;
                        if (!pawn.CanReach(carryTarget, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                        {
                            item = new FloatMenuOption("CannotCarry".Translate(carryTarget.Thing) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                        }
                        else
                        {
                            item = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Carry".Translate(carryTarget.Thing), delegate ()
                            {
                                carryTarget.Thing.SetForbidden(false, false);
                                Job job = JobMaker.MakeJob(VMF_DefOf.VMF_CarryDownedPawnDraftedAcrossMaps, carryTarget);
                                job.count = 1;
                                pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot), new JobTag?(JobTag.Misc), false);
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
                        foreach (LocalTargetInfo destTarget in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForDraftedCarryBed(carriedPawn, pawn, carriedPawn.GuestStatus), true, null))
                        {
                            FloatMenuOption item2;
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
                                    Job job = JobMaker.MakeJob(VMF_DefOf.VMF_TakeDownedPawnToBedDraftedAcrossMaps, pawn.carryTracker.CarriedThing, destTarget);
                                    job.count = 1;
                                    pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, null, null, exitSpot, enterSpot), new JobTag?(JobTag.Misc), false);
                                }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, destTarget, "ReservedBy", null);
                            }
                            opts.Add(item2);
                        }
                    }
                    if (carriedPawn.CanBeCaptured())
                    {
                        foreach (var localTargetInfo in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForDraftedCarryBed(carriedPawn, pawn, new GuestStatus?(GuestStatus.Prisoner)), true, null))
                        {
                            Building_Bed bed = (Building_Bed)localTargetInfo.Thing;
                            FloatMenuOption item3;
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
                                    Job job = JobMaker.MakeJob(VMF_DefOf.VMF_CarryToPrisonerBedDraftedAcrossMaps, pawn.carryTracker.CarriedThing, bed);
                                    job.count = 1;
                                    pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, null, null, exitSpot, enterSpot), new JobTag?(JobTag.Misc), false);
                                }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, bed, "ReservedBy", null);
                            }
                            opts.Add(item3);
                        }
                    }
                    CompHoldingPlatformTarget compHoldingPlatformTarget;
                    if (ModsConfig.AnomalyActive && carriedPawn.TryGetComp(out compHoldingPlatformTarget) && compHoldingPlatformTarget.CanBeCaptured)
                    {
                        foreach (LocalTargetInfo localTargetInfo2 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForBuilding(null), true, null))
                        {
                            if (localTargetInfo2.Thing.TryGetComp(out CompEntityHolder compEntityHolder) && compEntityHolder.Available)
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
                                        Job job = JobMaker.MakeJob(VMF_DefOf.VMF_CarryToEntityHolderAlreadyHoldingAcrossMaps, thing, pawn.carryTracker.CarriedThing);
                                        job.count = 1;
                                        pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, null, null, exitSpot, enterSpot), new JobTag?(JobTag.Misc), false);
                                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, thing, "ReservedBy", null);
                                }
                                opts.Add(item4);
                            }
                        }
                    }
                    foreach (LocalTargetInfo localTargetInfo3 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForDraftedCarryTransporter(carriedPawn), true, null))
                    {
                        Thing transporterThing = localTargetInfo3.Thing;
                        if (transporterThing != null)
                        {
                            CompTransporter compTransporter = transporterThing.TryGetComp<CompTransporter>();
                            if (compTransporter.Shuttle == null || compTransporter.Shuttle.IsAllowedNow(carriedPawn))
                            {
                                if (!pawn.CanReach(transporterThing, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                                {
                                    opts.Add(new FloatMenuOption("CannotPlaceIn".Translate(carriedPawn, transporterThing) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                                }
                                else if (compTransporter.Shuttle == null && !compTransporter.LeftToLoadContains(carriedPawn))
                                {
                                    opts.Add(new FloatMenuOption("CannotPlaceIn".Translate(carriedPawn, transporterThing) + ": " + "NotPartOfLaunchGroup".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                                }
                                else
                                {
                                    opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("PlaceIn".Translate(carriedPawn, transporterThing), delegate ()
                                    {
                                        if (!compTransporter.LoadingInProgressOrReadyToLaunch)
                                        {
                                            TransporterUtility.InitiateLoading(Gen.YieldSingle<CompTransporter>(compTransporter));
                                        }
                                        Job job = JobMaker.MakeJob(VMF_DefOf.VMF_HaulToTransporterAcrossMaps, carriedPawn, transporterThing);
                                        job.ignoreForbidden = true;
                                        job.count = 1;
                                        pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(null, null, exitSpot, enterSpot), new JobTag?(JobTag.Misc), false);
                                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, transporterThing, "ReservedBy", null));
                                }
                            }
                        }
                    }
                    foreach (LocalTargetInfo localTargetInfo4 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForDraftedCarryCryptosleepCasket(pawn), true, null))
                    {
                        Thing casket = localTargetInfo4.Thing;
                        pawn.CanReach(casket, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot);
                        TaggedString taggedString3 = "PlaceIn".Translate(carriedPawn, casket);
                        if (((Building_CryptosleepCasket)casket).HasAnyContents)
                        {
                            opts.Add(new FloatMenuOption("CannotPlaceIn".Translate(carriedPawn, casket) + ": " + "CryptosleepCasketOccupied".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                        }
                        else if (carriedPawn.IsQuestLodger())
                        {
                            opts.Add(new FloatMenuOption("CannotPlaceIn".Translate(carriedPawn, casket) + ": " + "CryptosleepCasketGuestsNotAllowed".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                        }
                        else if (carriedPawn.GetExtraHostFaction(null) != null)
                        {
                            opts.Add(new FloatMenuOption("CannotPlaceIn".Translate(carriedPawn, casket) + ": " + "CryptosleepCasketGuestPrisonersNotAllowed".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                        }
                        else
                        {
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(taggedString3, delegate ()
                            {
                                Job job = JobMaker.MakeJob(VMF_DefOf.VMF_CarryToCryptosleepCasketDraftedAcrossMaps, carriedPawn, casket);
                                job.count = 1;
                                job.playerForced = true;
                                pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, null, null, exitSpot, enterSpot), new JobTag?(JobTag.Misc), false);
                            }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, casket, "ReservedBy", null));
                        }
                    }
                }
                if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && !pawn.IsMutant)
                {
                    foreach (LocalTargetInfo localTargetInfo5 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForTend(pawn), true, null))
                    {
                        Pawn tendTarget = (Pawn)localTargetInfo5.Thing;
                        if (!tendTarget.health.HasHediffsNeedingTend(false))
                        {
                            opts.Add(new FloatMenuOption("CannotTend".Translate(tendTarget) + ": " + "TendingNotRequired".Translate(tendTarget), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                        }
                        else if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                        {
                            opts.Add(new FloatMenuOption("CannotTend".Translate(tendTarget) + ": " + "CannotPrioritizeWorkTypeDisabled".Translate(WorkTypeDefOf.Doctor.gerundLabel), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                        }
                        else if (!pawn.CanReach(tendTarget, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                        {
                            opts.Add(new FloatMenuOption("CannotTend".Translate(tendTarget) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                        }
                        else
                        {
                            Thing medicine = HealthAIAcrossMapsUtility.FindBestMedicine(pawn, tendTarget, true, out var exitSpot2, out var enterSpot2);
                            TaggedString taggedString4 = "Tend".Translate(tendTarget);
                            Action action = delegate ()
                            {
                                Job job = JobMaker.MakeJob(VMF_DefOf.VMF_TendPatientAcrossMaps, tendTarget, medicine);
                                job.count = 1;
                                job.draftedTend = true;
                                pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, null, null, exitSpot2, enterSpot2), new JobTag?(JobTag.Misc), false);
                            };
                            if (tendTarget == pawn && pawn.playerSettings != null && !pawn.playerSettings.selfTend)
                            {
                                action = null;
                                taggedString4 = "CannotGenericWorkCustom".Translate("Tend".Translate(tendTarget).ToString().UncapitalizeFirst()) + ": " + "SelfTendDisabled".Translate().CapitalizeFirst();
                            }
                            else if (tendTarget.InAggroMentalState && !tendTarget.health.hediffSet.HasHediff(HediffDefOf.Scaria, false))
                            {
                                action = null;
                                taggedString4 = "CannotGenericWorkCustom".Translate("Tend".Translate(tendTarget).ToString().UncapitalizeFirst()) + ": " + "PawnIsInAggroMentalState".Translate(tendTarget).CapitalizeFirst();
                            }
                            else if (medicine == null)
                            {
                                taggedString4 += " (" + "WithoutMedicine".Translate() + ")";
                            }
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(taggedString4, action, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, tendTarget, "ReservedBy", null));
                            if (medicine != null && action != null && pawn.CanReserve(tendTarget, tendTarget.Map, 1, -1, null, false) && tendTarget.Spawned)
                            {
                                opts.Add(new FloatMenuOption("Tend".Translate(tendTarget) + " (" + "WithoutMedicine".Translate() + ")", delegate ()
                                {
                                    Job job = JobMaker.MakeJob(VMF_DefOf.VMF_TendPatientAcrossMaps, tendTarget, null);
                                    job.count = 1;
                                    job.draftedTend = true;
                                    pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
                                }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                            }
                        }
                    }
                    foreach (LocalTargetInfo localTargetInfo6 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForHeldEntity(), false, null))
                    {
                        Building_HoldingPlatform holdingPlatform;
                        if ((holdingPlatform = (localTargetInfo6.Thing as Building_HoldingPlatform)) != null)
                        {
                            Pawn heldPawn = holdingPlatform.HeldPawn;
                            if (heldPawn != null)
                            {
                                if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                                {
                                    opts.Add(new FloatMenuOption("CannotTend".Translate(heldPawn) + ": " + "CannotPrioritizeWorkTypeDisabled".Translate(WorkTypeDefOf.Doctor.gerundLabel), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                                }
                                else if (!pawn.CanReach(holdingPlatform, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                                {
                                    opts.Add(new FloatMenuOption("CannotTend".Translate(heldPawn) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                                }
                                else if (HealthAIUtility.ShouldBeTendedNowByPlayer(heldPawn) && pawn.CanReserveAndReach(map, holdingPlatform, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, true, out exitSpot, out enterSpot))
                                {
                                    Thing medicine = HealthAIAcrossMapsUtility.FindBestMedicine(pawn, heldPawn, false, out var exitSpot2, out var enterSpot2);
                                    opts.Add(new FloatMenuOption("Tend".Translate(heldPawn.LabelShort), delegate ()
                                    {
                                        LocalTargetInfo targetA = holdingPlatform;
                                        Job job = JobMaker.MakeJob(VMF_DefOf.VMF_TendEntityAcrossmaps, targetA, medicine ?? LocalTargetInfo.Invalid);
                                        job.count = 1;
                                        job.draftedTend = true;
                                        pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot2, enterSpot2), new JobTag?(JobTag.Misc), false);
                                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                                }
                            }
                        }
                    }
                    if (pawn.skills != null && !pawn.skills.GetSkill(SkillDefOf.Construction).TotallyDisabled)
                    {
                        foreach (LocalTargetInfo localTargetInfo7 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForRepair(pawn), true, null))
                        {
                            Thing repairTarget = localTargetInfo7.Thing;
                            if (!pawn.CanReach(repairTarget, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                            {
                                opts.Add(new FloatMenuOption("CannotRepair".Translate(repairTarget) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                            }
                            else if (RepairUtility.PawnCanRepairNow(pawn, repairTarget))
                            {
                                FloatMenuOption item5 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("RepairThing".Translate(repairTarget), delegate ()
                                {
                                    pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.Repair, repairTarget)), new JobTag?(JobTag.Misc), true);
                                }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, repairTarget, "ReservedBy", null);
                                opts.Add(item5);
                            }
                        }
                    }
                }
            }
            FloatMenuMakerOnVehicle.AddJobGiverWorkOrders(clickPos, pawn, opts, true);
            FloatMenuOption floatMenuOption3 = FloatMenuMakerOnVehicle.GotoLocationOption(clickCell, pawn, suppressAutoTakeableGoto, map);
            if (floatMenuOption3 != null)
            {
                opts.Add(floatMenuOption3);
            }
        }

        private static void AddHumanlikeOrders(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
			IntVec3 clickCell;
			Map map;
			if (GenUIOnVehicle.vehicleForSelector != null)
			{
				clickCell = IntVec3.FromVector3(clickPos).ToVehicleMapCoord(GenUIOnVehicle.vehicleForSelector);
				map = GenUIOnVehicle.vehicleForSelector.VehicleMap;

            }
			else
			{
				clickCell = IntVec3.FromVector3(clickPos);
				map = pawn.BaseMap();
            }
            var targetParms = new TargetingParameters()
            {
                canTargetSelf = true,
                canTargetFires = true,
                canTargetItems = true,
                canTargetPlants = true,
                mapObjectTargetsMustBeAutoAttackable = false
            };
            var thingList = GenUIOnVehicle.TargetsAt(clickPos, targetParms, true, null).Select(t => t.Thing).ToList();
            foreach (var thing in thingList)
            {
                if (thing is Pawn pawn2)
                {
                    Lord lord = pawn2.GetLord();
                    if (lord?.CurLordToil != null)
                    {
                        IEnumerable<FloatMenuOption> enumerable = lord.CurLordToil.ExtraFloatMenuOptions(pawn2, pawn);
                        if (enumerable != null)
                        {
                            foreach (FloatMenuOption item10 in enumerable)
                            {
                                opts.Add(item10);
                            }
                        }
                    }
                }
            }
            if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
                foreach (LocalTargetInfo dest in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForArrest(pawn), true, null))
                {
                    bool flag = dest.Thing is Pawn pawn1 && pawn1.IsWildMan();
                    if (pawn.Drafted || flag)
					{
                        if (dest.Thing is Pawn pawn2 && (pawn.InSameExtraFaction(pawn2, ExtraFactionType.HomeFaction, null) || pawn.InSameExtraFaction(pawn2, ExtraFactionType.MiniFaction, null)))
						{
                            opts.Add(new FloatMenuOption("CannotArrest".Translate() + ": " + "SameFaction".Translate(pawn2), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                        }

                        else if (!pawn.CanReach(dest, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
						{
                            opts.Add(new FloatMenuOption("CannotArrest".Translate() + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                        }

                        else
                        {
                            Pawn pTarg = (Pawn)dest.Thing;
                            void action()
                            {
                                Building_Bed building_Bed2 = RestUtilityOnVehicle.FindBedFor(pTarg, pawn, false, false, new GuestStatus?(GuestStatus.Prisoner), out var exitSpot2, out var enterSpot2) ?? RestUtilityOnVehicle.FindBedFor(pTarg, pawn, false, true, new GuestStatus?(GuestStatus.Prisoner), out exitSpot2, out enterSpot2);
                                if (building_Bed2 == null)
                                {
                                    Messages.Message("CannotArrest".Translate() + ": " + "NoPrisonerBed".Translate(), pTarg, MessageTypeDefOf.RejectInput, false);
                                    return;
                                }
                                Job job = JobMaker.MakeJob(VMF_DefOf.VMF_ArrestAcrossMaps, pTarg, building_Bed2);
                                job.count = 1;
                                pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2), new JobTag?(JobTag.Misc), false);
                                if (pTarg.Faction != null && ((pTarg.Faction != Faction.OfPlayer && !pTarg.Faction.Hidden) || pTarg.IsQuestLodger()))
                                {
                                    TutorUtility.DoModalDialogIfNotKnown(ConceptDefOf.ArrestingCreatesEnemies, new string[]
                                    {
                                        pTarg.GetAcceptArrestChance(pawn).ToStringPercent()
                                    });
                                }
                            }
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("TryToArrest".Translate(dest.Thing.LabelCap, dest.Thing, pTarg.GetAcceptArrestChance(pawn).ToStringPercent()), action, MenuOptionPriority.High, null, dest.Thing, 0f, null, null, true, 0), pawn, pTarg, "ReservedBy", null));
                        }
                    }
                }
            }
            foreach (Thing t4 in thingList)
			{
                Thing t = t4;
                if (!t.def.IsDrug)
                {
                    Pawn_NeedsTracker needs = pawn.needs;
                    if (needs?.food == null)
                    {
                        continue;
                    }
                }
                if (t.def.ingestible != null && t.def.ingestible.showIngestFloatOption && pawn.RaceProps.CanEverEat(t) && t.IngestibleNow)
				{
                    string text;
                    if (t.def.ingestible.ingestCommandString.NullOrEmpty())
                    {
                        text = "ConsumeThing".Translate(t.LabelShort, t);
                    }
                    else
                    {
                        text = t.def.ingestible.ingestCommandString.Formatted(t.LabelShort);
                    }
                    if (!t.IsSociallyProper(pawn))
                    {
                        text = text + ": " + "ReservedForPrisoners".Translate().CapitalizeFirst();
                    }
                    else if (FoodUtility.MoodFromIngesting(pawn, t, t.def) < 0f)
                    {
                        text = string.Format("{0}: ({1})", text, "WarningFoodDisliked".Translate());
                    }
                    FloatMenuOption floatMenuOption;
                    if ((!t.def.IsDrug || !ModsConfig.IdeologyActive || new HistoryEvent(HistoryEventDefOf.IngestedDrug, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo(out floatMenuOption, text) || PawnUtility.CanTakeDrugForDependency(pawn, t.def)) && (!t.def.IsNonMedicalDrug || !ModsConfig.IdeologyActive || new HistoryEvent(HistoryEventDefOf.IngestedRecreationalDrug, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo(out floatMenuOption, text) || PawnUtility.CanTakeDrugForDependency(pawn, t.def)) && (!t.def.IsDrug || !ModsConfig.IdeologyActive || t.def.ingestible.drugCategory != DrugCategory.Hard || new HistoryEvent(HistoryEventDefOf.IngestedHardDrug, pawn.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo(out floatMenuOption, text)))
                    {
                        if (t.def.IsNonMedicalDrug && !pawn.CanTakeDrug(t.def))
						{
                            floatMenuOption = new FloatMenuOption(text + ": " + TraitDefOf.DrugDesire.DataAtDegree(-1).GetLabelCapFor(pawn), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                        }

                        else if (FoodUtility.InappropriateForTitle(t.def, pawn, true))
                        {
                            floatMenuOption = new FloatMenuOption(text + ": " + "FoodBelowTitleRequirements".Translate(pawn.royalty.MostSeniorTitle.def.GetLabelFor(pawn).CapitalizeFirst()).CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                        }
                        else if (!pawn.CanReach(t, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
						{
                            floatMenuOption = new FloatMenuOption(text + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                        }

                        else
                        {
                            MenuOptionPriority priority = (t is Corpse) ? MenuOptionPriority.Low : MenuOptionPriority.Default;
                            bool maxAmountToPickup = FoodUtility.GetMaxAmountToPickup(t, pawn, FoodUtility.WillIngestStackCountOf(pawn, t.def, FoodUtility.NutritionForEater(pawn, t))) != 0;
                            floatMenuOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, delegate ()
                            {
                                int maxAmountToPickup2 = FoodUtility.GetMaxAmountToPickup(t, pawn, FoodUtility.WillIngestStackCountOf(pawn, t.def, FoodUtility.NutritionForEater(pawn, t)));
                                if (maxAmountToPickup2 == 0)
                                {
                                    return;
                                }
                                t.SetForbidden(false, true);
                                Job job = JobMaker.MakeJob(JobDefOf.Ingest, t);
                                job.count = maxAmountToPickup2;
                                pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
                            }, priority, null, null, 0f, null, null, true, 0), pawn, t, "ReservedBy", null);
                            if (!maxAmountToPickup)
                            {
                                floatMenuOption.action = null;
                            }
                        }
                    }
                    opts.Add(floatMenuOption);
                }
            }
            foreach (LocalTargetInfo dest2 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForQuestPawnsWhoWillJoinColony(pawn), true, null))
            {
                Pawn toHelpPawn = (Pawn)dest2.Thing;
                FloatMenuOption item2;
                if (!pawn.CanReach(dest2, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, toHelpPawn.Map, out var exitSpot, out var enterSpot))
				{
                    item2 = new FloatMenuOption("CannotGoNoPath".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                }

                else
                {
                    item2 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(toHelpPawn.IsPrisoner ? "FreePrisoner".Translate() : "OfferHelp".Translate(), delegate ()
                    {
                        pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.OfferHelp, toHelpPawn)), new JobTag?(JobTag.Misc), true);
                    }, MenuOptionPriority.RescueOrCapture, null, toHelpPawn, 0f, null, null, true, 0), pawn, toHelpPawn, "ReservedBy", null);
                }
                opts.Add(item2);
            }
            if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
                foreach (Thing thing10 in thingList)
                {
                    Corpse corpse;
                    if ((corpse = (thing10 as Corpse)) != null && corpse.IsInValidStorage())
                    {
                        StoragePriority priority2 = StoreUtility.CurrentHaulDestinationOf(corpse).GetStoreSettings().Priority;
                        Building_Grave grave;
                        if (pawn.CanReach(corpse, PathEndMode.ClosestTouch, Danger.None, false, false, TraverseMode.ByPawn, corpse.Map, out var exitSpot1, out var enterSpot1) &&
							StoreAcrossMapsUtility.TryFindBestBetterNonSlotGroupStorageFor(corpse, pawn, map, priority2, Faction.OfPlayer, out var haulDestination, true, true, out var exitSpot2, out var enterSpot2) && haulDestination.GetStoreSettings().Priority == priority2 && (grave = (haulDestination as Building_Grave)) != null)
                        {
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("PrioritizeGeneric".Translate("Burying".Translate(), corpse.Label).CapitalizeFirst(), delegate ()
                            {
                                pawn.jobs.TryTakeOrderedJob(HaulAIAcrossMapsUtility.HaulToContainerJob(pawn, corpse, grave, exitSpot1, enterSpot1, exitSpot2, enterSpot2), new JobTag?(JobTag.Misc), true);
                            }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(corpse), "ReservedBy", null));
                        }
                    }
                }
                foreach (Thing thing2 in thingList)
                {
                    if (thing2 is Corpse corpse)
                    {
                        var canReach = pawn.CanReach(corpse, PathEndMode.ClosestTouch, Danger.None, false, false, TraverseMode.ByPawn, corpse.Map, out var exitSpot1, out var enterSpot1);
                        Building_GibbetCage cage = FindBuildingUtility.FindGibbetCageFor(corpse, pawn, false, out var exitSpot2, out var enterSpot2);
                        if (canReach && cage != null)
                        {
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("PlaceIn".Translate(corpse, cage), delegate ()
                            {
                                pawn.jobs.TryTakeOrderedJob(HaulAIAcrossMapsUtility.HaulToContainerJob(pawn, corpse, cage, exitSpot1, enterSpot1, exitSpot2, enterSpot2), new JobTag?(JobTag.Misc), true);
                            }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(corpse), "ReservedBy", null));
                        }
                        if (ModsConfig.BiotechActive && canReach && corpse.InnerPawn.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant, false))
                        {
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Extract".Translate() + " " + HediffDefOf.MechlinkImplant.label, delegate ()
                            {
                                Job job = JobMaker.MakeJob(JobDefOf.RemoveMechlink, corpse);
                                pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot1, enterSpot1, job), new JobTag?(JobTag.Misc), false);
                            }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(corpse), "ReservedBy", null));
                        }
                    }
                }
                foreach (LocalTargetInfo localTargetInfo in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForRescue(pawn), true, null))
                {
                    Pawn victim = (Pawn)localTargetInfo.Thing;
                    if (HealthAIAcrossMapsUtility.CanRescueNow(pawn, victim, true, out var exitSpot, out var enterSpot) && !victim.mindState.WillJoinColonyIfRescued)
                    {
                        if (!victim.IsPrisonerOfColony && !victim.IsSlaveOfColony && !victim.IsColonyMech)
                        {
                            bool isBaby = ChildcareUtility.CanSuckle(victim, out ChildcareUtility.BreastfeedFailReason? breastfeedFailReason);
                            if ((victim.Faction == Faction.OfPlayer || victim.Faction == null || !victim.Faction.HostileTo(Faction.OfPlayer)) | isBaby)
                            {
                                FloatMenuOption floatMenuOption2 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption((HealthAIUtility.ShouldSeekMedicalRest(victim) || !victim.ageTracker.CurLifeStage.alwaysDowned) ? "Rescue".Translate(victim.LabelCap, victim) : "PutSomewhereSafe".Translate(victim.LabelCap, victim), delegate ()
                                {
                                    if (isBaby)
                                    {
                                        Job job2 = JobMaker.MakeJob(VMF_DefOf.VMF_BringBabyToSafetyAcrossMaps, victim);
                                        job2.count = 1;
                                        pawn.jobs.TryTakeOrderedJob(job2.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot), new JobTag?(JobTag.Misc), true);
                                        return;
                                    }
                                    Building_Bed building_Bed2 = RestUtilityOnVehicle.FindBedFor(victim, pawn, false, false, null, out var exitSpot2, out var enterSpot2);
                                    if (building_Bed2 == null)
                                    {
                                        building_Bed2 = RestUtilityOnVehicle.FindBedFor(victim, pawn, false, true, null, out exitSpot2, out enterSpot2);
                                    }
                                    if (building_Bed2 == null)
                                    {
                                        string t5;
                                        if (victim.RaceProps.Animal)
                                        {
                                            t5 = "NoAnimalBed".Translate();
                                        }
                                        else
                                        {
                                            t5 = "NoNonPrisonerBed".Translate();
                                        }
                                        Messages.Message("CannotRescue".Translate() + ": " + t5, victim, MessageTypeDefOf.RejectInput, false);
                                        return;
                                    }
                                    Job job = JobMaker.MakeJob(VMF_DefOf.VMF_RescueAcrossMaps, victim, building_Bed2);
                                    job.count = 1;
                                    pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2), new JobTag?(JobTag.Misc), true);
                                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Rescuing, KnowledgeAmount.Total);
                                }, MenuOptionPriority.RescueOrCapture, null, victim, 0f, null, null, true, 0), pawn, victim, "ReservedBy", null);
                                if (!isBaby)
                                {
                                    string key = victim.RaceProps.Animal ? "NoAnimalBed" : "NoNonPrisonerBed";
                                    string cannot = string.Format("{0}: {1}", "CannotRescue".Translate(), key.Translate().CapitalizeFirst());
                                    FloatMenuMakerOnVehicle.ValidateTakeToBedOption(pawn, victim, floatMenuOption2, cannot, null);
                                }
                                opts.Add(floatMenuOption2);
                            }
                        }
                        if (victim.IsSlaveOfColony && !victim.InMentalState)
                        {
                            FloatMenuOption floatMenuOption3 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("ReturnToSlaveBed".Translate(), delegate ()
                            {
                                Building_Bed building_Bed2 = RestUtilityOnVehicle.FindBedFor(victim, pawn, false, false, new GuestStatus?(GuestStatus.Slave), out var exitSpot2, out var enterSpot2)
                                ?? RestUtilityOnVehicle.FindBedFor(victim, pawn, false, true, new GuestStatus?(GuestStatus.Slave), out exitSpot2, out enterSpot2);
                                if (building_Bed2 == null)
                                {
                                    Messages.Message(string.Format("{0}: {1}", "CannotRescue".Translate(), "NoSlaveBed".Translate()), victim, MessageTypeDefOf.RejectInput, false);
                                    return;
                                }
                                Job job = JobMaker.MakeJob(VMF_DefOf.VMF_RescueAcrossMaps, victim, building_Bed2);
                                job.count = 1;
                                pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2), new JobTag?(JobTag.Misc), true);
                                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Rescuing, KnowledgeAmount.Total);
                            }, MenuOptionPriority.RescueOrCapture, null, victim, 0f, null, null, true, 0), pawn, victim, "ReservedBy", null);
                            string cannot2 = string.Format("{0}: {1}", "CannotRescue".Translate(), "NoSlaveBed".Translate());
                            FloatMenuMakerOnVehicle.ValidateTakeToBedOption(pawn, victim, floatMenuOption3, cannot2, new GuestStatus?(GuestStatus.Slave));
                            opts.Add(floatMenuOption3);
                        }
                        if (victim.CanBeCaptured())
                        {
                            TaggedString taggedString = "Capture".Translate(victim.LabelCap, victim);
                            if (!victim.guest.Recruitable)
                            {
                                taggedString += " (" + "Unrecruitable".Translate() + ")";
                            }
                            if (victim.Faction != null && victim.Faction != Faction.OfPlayer && !victim.Faction.Hidden && !victim.Faction.HostileTo(Faction.OfPlayer) && !victim.IsPrisonerOfColony)
                            {
                                taggedString += ": " + "AngersFaction".Translate().CapitalizeFirst();
                            }
                            FloatMenuOption floatMenuOption4 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(taggedString, delegate ()
                            {
                                Building_Bed building_Bed2 = RestUtilityOnVehicle.FindBedFor(victim, pawn, false, false, new GuestStatus?(GuestStatus.Prisoner), out var exitSpot2, out var enterSpot2)
                                ?? RestUtilityOnVehicle.FindBedFor(victim, pawn, false, true, new GuestStatus?(GuestStatus.Prisoner), out exitSpot2, out enterSpot2);
                                if (building_Bed2 == null)
                                {
                                    Messages.Message("CannotCapture".Translate() + ": " + "NoPrisonerBed".Translate(), victim, MessageTypeDefOf.RejectInput, false);
                                    return;
                                }
                                Job job = JobMaker.MakeJob(VMF_DefOf.VMF_CaptureAcrossMaps, victim, building_Bed2);
                                job.count = 1;
								pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2), new JobTag?(JobTag.Misc), true);
                                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Capturing, KnowledgeAmount.Total);
                                if (victim.Faction != null && victim.Faction != Faction.OfPlayer && !victim.Faction.Hidden && !victim.Faction.HostileTo(Faction.OfPlayer) && !victim.IsPrisonerOfColony)
                                {
                                    Messages.Message("MessageCapturingWillAngerFaction".Translate(victim.Named("PAWN")).AdjustedFor(victim, "PAWN", true), victim, MessageTypeDefOf.CautionInput, false);
                                }
                            }, MenuOptionPriority.RescueOrCapture, null, victim, 0f, null, null, true, 0), pawn, victim, "ReservedBy", null);
                            string cannot3 = string.Format("{0}: {1}", "CannotCapture".Translate(), "NoPrisonerBed".Translate());
                            FloatMenuMakerOnVehicle.ValidateTakeToBedOption(pawn, victim, floatMenuOption4, cannot3, new GuestStatus?(GuestStatus.Prisoner));
                            opts.Add(floatMenuOption4);
                        }
                    }
                }
                foreach (LocalTargetInfo localTargetInfo2 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForRescue(pawn), true, null))
                {
                    LocalTargetInfo localTargetInfo3 = localTargetInfo2;
                    Pawn victim = (Pawn)localTargetInfo3.Thing;
                    if (victim.Downed && pawn.CanReserveAndReach(victim.Map, victim, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, true, out var exitSpot, out var enterSpot) && Building_CryptosleepCasket.FindCryptosleepCasketFor(victim, pawn, true) != null)
					{
                        string text2 = "CarryToCryptosleepCasket".Translate(localTargetInfo3.Thing.LabelCap, localTargetInfo3.Thing);
                        void action2()
                        {
                            Building_CryptosleepCasket building_CryptosleepCasket = FindBuildingUtility.FindCryptosleepCasketFor(victim, pawn, false, out var exitSpot2, out var enterSpot2)
                            ?? FindBuildingUtility.FindCryptosleepCasketFor(victim, pawn, true, out exitSpot2, out enterSpot2);
                            if (building_CryptosleepCasket == null)
                            {
                                Messages.Message("CannotCarryToCryptosleepCasket".Translate() + ": " + "NoCryptosleepCasket".Translate(), victim, MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            Job job = JobMaker.MakeJob(VMF_DefOf.VMF_CarryToCryptosleepCasketAcrossMaps, victim, building_CryptosleepCasket);
                            job.count = 1;
                            pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2), new JobTag?(JobTag.Misc), true);
                        }
                        if (victim.IsQuestLodger())
                        {
                            text2 += " (" + "CryptosleepCasketGuestsNotAllowed".Translate() + ")";
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text2, null, MenuOptionPriority.Default, null, victim, 0f, null, null, true, 0), pawn, victim, "ReservedBy", null));
                        }
                        else if (victim.GetExtraHostFaction(null) != null)
                        {
                            text2 += " (" + "CryptosleepCasketGuestPrisonersNotAllowed".Translate() + ")";
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text2, null, MenuOptionPriority.Default, null, victim, 0f, null, null, true, 0), pawn, victim, "ReservedBy", null));
                        }
                        else
                        {
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text2, action2, MenuOptionPriority.Default, null, victim, 0f, null, null, true, 0), pawn, victim, "ReservedBy", null));
                        }
                    }
                }
                if (ModsConfig.AnomalyActive && pawn.ageTracker.AgeBiologicalYears >= 10)
				{
                    foreach (LocalTargetInfo localTargetInfo4 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForEntityCapture(), true, null))
                    {
                        Thing studyTarget = localTargetInfo4.Thing;
                        CompHoldingPlatformTarget holdComp = studyTarget.TryGetComp<CompHoldingPlatformTarget>();
                        if (holdComp != null && holdComp.StudiedAtHoldingPlatform && holdComp.CanBeCaptured)
                        {
                            if (!pawn.CanReserveAndReach(studyTarget.Map ,studyTarget, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, true, out var exitSpot, out var enterSpot))
							{
                                opts.Add(new FloatMenuOption("CannotGenericWorkCustom".Translate("CaptureLower".Translate(studyTarget)) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                            }
                            else
                            {
                                IEnumerable<Building_HoldingPlatform> source = pawn.Map.BaseMapAndVehicleMaps().SelectMany(m => m.listerBuildings.AllBuildingsColonistOfClass<Building_HoldingPlatform>());
                                bool predicate(Building_HoldingPlatform x) => !x.Occupied && pawn.CanReserveAndReach(x.Map, x, PathEndMode.Touch, Danger.Deadly, 1, -1, null, false, out _, out _);
                                IEnumerable<Building_HoldingPlatform> enumerable2 = source.Where(predicate);
                                Thing building = GenClosestOnVehicle.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, enumerable2, PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Some, TraverseMode.ByPawn, false, false, false), 9999f, null, delegate (Thing t)
                                {
                                    CompEntityHolder compEntityHolder = t.TryGetComp<CompEntityHolder>();
                                    if (compEntityHolder != null && compEntityHolder.ContainmentStrength >= studyTarget.GetStatValue(StatDefOf.MinimumContainmentStrength, true, -1))
                                    {
                                        return compEntityHolder.ContainmentStrength / Mathf.Max(studyTarget.PositionHeld.DistanceTo(t.Position), 1f);
                                    }
                                    return 0f;
                                }, false, out var exitSpot2, out var enterSpot2);
                                if (building != null)
                                {
                                    opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Capture".Translate(studyTarget.Label, studyTarget), delegate ()
                                    {
                                        if (!ContainmentUtility.SafeContainerExistsFor(studyTarget))
                                        {
                                            Messages.Message("MessageNoRoomWithMinimumContainmentStrength".Translate(studyTarget.Label), MessageTypeDefOf.ThreatSmall, true);
                                        }
                                        holdComp.targetHolder = building;
                                        Job job = JobMaker.MakeJob(VMF_DefOf.VMF_CarryToEntityHolderAcrossMaps, building, studyTarget);
                                        job.count = 1;
                                        pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot2, enterSpot2, exitSpot, enterSpot), new JobTag?(JobTag.Misc), false);
                                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, studyTarget, "ReservedBy", null));
                                    if (enumerable2.Count<Building_HoldingPlatform>() > 1)
                                    {
                                        opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Capture".Translate(studyTarget.Label, studyTarget) + " (" + "ChooseEntityHolder".Translate() + "...)", delegate ()
                                        {
                                            StudyUtilityOnVehicle.TargetHoldingPlatformForEntity(pawn, studyTarget, exitSpot, enterSpot, false, null);
                                        }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, studyTarget, "ReservedBy", null));
                                    }
                                }
                                else
                                {
                                    opts.Add(new FloatMenuOption("CannotGenericWorkCustom".Translate("CaptureLower".Translate(studyTarget)) + ": " + "NoHoldingPlatformsAvailable".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                                }
                            }
                        }
                    }
                    foreach (LocalTargetInfo localTargetInfo5 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForHeldEntity(), true, null))
                    {
                        Building_HoldingPlatform holdingPlatform;
                        if ((holdingPlatform = (localTargetInfo5.Thing as Building_HoldingPlatform)) != null)
                        {
                            Pawn heldPawn = holdingPlatform.HeldPawn;
                            if (heldPawn != null && pawn.CanReserveAndReach(holdingPlatform.Map, holdingPlatform, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, true, out var exitSpot, out var enterSpot))
							{
                                IntVec3 position = pawn.Position;
                                IEnumerable<Thing> searchSet = pawn.Map.BaseMapAndVehicleMaps().SelectMany(m => m.listerBuildings.AllBuildingsColonistOfClass<Building_HoldingPlatform>());
                                PathEndMode peMode = PathEndMode.ClosestTouch;
                                TraverseParms traverseParams = TraverseParms.For(pawn, Danger.Some, TraverseMode.ByPawn, false, false, false);
                                float maxDistance = 9999f;
                                bool validator(Thing b)
                                {
                                    Building_HoldingPlatform building_HoldingPlatform;
                                    return (building_HoldingPlatform = (b as Building_HoldingPlatform)) != null && !building_HoldingPlatform.Occupied && pawn.CanReserve(building_HoldingPlatform, building_HoldingPlatform.Map, 1, -1, null, false);
                                }
                                if (GenClosestOnVehicle.ClosestThing_Global_Reachable(position, pawn.Map, searchSet, peMode, traverseParams, maxDistance, validator, delegate (Thing t)
                                {
                                    CompEntityHolder compEntityHolder = t.TryGetComp<CompEntityHolder>();
                                    if (compEntityHolder != null && compEntityHolder.ContainmentStrength >= heldPawn.GetStatValue(StatDefOf.MinimumContainmentStrength, true, -1))
                                    {
                                        return compEntityHolder.ContainmentStrength / Mathf.Max(heldPawn.PositionHeldOnBaseMap().DistanceTo(t.PositionOnBaseMap()), 1f);
                                    }
                                    return 0f;
                                }, false, out _, out _) != null)
                                {
                                    opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("TransferEntity".Translate(heldPawn) + " (" + "ChooseEntityHolder".Translate() + "...)", delegate ()
                                    {
                                        StudyUtilityOnVehicle.TargetHoldingPlatformForEntity(pawn, heldPawn, exitSpot, enterSpot, true, holdingPlatform);
                                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, holdingPlatform, "ReservedBy", null));
                                }
                            }
                        }
                    }
                }
                if (ModsConfig.IdeologyActive)
                {
                    foreach (LocalTargetInfo localTargetInfo6 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForCarryToBiosculpterPod(pawn), true, null))
                    {
                        Pawn pawn3 = (Pawn)localTargetInfo6.Thing;
                        if ((pawn3.IsColonist && pawn3.Downed) || pawn3.IsPrisonerOfColony)
                        {
                            CompBiosculpterPod.AddCarryToPodJobs(opts, pawn, pawn3);
                        }
                    }
                }
                if (ModsConfig.RoyaltyActive)
                {
                    foreach (LocalTargetInfo localTargetInfo7 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForShuttle(pawn), true, null))
                    {
                        LocalTargetInfo localTargetInfo8 = localTargetInfo7;
                        var victim = (Pawn)localTargetInfo8.Thing;
                        if (victim.Spawned)
						{
							bool IsValidShuttle(Thing thing) => thing.TryGetComp<CompShuttle>(out var comp) && comp.IsAllowedNow(thing);
                            var shuttleThing = GenClosestOnVehicle.ClosestThingReachable(victim.Position, victim.Map, ThingRequest.ForDef(ThingDefOf.Shuttle), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, new Predicate<Thing>(IsValidShuttle), null, 0, -1, false, RegionType.Set_Passable, false, false, out var exitSpot, out var enterSpot);
                            if (shuttleThing != null)
							{
                                if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling))
								{
                                    opts.Add(new FloatMenuOption("CannotLoadIntoShuttle".Translate(shuttleThing) + ": " + "Incapable".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                                }

                                else if (pawn.CanReserveAndReach(victim.Map, victim, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, true, out var exitSpot2, out var enterSpot2))
								{
                                    opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("CarryToShuttle".Translate(localTargetInfo8.Thing), () =>
									{
                                        var comp = shuttleThing.TryGetComp<CompShuttle>();
                                        if (!comp.LoadingInProgressOrReadyToLaunch)
                                        {
                                            Gen.YieldSingle<CompTransporter>(comp.Transporter);
                                        }
										var job = JobMaker.MakeJob(VMF_DefOf.VMF_HaulToTransporterAcrossMaps, victim, shuttleThing);
										job.ignoreForbidden = true;
										job.count = 1;
										pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2), JobTag.Misc, false);

                                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, victim, "ReservedBy", null));
                                }
                            }
                        }
                    }
                }
                if (ModsConfig.IdeologyActive)
                {
                    foreach(var thing in thingList)
					{ 
						CompHackable compHackable = thing.TryGetComp<CompHackable>();
						if (compHackable != null)
						{
							if (compHackable.IsHacked)
							{
								opts.Add(new FloatMenuOption("CannotHack".Translate(thing.Label) + ": " + "AlreadyHacked".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else if (!HackUtility.IsCapableOfHacking(pawn))
							{
								opts.Add(new FloatMenuOption("CannotHack".Translate(thing.Label) + ": " + "IncapableOfHacking".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else if (!pawn.CanReach(thing, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, thing.Map, out var exitSpot, out var enterSpot))
							{
								opts.Add(new FloatMenuOption("CannotHack".Translate(thing.Label) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else if (thing.def == ThingDefOf.AncientEnemyTerminal)
							{
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Hack".Translate(thing.Label), delegate ()
								{
									WindowStack windowStack = Find.WindowStack;
									TaggedString text6 = "ConfirmHackEnenyTerminal".Translate(ThingDefOf.AncientEnemyTerminal.label);
									Action confirmedAct = () =>
									{
                                        pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.Hack, thing)), new JobTag?(JobTag.Misc), true);
									};
									windowStack.Add(Dialog_MessageBox.CreateConfirmation(text6, confirmedAct, false, null, WindowLayer.Dialog));
								}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(thing), "ReservedBy", null));
							}
							else
							{
								TaggedString taggedString2 = (thing.def == ThingDefOf.AncientCommsConsole) ? "Hack".Translate("ToDropSupplies".Translate()) : "Hack".Translate(thing.Label);
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(taggedString2, delegate ()
                                {
                                    pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.Hack, thing)), new JobTag?(JobTag.Misc), true);
								}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(thing), "ReservedBy", null));
							}
						}
                    }
                    foreach(var thing in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForBuilding(ThingDefOf.ArchonexusCore), false, null))
                    {
						if (!pawn.CanReach(thing, PathEndMode.InteractionCell, Danger.Deadly, false, false, TraverseMode.ByPawn, thing.Thing.Map, out var exitSpot, out var enterSpot))
						{
							opts.Add(new FloatMenuOption("CannotInvoke".Translate("Power".Translate()) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}

						else if (!((Building_ArchonexusCore)((Thing)thing)).CanActivateNow)
						{
							opts.Add(new FloatMenuOption("CannotInvoke".Translate("Power".Translate()) + ": " + "AlreadyInvoked".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}
						else
						{
							opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Invoke".Translate("Power".Translate()), delegate ()
                            {
                                pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.ActivateArchonexusCore, thing)), new JobTag?(JobTag.Misc), true);
							}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, thing, "ReservedBy", null));
						}
                    }
                }
                if (ModsConfig.IdeologyActive)
                {
					foreach (var thing in thingList)
					{
						CompRelicContainer container = thing.TryGetComp<CompRelicContainer>();
						if (container != null && pawn.CanReach(thing, PathEndMode.ClosestTouch, Danger.None, false, false, TraverseMode.ByPawn, thing.Map, out var exitSpot, out var enterSpot))
						{
							if (container.Full)
							{
								string text3 = "ExtractRelic".Translate(container.ContainedThing.Label);
                                if (!StoreAcrossMapsUtility.TryFindBestBetterStorageFor(container.ContainedThing, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out var c, out var haulDestination2, true, out var exitSpot2, out var enterSpot2))
                                {
                                    opts.Add(new FloatMenuOption(text3 + " (" + HaulAIUtility.NoEmptyPlaceLowerTrans + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
                                }
                                else
                                {
                                    Job job = JobMaker.MakeJob(VMF_DefOf.VMF_ExtractRelicAcrossMaps, thing, container.ContainedThing, c);
                                    job.count = 1;
                                    opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text3, delegate ()
                                    {
                                        pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2), new JobTag?(JobTag.Misc), true);
                                    }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(thing), "ReservedBy", null));
                                }
                            }
							else
							{
								var baseMap = pawn.BaseMap();
                                IEnumerable<Thing> allThings = map.BaseMapAndVehicleMaps().SelectMany(m => m.listerThings.AllThings);
                                var exitSpot2 = TargetInfo.Invalid;
                                var enterSpot2 = TargetInfo.Invalid;
                                bool predicate2(Thing x) => CompRelicContainer.IsRelic(x) && pawn.CanReach(x, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, x.Map, out exitSpot2, out enterSpot2);
                                IEnumerable<Thing> enumerable3 = allThings.Where(predicate2);
								if (!enumerable3.Any<Thing>())
								{
									opts.Add(new FloatMenuOption("NoRelicToInstall".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
								}
								else
								{
									foreach (Thing thing3 in enumerable3)
									{
                                        if (ReachabilityUtilityOnVehicle.CanReach(thing3.Map, thing3.Position, thing, PathEndMode.ClosestTouch, TraverseParms.For(pawn), thing.Map, out var exitSpot5, out var enterSpot5))
                                        {
                                            Job job = JobMaker.MakeJob(VMF_DefOf.VMF_InstallRelicAcrossMaps, thing3, thing, thing.InteractionCell);
                                            job.count = 1;
                                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("InstallRelic".Translate(thing3.Label), delegate ()
                                            {
                                                pawn.jobs.TryTakeOrderedJob(job.SetSpotsToJobAcrossMaps(pawn, exitSpot2, exitSpot2, exitSpot5, enterSpot5), new JobTag?(JobTag.Misc), false);
                                            }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(thing), "ReservedBy", null));
                                        }
									}
								}
							}
							if (!pawn.BaseMap().IsPlayerHome && !pawn.IsFormingCaravan() && container.Full)
							{
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("ExtractRelicToInventory".Translate(container.ContainedThing.Label, 300.ToStringTicksToPeriod(true, false, true, true, false)), delegate ()
								{
									Job job = JobMaker.MakeJob(JobDefOf.ExtractToInventory, thing, container.ContainedThing, thing.InteractionCell);
									job.count = 1;
									pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
								}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(thing), "ReservedBy", null));
							}
						}
					}
					foreach (Thing thing4 in thingList)
                    {
                        if (CompRelicContainer.IsRelic(thing4) && pawn.CanReach(thing4, PathEndMode.ClosestTouch, Danger.None, false, false, TraverseMode.ByPawn, thing4.Map, out var exitSpot, out var enterSpot))
                        {
                            var baseMap = thing4.BaseMap();
                            IEnumerable<Thing> enumerable4 = from x in map.BaseMapAndVehicleMaps().SelectMany(m => m.listerThings.ThingsOfDef(ThingDefOf.Reliquary))
							where x.TryGetComp<CompRelicContainer>().ContainedThing == null
							select x;
							IntVec3 position2 = thing4.Position;
							Map map2 = thing4.Map;
							IEnumerable<Thing> searchSet2 = enumerable4;
							PathEndMode peMode2 = PathEndMode.Touch;
							TraverseParms traverseParams2 = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false);
							float maxDistance2 = 9999f;
							Predicate<Thing> validator2 = ((Thing t) => pawn.CanReserve(t, t.Map, 1, -1, null, false));
							Thing thing5 = GenClosestOnVehicle.ClosestThing_Global_Reachable(position2, map2, searchSet2, peMode2, traverseParams2, maxDistance2, validator2, null, false, out var exitSpot2, out var enterSpot2);
							if (thing5 == null)
							{
								opts.Add(new FloatMenuOption("InstallInReliquary".Translate() + " (" + "NoEmptyReliquary".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else
							{
								Job job = JobMaker.MakeJob(VMF_DefOf.VMF_InstallRelicAcrossMaps, thing4, thing5, thing5.InteractionCell);
								job.count = 1;
                                job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2);
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("InstallInReliquary".Translate(), delegate()
								{
									pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
								}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(thing4), "ReservedBy", null));
							}
						}
					}
				}
				if (ModsConfig.BiotechActive && MechanitorUtility.IsMechanitor(pawn))
				{
					foreach(var thing in thingList)
					{
						if (thing is Pawn mech && mech.IsColonyMech)
						{
							var canReach = pawn.CanReach(mech, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, mech.Map, out var exitSpot, out var enterSpot);

                            if (mech.GetOverseer() != pawn)
							{
								if (!canReach)
								{
									opts.Add(new FloatMenuOption("CannotControlMech".Translate(mech.LabelShort) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
								}
								else if (!MechanitorUtility.CanControlMech(pawn, mech))
								{
									AcceptanceReport acceptanceReport = MechanitorUtility.CanControlMech(pawn, mech);
									if (!acceptanceReport.Reason.NullOrEmpty())
									{
										opts.Add(new FloatMenuOption("CannotControlMech".Translate(mech.LabelShort) + ": " + acceptanceReport.Reason, null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
									}
								}
								else
								{
									opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("ControlMech".Translate(mech.LabelShort), delegate ()
									{
										Job job = JobMaker.MakeJob(JobDefOf.ControlMech, thing);
										pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
									}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(thing), "ReservedBy", null));
								}
								opts.Add(new FloatMenuOption("CannotDisassembleMech".Translate(mech.LabelCap) + ": " + "MustBeOverseer".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else
							{
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("DisconnectMech".Translate(mech.LabelShort), delegate ()
								{
									MechanitorUtility.ForceDisconnectMechFromOverseer(mech);
								}, MenuOptionPriority.Low, null, null, 0f, null, null, true, -10), pawn, new LocalTargetInfo(thing), "ReservedBy", null));
								if (!mech.IsFighting())
								{
									opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("DisassembleMech".Translate(mech.LabelCap), delegate ()
									{
										WindowStack windowStack = Find.WindowStack;
										TaggedString text6 = "ConfirmDisassemblingMech".Translate(mech.LabelCap) + ":\n" + (from x in MechanitorUtility.IngredientsFromDisassembly(mech.def)
																															select x.Summary).ToLineList("  - ", false);
										Action confirmedAct = () =>
                                        {
                                            pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.DisassembleMech, thing)), new JobTag?(JobTag.Misc), true);
										};
										windowStack.Add(Dialog_MessageBox.CreateConfirmation(text6, confirmedAct, true, null, WindowLayer.Dialog));
									}, MenuOptionPriority.Low, null, null, 0f, null, null, true, -20), pawn, new LocalTargetInfo(thing), "ReservedBy", null));
								}
							}
							if (pawn.Drafted && MechRepairUtility.CanRepair(mech))
							{
								if (!canReach)
								{
									opts.Add(new FloatMenuOption("CannotRepairMech".Translate(mech.LabelShort) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
								}
								else
								{
									opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("RepairThing".Translate(mech.LabelShort), delegate ()
                                    {
                                        Job job = JobMaker.MakeJob(JobDefOf.RepairMech, mech);
										pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
									}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(thing), "ReservedBy", null));
								}
							}
						}
					}
				}
				if (ModsConfig.BiotechActive)
				{
					foreach (Thing thing6 in thingList)
					{
						if (thing6 is Pawn p && p.IsSelfShutdown() && pawn.CanReach(p, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, p.Map, out var exitSpot, out var enterSpot))
						{
							Building_MechCharger charger = FindBuildingUtility.GetClosestCharger(p, pawn, false, out var exitSpot2, out var enterSpot2);
							if (charger == null)
							{
								charger = JobGiver_GetEnergy_Charger.GetClosestCharger(p, pawn, true);
							}
							if (charger == null)
							{
								opts.Add(new FloatMenuOption("CannotCarryToRecharger".Translate(p.Named("PAWN")) + ": " + "CannotCarryToRechargerNoneAvailable".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else if (pawn.CanReach(charger, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, charger.Map, out _, out _))
							{
								opts.Add(new FloatMenuOption("CannotCarryToRecharger".Translate(p.Named("PAWN")) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else
							{
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("CarryToRechargerOrdered".Translate(p.Named("PAWN")), delegate()
								{
									Job job = JobMaker.MakeJob(VMF_DefOf.VMF_HaulMechToChargerAcrossMaps, p, charger, charger.InteractionCell);
									job.count = 1;
                                    job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2);
									pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
								}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, new LocalTargetInfo(p), "ReservedBy", null));
							}
						}
					}
				}
			}
			if (ModsConfig.BiotechActive && pawn.CanDeathrest())
			{
				List<Thing> thingList2 = thingList;
				for (int i = 0; i < thingList2.Count; i++)
				{
					if (thingList2[i] is Building_Bed bed && bed.def.building.bed_humanlike)
					{
						if (!pawn.CanReach(bed, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, bed.Map, out var exitSpot, out var enterSpot))
						{
							opts.Add(new FloatMenuOption("CannotDeathrest".Translate().CapitalizeFirst() + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}
						else
						{
							AcceptanceReport acceptanceReport2 = bed.CompAssignableToPawn.CanAssignTo(pawn);
							if (!acceptanceReport2.Accepted)
							{
								opts.Add(new FloatMenuOption("CannotDeathrest".Translate().CapitalizeFirst() + ": " + acceptanceReport2.Reason, null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else if ((!bed.CompAssignableToPawn.HasFreeSlot || !RestUtility.BedOwnerWillShare(bed, pawn, new GuestStatus?(pawn.guest.GuestStatus))) && !bed.IsOwner(pawn))
							{
								opts.Add(new FloatMenuOption("CannotDeathrest".Translate().CapitalizeFirst() + ": " + "AssignedToOtherPawn".Translate(bed).CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else
							{
								bool flag2 = false;
								foreach (var c in bed.OccupiedRect())
								{
									if (c.GetRoof(bed.Map) == null)
									{
										flag2 = true;
										break;
									}
								}
								if (flag2)
								{
									opts.Add(new FloatMenuOption("CannotDeathrest".Translate().CapitalizeFirst() + ": " + "ThingIsSkyExposed".Translate(bed).CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
								}
								else if (RestUtility.IsValidBedFor(bed, pawn, pawn, true, false, false, pawn.GuestStatus))
								{
									opts.Add(new FloatMenuOption("StartDeathrest".Translate(), delegate()
									{
										Job job = JobMaker.MakeJob(JobDefOf.Deathrest, bed);
										job.forceSleep = true;
										pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
									}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
								}
							}
						}
					}
				}
			}
			if (ModsConfig.BiotechActive && pawn.IsBloodfeeder())
			{
				Pawn_GeneTracker genes = pawn.genes;
				if (((genes != null) ? genes.GetFirstGeneOfType<Gene_Hemogen>() : null) != null)
				{
					foreach (LocalTargetInfo localTargetInfo9 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForBloodfeeding(pawn), false, null))
					{
						Pawn targPawn = (Pawn)localTargetInfo9.Thing;
						if (!pawn.CanReach(targPawn, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, targPawn.Map, out var exitSpot, out var enterSpot))
						{
							opts.Add(new FloatMenuOption("CannotBloodfeedOn".Translate(targPawn.Named("PAWN")) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}
						else
						{
							AcceptanceReport acceptanceReport3 = GetHemogenUtilityOnVehicle.CanFeedOnPrisoner(pawn, targPawn);
							if (acceptanceReport3.Accepted)
							{
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("BloodfeedOn".Translate(targPawn.Named("PAWN")), delegate()
								{
									Job job = JobMaker.MakeJob(JobDefOf.PrisonerBloodfeed, targPawn);
									pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
								}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, targPawn, "ReservedBy", null));
							}
							else if (!acceptanceReport3.Reason.NullOrEmpty())
							{
								opts.Add(new FloatMenuOption("CannotBloodfeedOn".Translate(targPawn.Named("PAWN")) + ": " + acceptanceReport3.Reason.CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
						}
					}
				}
			}
			if (ModsConfig.BiotechActive && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
			{
				foreach (LocalTargetInfo localTargetInfo10 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForCarryDeathresterToBed(pawn), false, null))
				{
					Pawn targPawn = (Pawn)localTargetInfo10.Thing;
					if (!targPawn.InBed())
					{
						if (!pawn.CanReach(targPawn, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, targPawn.Map, out var exitSpot, out var enterSpot))
						{
							opts.Add(new FloatMenuOption("CannotCarry".Translate(targPawn) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}
						else
						{
							Thing bestBedOrCasket = GenClosestOnVehicle.ClosestThingReachable(targPawn.PositionHeld, targPawn.MapHeld, ThingRequest.ForDef(ThingDefOf.DeathrestCasket), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, (Thing casket) => casket.Faction == Faction.OfPlayer && RestUtility.IsValidBedFor(casket, targPawn, pawn, true, false, false, targPawn.GuestStatus), null, 0, -1, false, RegionType.Set_Passable, false, false, out var exitSpot2, out var enterSpot2)
                                ?? RestUtilityOnVehicle.FindBedFor(targPawn, pawn, false, false, null, out exitSpot2, out enterSpot2);
                            if (bestBedOrCasket != null)
							{
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("CarryToSpecificThing".Translate(bestBedOrCasket), delegate()
								{
									Job job = JobMaker.MakeJob(VMF_DefOf.VMF_DeliverToBedAcrossMaps, targPawn, bestBedOrCasket);
									job.count = 1;
                                    job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2);
									pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
								}, MenuOptionPriority.RescueOrCapture, null, targPawn, 0f, null, null, true, 0), pawn, targPawn, "ReservedBy", null));
							}
							else
							{
								opts.Add(new FloatMenuOption("CannotCarry".Translate(targPawn) + ": " + "NoCasketOrBed".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
						}
					}
				}
			}
			if (ModsConfig.BiotechActive && pawn.genes != null)
			{
				foreach (LocalTargetInfo localTargetInfo11 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForXenogermAbsorption(pawn), true, null))
				{
					Pawn targPawn = (Pawn)localTargetInfo11.Thing;
					if (pawn.CanReserveAndReach(targPawn.Map, targPawn, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, true, out var exitSpot, out var enterSpot))
					{
						FloatMenuOption item3;
						if (pawn.IsQuestLodger())
						{
							item3 = new FloatMenuOption("CannotAbsorbXenogerm".Translate(targPawn.Named("PAWN")) + ": " + "TemporaryFactionMember".Translate(pawn.Named("PAWN")), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (GeneUtility.SameXenotype(pawn, targPawn))
						{
							item3 = new FloatMenuOption("CannotAbsorbXenogerm".Translate(targPawn.Named("PAWN")) + ": " + "SameXenotype".Translate(pawn.Named("PAWN")), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (targPawn.health.hediffSet.HasHediff(HediffDefOf.XenogermLossShock, false))
						{
							item3 = new FloatMenuOption("CannotAbsorbXenogerm".Translate(targPawn.Named("PAWN")) + ": " + "XenogermLossShockPresent".Translate(targPawn.Named("PAWN")), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (!CompAbilityEffect_ReimplantXenogerm.PawnIdeoCanAcceptReimplant(targPawn, pawn))
						{
							item3 = new FloatMenuOption("CannotAbsorbXenogerm".Translate(targPawn.Named("PAWN")) + ": " + "IdeoligionForbids".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else
						{
							item3 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("AbsorbXenogerm".Translate(targPawn.Named("PAWN")), delegate()
							{
								if (targPawn.IsPrisonerOfColony && !targPawn.Downed)
								{
									Messages.Message("MessageTargetMustBeDownedToForceReimplant".Translate(targPawn.Named("PAWN")), targPawn, MessageTypeDefOf.RejectInput, false);
									return;
								}
								if (GeneUtility.PawnWouldDieFromReimplanting(targPawn))
								{
									WindowStack windowStack = Find.WindowStack;
									TaggedString text6 = "WarningPawnWillDieFromReimplanting".Translate(targPawn.Named("PAWN"));
									Action confirmedAct = () =>
                                    {
                                        pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.AbsorbXenogerm, targPawn)), JobTag.Misc);
                                        if (targPawn.HomeFaction != null && !targPawn.HomeFaction.Hidden && targPawn.HomeFaction != pawn.Faction && !targPawn.HomeFaction.HostileTo(Faction.OfPlayer))
                                        {
                                            Messages.Message("MessageAbsorbingXenogermWillAngerFaction".Translate(targPawn.HomeFaction, targPawn.Named("PAWN")), pawn, MessageTypeDefOf.CautionInput, historical: false);
                                        }
                                    };
									windowStack.Add(Dialog_MessageBox.CreateConfirmation(text6, confirmedAct, true, null, WindowLayer.Dialog));
									return;
                                }
                                pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.AbsorbXenogerm, targPawn)), JobTag.Misc);
                                if (targPawn.HomeFaction != null && !targPawn.HomeFaction.Hidden && targPawn.HomeFaction != pawn.Faction && !targPawn.HomeFaction.HostileTo(Faction.OfPlayer))
                                {
                                    Messages.Message("MessageAbsorbingXenogermWillAngerFaction".Translate(targPawn.HomeFaction, targPawn.Named("PAWN")), pawn, MessageTypeDefOf.CautionInput, historical: false);
                                }
                            }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, targPawn, "ReservedBy", null);
						}
						opts.Add(item3);
					}
				}
			}
			if (ModsConfig.BiotechActive && !pawn.Downed && !pawn.Drafted)
			{
				foreach (LocalTargetInfo localTargetInfo12 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForBabyCare(pawn), true, null))
				{
					Pawn baby = (Pawn)localTargetInfo12.Thing;
					ChildcareUtility.BreastfeedFailReason? breastfeedFailReason;
					if (ChildcareUtility.CanSuckle(baby, out breastfeedFailReason))
                    {
                        pawn.CanReach(baby, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, baby.Map, out var exitSpot, out var enterSpot);
                        if (ChildcareUtility.CanBreastfeed(pawn, out breastfeedFailReason))
						{
							if (!ChildcareUtility.HasBreastfeedCompatibleFactions(pawn, baby))
							{
								continue;
							}
                            FloatMenuOption floatMenuOption5;
                            if (!ChildcareUtility.CanMomAutoBreastfeedBabyNow(pawn, baby, true, out ChildcareUtility.BreastfeedFailReason? breastfeedFailReason2))
							{
								floatMenuOption5 = new FloatMenuOption("BabyCareBreastfeedUnable".Translate(baby.Named("BABY")) + ": " + breastfeedFailReason2.Value.Translate(pawn, pawn, baby).CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
							}
							else
							{
								floatMenuOption5 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("BabyCareBreastfeed".Translate(baby.Named("BABY")), delegate()
								{
									pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, ChildcareUtility.MakeBreastfeedJob(baby, null)), new JobTag?(JobTag.Misc), true);
								}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, baby, "ReservedBy", null);
							}
							opts.Add(floatMenuOption5);
						}
						if (!CaravanFormingUtility.IsFormingCaravanOrDownedPawnToBeTakenByCaravan(baby))
						{
							LocalTargetInfo safePlace = JobDriver_BringBabyToSafetyAcrossMaps.SafePlaceForBaby(baby, pawn, true, out var exitSpot2, out var enterSpot2);
							if (safePlace.IsValid)
							{
								Building_Bed building_Bed;
								if ((building_Bed = (safePlace.Thing as Building_Bed)) != null)
								{
									if (baby.CurrentBed() == building_Bed)
									{
										continue;
									}
								}
								else if (baby.Spawned && baby.Position == safePlace.Cell)
								{
									continue;
								}
								FloatMenuOption floatMenuOption5 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("CarryToSafePlace".Translate(baby.Named("BABY")), delegate()
								{
									Job job = JobMaker.MakeJob(VMF_DefOf.VMF_BringBabyToSafetyAcrossMaps, baby, safePlace);
									job.count = 1;
                                    job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2);
									pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
								}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, baby, "ReservedBy", null);
								ChildcareUtility.BreastfeedFailReason? breastfeedFailReason3;
								if (!JobDriver_BringBabyToSafetyAcrossMaps.CanHaulBabyNow(pawn, baby, false, out breastfeedFailReason3))
								{
									Pawn pawn4;
									if (baby.MapHeld.reservationManager.TryGetReserver(baby, pawn.Faction, out pawn4))
									{
										floatMenuOption5.Label = string.Format("{0}: {1} {2}", "CannotCarryToSafePlace".Translate(), baby.LabelShort, "ReservedBy".Translate(pawn4.LabelShort, pawn4).Resolve().StripTags());
									}
									else
									{
										if (breastfeedFailReason3 != null)
										{
											breastfeedFailReason = breastfeedFailReason3;
											ChildcareUtility.BreastfeedFailReason breastfeedFailReason4 = ChildcareUtility.BreastfeedFailReason.HaulerCannotReachBaby;
											if (breastfeedFailReason.GetValueOrDefault() == breastfeedFailReason4 & breastfeedFailReason != null)
											{
												floatMenuOption5.Label = string.Format("{0}: {1}", "CannotCarryToSafePlace".Translate(), "NoPath".Translate().CapitalizeFirst());
												goto IL_3D49;
											}
										}
										floatMenuOption5.Label = string.Format("{0}: {1}", "CannotCarryToSafePlace".Translate(), "Incapable".Translate().CapitalizeFirst());
									}
									IL_3D49:
									floatMenuOption5.Disabled = true;
								}
								opts.Add(floatMenuOption5);
							}
						}
					}
				}
			}
			if (!pawn.Drafted && ModsConfig.BiotechActive)
			{
				foreach (LocalTargetInfo localTargetInfo13 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForRomance(pawn), true, null))
				{
					Pawn pawn5 = (Pawn)localTargetInfo13.Thing;
                    if (!pawn5.Drafted && !ChildcareUtility.CanSuckle(pawn5, out ChildcareUtility.BreastfeedFailReason? breastfeedFailReason))
                    {
                        bool flag3 = RelationsUtility.RomanceOption(pawn, pawn5, out FloatMenuOption floatMenuOption6, out float num);
                        if (floatMenuOption6 != null)
                        {
                            floatMenuOption6.Label = (flag3 ? "CanRomance" : "CannotRomance").Translate(floatMenuOption6.Label);
                            opts.Add(floatMenuOption6);
                        }
                    }
                }
			}
			foreach (LocalTargetInfo stripTarg2 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForStrip(pawn), true, null))
			{
				LocalTargetInfo stripTarg = stripTarg2;
				FloatMenuOption item4;
				if (!pawn.CanReach(stripTarg, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, stripTarg.Thing.Map, out var exitSpot, out var enterSpot))
				{
					item4 = new FloatMenuOption("CannotStrip".Translate(stripTarg.Thing.LabelCap, stripTarg.Thing) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
				}
				else if (stripTarg.Pawn != null && stripTarg.Pawn.HasExtraHomeFaction())
				{
					item4 = new FloatMenuOption("CannotStrip".Translate(stripTarg.Thing.LabelCap, stripTarg.Thing) + ": " + "QuestRelated".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
				}
				else if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
				{
					item4 = new FloatMenuOption("CannotStrip".Translate(stripTarg.Thing.LabelCap, stripTarg.Thing) + ": " + "Incapable".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
				}
				else
				{
					item4 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Strip".Translate(stripTarg.Thing.LabelCap, stripTarg.Thing), delegate()
					{
						stripTarg.Thing.SetForbidden(false, false);
						pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.Strip, stripTarg)), new JobTag?(JobTag.Misc), true);
						StrippableUtility.CheckSendStrippingImpactsGoodwillMessage(stripTarg.Thing);
					}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, stripTarg, "ReservedBy", null);
				}
				opts.Add(item4);
			}
			if (pawn.equipment != null)
			{
				List<Thing> thingList3 = thingList;
				for (int j = 0; j < thingList3.Count; j++)
				{
					if (thingList3[j].TryGetComp<CompEquippable>() != null)
					{
						ThingWithComps equipment = (ThingWithComps)thingList3[j];
						string labelShort = equipment.LabelShort;
						FloatMenuOption item5;
						string str;
						if (equipment.def.IsWeapon && pawn.WorkTagIsDisabled(WorkTags.Violent))
						{
							item5 = new FloatMenuOption("CannotEquip".Translate(labelShort) + ": " + "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (equipment.def.IsRangedWeapon && pawn.WorkTagIsDisabled(WorkTags.Shooting))
						{
							item5 = new FloatMenuOption("CannotEquip".Translate(labelShort) + ": " + "IsIncapableOfShootingLower".Translate(pawn), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (!pawn.CanReach(equipment, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, equipment.Map, out var exitSpot, out var enterSpot))
						{
							item5 = new FloatMenuOption("CannotEquip".Translate(labelShort) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
						{
							item5 = new FloatMenuOption("CannotEquip".Translate(labelShort) + ": " + "Incapable".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (equipment.IsBurning())
						{
							item5 = new FloatMenuOption("CannotEquip".Translate(labelShort) + ": " + "BurningLower".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (pawn.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanEquip(equipment, pawn))
						{
							item5 = new FloatMenuOption("CannotEquip".Translate(labelShort) + ": " + "QuestRelated".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (!EquipmentUtility.CanEquip(equipment, pawn, out str, false))
						{
							item5 = new FloatMenuOption("CannotEquip".Translate(labelShort) + ": " + str.CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else
						{
							string text4 = "Equip".Translate(labelShort);
							if (equipment.def.IsRangedWeapon && pawn.story != null && pawn.story.traits.HasTrait(TraitDefOf.Brawler))
							{
								text4 += " " + "EquipWarningBrawler".Translate();
							}
							if (EquipmentUtility.AlreadyBondedToWeapon(equipment, pawn))
							{
								text4 += " " + "BladelinkAlreadyBonded".Translate();
								TaggedString dialogText = "BladelinkAlreadyBondedDialog".Translate(pawn.Named("PAWN"), equipment.Named("WEAPON"), pawn.equipment.bondedWeapon.Named("BONDEDWEAPON"));
								item5 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text4, delegate()
								{
									Find.WindowStack.Add(new Dialog_MessageBox(dialogText, null, null, null, null, null, false, null, null, WindowLayer.Dialog));
								}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, equipment, "ReservedBy", null);
							}
							else
							{
								item5 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text4, delegate()
								{
									void Equip()
									{
										equipment.SetForbidden(false, true);
										pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.Equip, equipment)), JobTag.Misc, true);
										FleckMaker.Static(equipment.DrawPos, equipment.MapHeld, FleckDefOf.FeedbackEquip);
										PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.EquippingWeapons, KnowledgeAmount.Total);
									}

									string personaWeaponConfirmationText = EquipmentUtility.GetPersonaWeaponConfirmationText(equipment, pawn);
									if (!personaWeaponConfirmationText.NullOrEmpty())
									{
										WindowStack windowStack = Find.WindowStack;
										TaggedString text6 = personaWeaponConfirmationText;
										string buttonAText = "Yes".Translate();
										Action buttonAAction = () =>
										{
                                            Equip();
										};
										windowStack.Add(new Dialog_MessageBox(text6, buttonAText, buttonAAction, "No".Translate(), null, null, false, null, null, WindowLayer.Dialog));
										return;
									}
                                    Equip();
								}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, equipment, "ReservedBy", null);
							}
						}
						opts.Add(item5);
					}
				}
			}
			foreach (Pair<IReloadableComp, Thing> pair in ReloadableUtility.FindPotentiallyReloadableGear(pawn, thingList))
			{
				IReloadableComp reloadable = pair.First;
				Thing second = pair.Second;
				ThingComp thingComp = reloadable as ThingComp;
				string text5 = "Reload".Translate(thingComp.parent.Named("GEAR"), reloadable.AmmoDef.Named("AMMO")) + " (" + reloadable.LabelRemaining + ")";
				List<Thing> chosenAmmo;
				if (!pawn.CanReach(second, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, second.MapHeld, out var exitSpot, out var enterSpot))
				{
					opts.Add(new FloatMenuOption(text5 + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
				}
				else if (!reloadable.NeedsReload(true))
				{
					opts.Add(new FloatMenuOption(text5 + ": " + "ReloadFull".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
				}
				else if ((chosenAmmo = ReloadableUtility.FindEnoughAmmo(pawn, second.Position, reloadable, true)) == null)
				{
					opts.Add(new FloatMenuOption(text5 + ": " + "ReloadNotEnough".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
				}
				else if (pawn.carryTracker.AvailableStackSpace(reloadable.AmmoDef) < reloadable.MinAmmoNeeded(true))
				{
					opts.Add(new FloatMenuOption(text5 + ": " + "ReloadCannotCarryEnough".Translate(reloadable.AmmoDef.Named("AMMO")), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
				}
				else
				{
					Action action3 = delegate()
					{
						pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobGiver_Reload.MakeReloadJob(reloadable, chosenAmmo)), new JobTag?(JobTag.Misc), true);
					};
					opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text5, action3, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, second, "ReservedBy", null));
				}
			}
			if (pawn.apparel != null)
			{
				foreach (Thing thing7 in thingList)
				{
					Apparel apparel = thing7 as Apparel;
					if (apparel != null)
					{
						string key2 = "CannotWear";
						string key3 = "ForceWear";
						if (apparel.def.apparel.LastLayer.IsUtilityLayer)
						{
							key2 = "CannotEquipApparel";
							key3 = "ForceEquipApparel";
						}
						FloatMenuOption item6;
						string t2;
						if (!pawn.CanReach(apparel, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, apparel.Map, out var exitSpot, out var enterSpot))
						{
							item6 = new FloatMenuOption(key2.Translate(apparel.Label, apparel) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (apparel.IsBurning())
						{
							item6 = new FloatMenuOption(key2.Translate(apparel.Label, apparel) + ": " + "Burning".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (pawn.apparel.WouldReplaceLockedApparel(apparel))
						{
							item6 = new FloatMenuOption(key2.Translate(apparel.Label, apparel) + ": " + "WouldReplaceLockedApparel".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (!ApparelUtility.HasPartsToWear(pawn, apparel.def))
						{
							item6 = new FloatMenuOption(key2.Translate(apparel.Label, apparel) + ": " + "CannotWearBecauseOfMissingBodyParts".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else if (!EquipmentUtility.CanEquip(apparel, pawn, out t2, true))
						{
							item6 = new FloatMenuOption(key2.Translate(apparel.Label, apparel) + ": " + t2, null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
						}
						else
						{
							item6 = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(key3.Translate(apparel.LabelShort, apparel), delegate()
							{
                                Action action7 = () =>
								{
									apparel.SetForbidden(false, true);
									Job job = JobMaker.MakeJob(JobDefOf.Wear, apparel);
									pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
								};
								Action action8 = action7;
								Apparel apparelReplacedByNewApparel = ApparelUtility.GetApparelReplacedByNewApparel(pawn, apparel);
								if (apparelReplacedByNewApparel == null || !ModsConfig.BiotechActive || !MechanitorUtility.TryConfirmBandwidthLossFromDroppingThing(pawn, apparelReplacedByNewApparel, action8))
								{
									action8();
								}
							}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, apparel, "ReservedBy", null);
						}
						opts.Add(item6);
					}
				}
			}
			if (pawn.IsFormingCaravan())
			{
				foreach (var item in thingList.Where(t => t.def.category == ThingCategory.Item))
				{
					if (item.def.EverHaulable && item.def.canLoadIntoCaravan)
					{
						Pawn packTarget = GiveToPackAnimalUtility.UsablePackAnimalWithTheMostFreeSpace(pawn) ?? pawn;
						JobDef jobDef = (packTarget == pawn) ? JobDefOf.TakeInventory : JobDefOf.GiveToPackAnimal;
						if (!pawn.CanReach(item, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, item.MapHeld, out var exitSpot, out var enterSpot))
						{
							opts.Add(new FloatMenuOption("CannotLoadIntoCaravan".Translate(item.Label, item) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}
						else if (MassUtility.WillBeOverEncumberedAfterPickingUp(packTarget, item, 1))
						{
							opts.Add(new FloatMenuOption("CannotLoadIntoCaravan".Translate(item.Label, item) + ": " + "TooHeavy".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}
						else
						{
							LordJob_FormAndSendCaravan lordJob = (LordJob_FormAndSendCaravan)pawn.GetLord().LordJob;
							float capacityLeft = CaravanFormingUtility.CapacityLeft(lordJob);
							if (item.stackCount == 1)
							{
								float capacityLeft4 = capacityLeft - item.GetStatValue(StatDefOf.Mass, true, -1);
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(CaravanFormingUtility.AppendOverweightInfo("LoadIntoCaravan".Translate(item.Label, item), capacityLeft4), delegate ()
								{
									item.SetForbidden(false, false);
									Job job = JobMaker.MakeJob(jobDef, item);
									job.count = 1;
									job.checkEncumbrance = (packTarget == pawn);
									pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
								}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, item, "ReservedBy", null));
							}
							else
							{
								if (MassUtility.WillBeOverEncumberedAfterPickingUp(packTarget, item, item.stackCount))
								{
									opts.Add(new FloatMenuOption("CannotLoadIntoCaravanAll".Translate(item.Label, item) + ": " + "TooHeavy".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
								}
								else
								{
									float capacityLeft2 = capacityLeft - (float)item.stackCount * item.GetStatValue(StatDefOf.Mass, true, -1);
									opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(CaravanFormingUtility.AppendOverweightInfo("LoadIntoCaravanAll".Translate(item.Label, item), capacityLeft2), delegate ()
									{
										item.SetForbidden(false, false);
                                        Job job = JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, JobMaker.MakeJob(jobDef, item));
										job.count = item.stackCount;
										job.checkEncumbrance = (packTarget == pawn);
										pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), true);
									}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, item, "ReservedBy", null));
								}
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("LoadIntoCaravanSome".Translate(item.LabelNoCount, item), delegate ()
								{
									int num2 = Mathf.Min(MassUtility.CountToPickUpUntilOverEncumbered(packTarget, item), item.stackCount);
									Func<int, string> textGetter = (int val) =>
									{
										float capacityLeft3 = capacityLeft - (float)val * item.GetStatValue(StatDefOf.Mass, true, -1);
										return CaravanFormingUtility.AppendOverweightInfo("LoadIntoCaravanCount".Translate(item.LabelNoCount, item).Formatted(val), capacityLeft3);
									};
									int from = 1;
									int to = num2;
									Action<int> confirmAction = (int count) =>
									{
										item.SetForbidden(false, false);
                                        Job job = JobMaker.MakeJob(jobDef, item);
										job.count = count;
										job.checkEncumbrance = (packTarget == pawn);
										pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
									};
									Dialog_Slider window = new Dialog_Slider(textGetter, from, to, confirmAction, int.MinValue, 1f);
									Find.WindowStack.Add(window);
								}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, item, "ReservedBy", null));
							}
						}
					}
				}
			}
			if (!pawn.IsFormingCaravan())
			{
				foreach(var item in thingList.Where(t => t.def.category == ThingCategory.Item))
				{
					bool CanPickUp()
					{
						return !pawn.BaseMap().IsPlayerHome || (pawn.inventory != null && item.def.orderedTakeGroup != null && item.def.orderedTakeGroup.max > 0);
                    }
					if (item.def.EverHaulable && CanPickUp() && (!pawn.BaseMap().IsPlayerHome || JobGiver_DropUnusedInventory.ShouldKeepDrugInInventory(pawn, item)))
					{
						if (!pawn.CanReach(item, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, item.MapHeld, out var exitSpot, out var enterSpot))
						{
							opts.Add(new FloatMenuOption("CannotPickUp".Translate(item.Label, item) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}
						else if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, item, 1))
						{
							opts.Add(new FloatMenuOption("CannotPickUp".Translate(item.Label, item) + ": " + "TooHeavy".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}
						else
						{
							int maxAllowedToPickUp = PawnUtility.GetMaxAllowedToPickUp(pawn, item.def);
							if (maxAllowedToPickUp == 0)
							{
								opts.Add(new FloatMenuOption("CannotPickUp".Translate(item.Label, item) + ": " + "MaxPickUpAllowed".Translate(item.def.orderedTakeGroup.max, item.def.orderedTakeGroup.label), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else if (item.stackCount == 1 || maxAllowedToPickUp == 1)
							{
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("PickUpOne".Translate(item.LabelNoCount, item), delegate ()
								{
									item.SetForbidden(false, false);
									Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, item);
									job.count = 1;
									job.checkEncumbrance = true;
									job.takeInventoryDelay = 120;
									pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
								}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, item, "ReservedBy", null));
							}
							else
							{
								if (maxAllowedToPickUp < item.stackCount)
								{
									opts.Add(new FloatMenuOption("CannotPickUpAll".Translate(item.Label, item) + ": " + "MaxPickUpAllowed".Translate(item.def.orderedTakeGroup.max, item.def.orderedTakeGroup.label), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
								}
								else if (MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, item, item.stackCount))
								{
									opts.Add(new FloatMenuOption("CannotPickUpAll".Translate(item.Label, item) + ": " + "TooHeavy".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
								}
								else
								{
									opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("PickUpAll".Translate(item.Label, item), delegate ()
									{
										item.SetForbidden(false, false);
                                        Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, item);
										job.count = item.stackCount;
										job.checkEncumbrance = true;
										job.takeInventoryDelay = 120;
										pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
									}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, item, "ReservedBy", null));
								}
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("PickUpSome".Translate(item.LabelNoCount, item), delegate ()
								{
									int b = Mathf.Min(MassUtility.CountToPickUpUntilOverEncumbered(pawn, item), item.stackCount);
									int num2 = Mathf.Min(maxAllowedToPickUp, b);
									string text6 = "PickUpCount".Translate(item.LabelNoCount, item);
									int from = 1;
									int to = num2;
									Action<int> confirmAction = (int count) =>
									{
										item.SetForbidden(false, false);
                                        Job job = JobMaker.MakeJob(JobDefOf.TakeInventory, item);
										job.count = count;
										job.checkEncumbrance = true;
										job.takeInventoryDelay = 120;
										pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
									};
									Dialog_Slider window = new Dialog_Slider(text6, from, to, confirmAction, int.MinValue, 1f);
									Find.WindowStack.Add(window);
								}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, item, "ReservedBy", null));
							}
						}
					}
				}
			}
			if (!pawn.BaseMap().IsPlayerHome && !pawn.IsFormingCaravan())
			{
				foreach(var item in thingList.Where(t => t.def.category == ThingCategory.Item))
				{
					if (item.def.EverHaulable)
					{
						Pawn bestPackAnimal = GiveToPackAnimalUtility.UsablePackAnimalWithTheMostFreeSpace(pawn);
						if (bestPackAnimal != null)
						{
							if (!pawn.CanReach(item, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, item.MapHeld, out var exitSpot, out var enterSpot))
							{
								opts.Add(new FloatMenuOption("CannotGiveToPackAnimal".Translate(item.Label, item) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else if (MassUtility.WillBeOverEncumberedAfterPickingUp(bestPackAnimal, item, 1))
							{
								opts.Add(new FloatMenuOption("CannotGiveToPackAnimal".Translate(item.Label, item) + ": " + "TooHeavy".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else if (item.stackCount == 1)
							{
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("GiveToPackAnimal".Translate(item.Label, item), delegate ()
								{
									item.SetForbidden(false, false);
                                    Job job = JobMaker.MakeJob(VMF_DefOf.VMF_GiveToPackAnimalAcrossMaps, item);
									job.count = 1;
									pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
								}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, item, "ReservedBy", null));
							}
							else
							{
								if (MassUtility.WillBeOverEncumberedAfterPickingUp(bestPackAnimal, item, item.stackCount))
								{
									opts.Add(new FloatMenuOption("CannotGiveToPackAnimalAll".Translate(item.Label, item) + ": " + "TooHeavy".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
								}
								else
								{
									opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("GiveToPackAnimalAll".Translate(item.Label, item), delegate ()
									{
										item.SetForbidden(false, false);
                                        Job job = JobMaker.MakeJob(VMF_DefOf.VMF_GiveToPackAnimalAcrossMaps, item);
										job.count = item.stackCount;
										pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
									}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, item, "ReservedBy", null));
								}
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("GiveToPackAnimalSome".Translate(item.LabelNoCount, item), delegate ()
								{
									int num2 = Mathf.Min(MassUtility.CountToPickUpUntilOverEncumbered(bestPackAnimal, item), item.stackCount);
									string text6 = "GiveToPackAnimalCount".Translate(item.LabelNoCount, item);
									int from = 1;
									int to = num2;
									Action<int> confirmAction = (int count) =>
									{
										item.SetForbidden(false, false);
                                        Job job = JobMaker.MakeJob(VMF_DefOf.VMF_GiveToPackAnimalAcrossMaps, item);
										job.count = count;
										pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
									};
									Dialog_Slider window = new Dialog_Slider(text6, from, to, confirmAction, int.MinValue, 1f);
									Find.WindowStack.Add(window);
								}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, item, "ReservedBy", null));
							}
						}
					}
				}
			}
			if (!pawn.BaseMap().IsPlayerHome && pawn.BaseMap().exitMapGrid.MapUsesExitGrid)
			{
				foreach (LocalTargetInfo target in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForRescue(pawn), true, null))
				{
					Pawn p = (Pawn)target.Thing;
					if (p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony || CaravanUtility.ShouldAutoCapture(p, Faction.OfPlayer))
					{
						if (!pawn.CanReach(p, PathEndMode.ClosestTouch, Danger.Deadly, false, false, TraverseMode.ByPawn, p.Map, out var exitSpot, out var enterSpot))
						{
							opts.Add(new FloatMenuOption("CannotCarryToExit".Translate(p.Label, p) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}
						else if (pawn.Map.IsPocketMap)
						{
                            if (!FindBuildingUtility.TryFindExitPortal(pawn, p, out var portal, out var exitSpot2, out var enterSpot2))
							{
								opts.Add(new FloatMenuOption("CannotCarryToExit".Translate(p.Label, p) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else
							{
								TaggedString taggedString3 = (p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony) ? "CarryToExit".Translate(p.Label, p) : "CarryToExitAndCapture".Translate(p.Label, p);
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(taggedString3, delegate()
								{
									Job job = JobMaker.MakeJob(VMF_DefOf.VMF_CarryDownedPawnToPortalAcrossMaps, portal, p);
									job.count = 1;
                                    job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2, enterSpot2);
                                    pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
								}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, target, "ReservedBy", null));
							}
						}
						else
						{
							if (!ReachabilityUtilityOnVehicle.TryFindBestExitSpot(pawn, p, out var spot, out var exitSpot2, TraverseMode.ByPawn, true))
							{
								opts.Add(new FloatMenuOption("CannotCarryToExit".Translate(p.Label, p) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
							}
							else
							{
								TaggedString taggedString4 = (p.Faction == Faction.OfPlayer || p.IsPrisonerOfColony) ? "CarryToExit".Translate(p.Label, p) : "CarryToExitAndCapture".Translate(p.Label, p);
								opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(taggedString4, delegate()
								{
									Job job = JobMaker.MakeJob(VMF_DefOf.VMF_CarryDownedPawnToExitAcrossMaps, p, exitSpot.Cell);
									job.count = 1;
									job.failIfCantJoinOrCreateCaravan = true;
                                    job.SetSpotsToJobAcrossMaps(pawn, exitSpot, enterSpot, exitSpot2);
									pawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
								}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, target, "ReservedBy", null));
							}
						}
					}
				}
			}
			if (pawn.equipment != null && pawn.equipment.Primary != null && GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForSelf(pawn), true, null).Any<LocalTargetInfo>())
			{
				if (pawn.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanUnequip(pawn.equipment.Primary, pawn))
				{
					opts.Add(new FloatMenuOption("CannotDrop".Translate(pawn.equipment.Primary.Label, pawn.equipment.Primary) + ": " + "QuestRelated".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
				}
				else
				{
                    void action4()
                    {
                        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.DropEquipment, pawn.equipment.Primary), new JobTag?(JobTag.Misc), false);
                    }
                    opts.Add(new FloatMenuOption("Drop".Translate(pawn.equipment.Primary.Label, pawn.equipment.Primary), action4, MenuOptionPriority.Default, null, pawn, 0f, null, null, true, 0));
				}
			}
			foreach (LocalTargetInfo localTargetInfo14 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForTrade(), true, null))
			{
				LocalTargetInfo dest3 = localTargetInfo14;
				if (!pawn.CanReach(dest3, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot2, out var enterSpot2))
				{
					opts.Add(new FloatMenuOption("CannotTrade".Translate() + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
				}
				else if (pawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
				{
					opts.Add(new FloatMenuOption("CannotPrioritizeWorkTypeDisabled".Translate(SkillDefOf.Social.LabelCap), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
				}
				else
				{
					Pawn pTarg = (Pawn)dest3.Thing;
					if (pTarg.mindState.traderDismissed)
					{
						opts.Add(new FloatMenuOption("TraderDismissed".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
					}
					else
					{
						if (!pawn.CanTradeWith(pTarg.Faction, pTarg.TraderKind).Accepted)
						{
							opts.Add(new FloatMenuOption("CannotTrade".Translate() + ": " + "MissingTitleAbility".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
						}
						else
						{
                            void action5()
                            {
                                Job job = JobMaker.MakeJob(JobDefOf.TradeWithPawn, pTarg);
                                job.playerForced = true;
                                pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot2, enterSpot2, job), new JobTag?(JobTag.Misc), true);
                                PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.InteractingWithTraders, KnowledgeAmount.Total);
                            }
                            string t3 = "";
							if (pTarg.Faction != null)
							{
								t3 = " (" + pTarg.Faction.Name + ")";
							}
							opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("TradeWith".Translate(pTarg.LabelShort + ", " + pTarg.TraderKind.label) + t3, action5, MenuOptionPriority.InitiateSocial, null, dest3.Thing, 0f, null, null, true, 0), pawn, pTarg, "ReservedBy", null));
						}
						if (pTarg.GetLord().LordJob is LordJob_TradeWithColony && !pTarg.mindState.traderDismissed)
						{
                            void action6()
                            {
                                Job job = JobMaker.MakeJob(JobDefOf.DismissTrader, pTarg);
                                job.playerForced = true;
                                pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot2, enterSpot2, job), new JobTag?(JobTag.Misc), true);
                            }
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("DismissTrader".Translate(), action6, MenuOptionPriority.InitiateSocial, null, dest3.Thing, 0f, null, null, true, 0), pawn, pTarg, "ReservedBy", null));
						}
					}
				}
			}
			foreach(var casket in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForOpen(pawn), true, null))
			{
				if (!pawn.CanReach(casket, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot2, out var enterSpot2))
				{
					opts.Add(new FloatMenuOption("CannotOpen".Translate(casket.Thing) + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
				}
				else if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
				{
					opts.Add(new FloatMenuOption("CannotOpen".Translate(casket.Thing) + ": " + "Incapable".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
				}
				else if (casket.Thing.Map.designationManager.DesignationOn(casket.Thing, DesignationDefOf.Open) == null)
				{
					opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("Open".Translate(casket.Thing), delegate ()
					{
						Job job = JobMaker.MakeJob(JobDefOf.Open, casket.Thing);
						job.ignoreDesignations = true;
						pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot2, enterSpot2, job), new JobTag?(JobTag.Misc), true);
					}, MenuOptionPriority.High, null, null, 0f, null, null, true, 0), pawn, casket.Thing, "ReservedBy", null));
				}
			}
			if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && clickCell.InBounds(map) && new TargetInfo(clickCell, map, false).IsBurning() && ReachabilityUtilityOnVehicle.CanReach(pawn.Map, pawn.Position, clickCell, PathEndMode.Touch, TraverseParms.For(pawn), map, out var exitSpot3, out var enterSpot3))
			{
				FloatMenuOption item7;
				if (pawn.WorkTypeIsDisabled(WorkTypeDefOf.Firefighter))
				{
					WorkGiverDef fightFires = WorkGiverDefOf.FightFires;
					item7 = new FloatMenuOption(string.Format("{0}: {1}", "CannotGenericWorkCustom".Translate(fightFires.label), "IncapableOf".Translate().CapitalizeFirst() + " " + WorkTypeDefOf.Firefighter.gerundLabel), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
				}
				else
				{
					item7 = new FloatMenuOption("ExtinguishFiresNearby".Translate(), delegate()
					{
						Job job = JobMaker.MakeJob(JobDefOf.ExtinguishFiresNearby);
						foreach (Fire t5 in clickCell.GetFiresNearCell(map))
						{
							job.AddQueuedTarget(TargetIndex.A, t5);
						}
						pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot3, enterSpot3, job), new JobTag?(JobTag.Misc), true);
					}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
				}
				opts.Add(item7);
			}
			if (!pawn.Drafted && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning) && ReachabilityUtilityOnVehicle.CanReach(pawn.Map, pawn.Position, clickCell, PathEndMode.Touch, TraverseParms.For(pawn), map, out var exitSpot4, out var enterSpot4))
			{
				Room room = clickCell.GetRoom(pawn.Map);
				if (room != null && room.ProperRoom && !room.PsychologicallyOutdoors && !room.TouchesMapEdge)
				{
					IEnumerable<Filth> filth = CleanRoomFilthUtility.GetRoomFilthCleanableByPawn(clickCell, pawn);
					if (!filth.EnumerableNullOrEmpty<Filth>())
					{
						string roomRoleLabel = room.GetRoomRoleLabel();
						opts.Add(new FloatMenuOption("CleanRoom".Translate(roomRoleLabel), delegate()
						{
							Job job = JobMaker.MakeJob(JobDefOf.Clean);
							foreach (Filth t5 in filth)
							{
								job.AddQueuedTarget(TargetIndex.A, t5);
							}
							pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot4, enterSpot4, job), new JobTag?(JobTag.Misc), true);
						}, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0));
					}
				}
			}
			foreach (Thing thing8 in thingList)
			{
				Thing thing9 = thing8;
				CompSelectProxy compSelectProxy;
				if ((compSelectProxy = thing9.TryGetComp<CompSelectProxy>()) != null && compSelectProxy.thingToSelect != null)
				{
					thing9 = compSelectProxy.thingToSelect;
				}
                foreach (FloatMenuOption item8 in thing9.GetFloatMenuOptions(pawn))
                {
                    FloatMenuMakerOnVehicle.cachedThings.Add(thing8);
                    opts.Add(item8);
                }
			}
			foreach (LocalTargetInfo localTargetInfo15 in GenUIOnVehicle.TargetsAt(clickPos, TargetingParameters.ForPawns(), true, null))
			{
				if (!FloatMenuMakerOnVehicle.cachedThings.Contains(localTargetInfo15.Pawn) && (localTargetInfo15.Pawn != GenUIOnVehicle.vehicleForSelector))
                {
                    foreach (FloatMenuOption item9 in localTargetInfo15.Pawn.GetFloatMenuOptions(pawn))
                    {
                        FloatMenuMakerOnVehicle.cachedThings.Add(localTargetInfo15.Pawn);
                        opts.Add(item9);
                    }
				}
			}
			FloatMenuMakerOnVehicle.cachedThings.Clear();
            if (MeleeAnimation.Active)
            {
                opts.AddRange(MeleeAnimation.GenerateAMMenuOptions(clickPos, pawn));
            }
            if (CombatExtended.Active)
            {
                CombatExtended.AddMenuItems(clickPos, pawn, opts, thingList);
            }
		}

        private static void AddMutantOrders(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            IntVec3 clickCell;
            Map map;
            Vector3 clickPos2;
            if (GenUIOnVehicle.vehicleForSelector != null)
            {
                clickPos2 = clickPos.ToVehicleMapCoord(GenUIOnVehicle.vehicleForSelector);
                map = GenUIOnVehicle.vehicleForSelector.VehicleMap;

            }
            else
            {
                clickPos2 = clickPos;
                map = pawn.BaseMap();
            }
            clickCell = IntVec3.FromVector3(clickPos2);

            var targetParms = new TargetingParameters()
            {
                canTargetSelf = true,
                canTargetFires = true,
                canTargetItems = true,
                canTargetPlants = true,
            };
            var thingList = GenUIOnVehicle.TargetsAt(clickPos, targetParms, true, null).Select(t => t.Thing).ToList();
            foreach (Thing t2 in thingList)
            {
                Thing t = t2;
                if (t.def.ingestible != null && t.def.ingestible.showIngestFloatOption && pawn.RaceProps.CanEverEat(t) && t.IngestibleNow)
                {
                    Pawn_NeedsTracker needs = pawn.needs;
                    if ((needs?.food) != null || t.def.IsDrug)
                    {
                        string text;
                        if (t.def.ingestible.ingestCommandString.NullOrEmpty())
                        {
                            text = "ConsumeThing".Translate(t.LabelShort, t);
                        }
                        else
                        {
                            text = t.def.ingestible.ingestCommandString.Formatted(t.LabelShort);
                        }
                        if (!t.IsSociallyProper(pawn))
                        {
                            text = text + ": " + "ReservedForPrisoners".Translate().CapitalizeFirst();
                        }
                        FloatMenuOption floatMenuOption;
                        if (!pawn.CanReach(t, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                        {
                            floatMenuOption = new FloatMenuOption(text + ": " + "NoPath".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                        }
                        else if (!t.def.IsDrug && !pawn.WillEat(t, null, true, false))
                        {
                            floatMenuOption = new FloatMenuOption(text + ": " + "FoodNotSuitable".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                        }
                        else if (t.def.IsDrug && pawn.IsMutant && (!pawn.mutant.Def.canUseDrugs || !pawn.mutant.Def.drugWhitelist.Contains(t.def)))
                        {
                            floatMenuOption = new FloatMenuOption(text + ": " + "DrugNotSuitable".Translate().CapitalizeFirst(), null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                        }
                        else
                        {
                            MenuOptionPriority priority = (t is Corpse) ? MenuOptionPriority.Low : MenuOptionPriority.Default;
                            bool maxAmountToPickup = FoodUtility.GetMaxAmountToPickup(t, pawn, FoodUtility.WillIngestStackCountOf(pawn, t.def, FoodUtility.NutritionForEater(pawn, t))) != 0;
                            floatMenuOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, delegate ()
                            {
                                int maxAmountToPickup2 = FoodUtility.GetMaxAmountToPickup(t, pawn, FoodUtility.WillIngestStackCountOf(pawn, t.def, FoodUtility.NutritionForEater(pawn, t)));
                                if (maxAmountToPickup2 == 0)
                                {
                                    return;
                                }
                                t.SetForbidden(false, true);
                                Job job = JobMaker.MakeJob(JobDefOf.Ingest, t);
                                job.count = maxAmountToPickup2;
                                pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, job), new JobTag?(JobTag.Misc), true);
                            }, priority, null, null, 0f, null, null, true, 0), pawn, t, "ReservedBy", null);
                            if (!maxAmountToPickup)
                            {
                                floatMenuOption.action = null;
                            }
                        }
                        opts.Add(floatMenuOption);
                    }
                }
            }
            foreach(var thing in thingList)
            {
				if (thing is Building_Bed building_Bed)
				{
					FloatMenuOption bedRestFloatMenuOption = FloatMenuMakerOnVehicle.GetBedRestFloatMenuOption(pawn, building_Bed);
					if (bedRestFloatMenuOption != null)
					{
						opts.Add(bedRestFloatMenuOption);
					}
				}
            }

            if (ModsConfig.IsActive("Orpheusly.PawnStorages"))
            {
                if (PawnStorages_MutantOrdersPatch == null)
                {
                    PawnStorages_MutantOrdersPatch = AccessTools.MethodDelegate<Action<Vector3, Pawn, List<FloatMenuOption>>>(AccessTools.Method("PawnStorages.MutantOrdersPatch:Postfix"));
                }
                var pawnMap = pawn.Map;
                var flag = pawnMap != map;
                try
                {
                    if (flag) pawn.VirtualMapTransfer(map);
                    PawnStorages_MutantOrdersPatch(clickPos2, pawn, opts);
                }
                finally
                {
                    if (flag) pawn.VirtualMapTransfer(pawnMap);
                }
            }
        }

        private static Action<Vector3, Pawn, List<FloatMenuOption>> PawnStorages_MutantOrdersPatch;

        private static void AddJobGiverWorkOrders(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts, bool drafted)
        {
            if (pawn.thinker.TryGetMainTreeThinkNode<JobGiver_Work>() == null)
            {
                return;
            }
            IntVec3 clickCell;
            Map map;
            if (GenUIOnVehicle.vehicleForSelector != null)
            {
                clickCell = IntVec3.FromVector3(clickPos.ToVehicleMapCoord(GenUIOnVehicle.vehicleForSelector));
                map = GenUIOnVehicle.vehicleForSelector.VehicleMap;

            }
            else
            {
                clickCell = IntVec3.FromVector3(clickPos);
                map = pawn.BaseMap();
            }
            if (!clickCell.InBounds(map)) return;
            var targetParms = new TargetingParameters()
            {
                canTargetSelf = true,
                canTargetFires = true,
                canTargetItems = true,
                canTargetPlants = true,
                canTargetCorpses = true
            };
            var baseClickCell = GenUIOnVehicle.vehicleForSelector != null && !GenUIOnVehicle.vehicleForSelector.Spawned ? clickCell : IntVec3.FromVector3(clickPos);
            IEnumerable<Thing> searchSet = clickCell.GetThingList(map);
            if (FloatMenuMakerMap.makingFor is VehiclePawn)
            {
                searchSet = searchSet.Except(FloatMenuMakerMap.makingFor);
            }
            if (!pawn.Drafted && GenUIOnVehicle.vehicleForSelector != null && GenUIOnVehicle.vehicleForSelector.Spawned)
            {
                searchSet = searchSet.Concat(GenUIOnVehicle.vehicleForSelector);
            }
            foreach (Thing thing in searchSet)
            {
                if (thing.Spawned)
                {
                    Thing thing2 = thing;
                    CompSelectProxy compSelectProxy;
                    if ((compSelectProxy = thing2.TryGetComp<CompSelectProxy>()) != null && compSelectProxy.thingToSelect != null)
                    {
                        thing2 = compSelectProxy.thingToSelect;
                    }
                    bool flag = false;
                    foreach (WorkTypeDef workTypeDef in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                    {
                        for (int i = 0; i < workTypeDef.workGiversByPriority.Count; i++)
                        {
                            WorkGiverDef workGiver = workTypeDef.workGiversByPriority[i];
                            if (!drafted || workGiver.canBeDoneWhileDrafted)
                            {
                                if (workGiver.Worker is WorkGiver_Scanner workGiver_Scanner && workGiver_Scanner.def.directOrderable)
                                {
                                    JobFailReason.Clear();
                                    var map2 = pawn.Map;
                                    var pos = pawn.Position;
                                    var canReach = pawn.CanReach(thing2, workGiver_Scanner.PathEndMode, Danger.Deadly, false, false, TraverseMode.ByPawn, thing2.Map, out var exitSpot, out var enterSpot);
                                    var needTransfer = !JobAcrossMapsUtility.NoNeedVirtualMapTransfer(pawn.Map, thing2.Map, workGiver_Scanner);
                                    if (needTransfer)
                                    {
                                        pawn.VirtualMapTransfer(thing2.Map, enterSpot.IsValid ? enterSpot.Cell : exitSpot.IsValid ? exitSpot.Cell.ToThingBaseMapCoord(pawn) : pawn.Position);
                                    }
                                    try
                                    {
                                        if (!FloatMenuMakerOnVehicle.ScannerShouldSkip(pawn, workGiver_Scanner, thing2))
                                        {
                                            Action action = null;
                                            PawnCapacityDef pawnCapacityDef = workGiver_Scanner.MissingRequiredCapacity(pawn);
                                            string text;
                                            if (pawnCapacityDef != null)
                                            {
                                                text = "CannotMissingHealthActivities".Translate(pawnCapacityDef.label);
                                            }
                                            else
                                            {
                                                Job job;
                                                if (!workGiver_Scanner.HasJobOnThing(pawn, thing2, true))
                                                {
                                                    job = null;
                                                }
                                                else
                                                {
                                                    job = workGiver_Scanner.JobOnThing(pawn, thing2, true);
                                                }
                                                if (JobFailReason.Silent)
                                                {
                                                    continue;
                                                }
                                                if (job == null)
                                                {
                                                    if (JobFailReason.HaveReason)
                                                    {
                                                        if (!JobFailReason.CustomJobString.NullOrEmpty())
                                                        {
                                                            text = "CannotGenericWorkCustom".Translate(JobFailReason.CustomJobString);
                                                        }
                                                        else
                                                        {
                                                            text = "CannotGenericWork".Translate(workGiver_Scanner.def.verb, thing2.LabelShort, thing2);
                                                        }
                                                        text = text + ": " + JobFailReason.Reason.CapitalizeFirst();
                                                    }
                                                    else
                                                    {
                                                        if (!thing2.IsForbidden(pawn))
                                                        {
                                                            continue;
                                                        }
                                                        if (!thing2.PositionOnBaseMap().InAllowedArea(pawn))
                                                        {
                                                            text = "CannotPrioritizeForbiddenOutsideAllowedArea".Translate() + ": " + pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap.Label;
                                                        }
                                                        else
                                                        {
                                                            text = "CannotPrioritizeForbidden".Translate(thing2.Label, thing2);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    WorkTypeDef workType = workGiver_Scanner.def.workType;
                                                    if (pawn.WorkTagIsDisabled(workGiver_Scanner.def.workTags))
                                                    {
                                                        text = "CannotPrioritizeWorkGiverDisabled".Translate(workGiver_Scanner.def.label);
                                                    }
                                                    else if (pawn.jobs.curJob != null && pawn.jobs.curJob.JobIsSameAs(pawn, job))
                                                    {
                                                        text = "CannotGenericAlreadyAm".Translate(workGiver_Scanner.PostProcessedGerund(job), thing2.LabelShort, thing2);
                                                    }
                                                    else if (pawn.workSettings.GetPriority(workType) == 0)
                                                    {
                                                        if (pawn.WorkTypeIsDisabled(workType))
                                                        {
                                                            text = "CannotPrioritizeWorkTypeDisabled".Translate(workType.gerundLabel);
                                                        }
                                                        else if ("CannotPrioritizeNotAssignedToWorkType".CanTranslate())
                                                        {
                                                            text = "CannotPrioritizeNotAssignedToWorkType".Translate(workType.gerundLabel);
                                                        }
                                                        else
                                                        {
                                                            text = "CannotPrioritizeWorkTypeDisabled".Translate(workType.pawnLabel);
                                                        }
                                                    }
                                                    else if (job.def == JobDefOf.Research && thing2 is Building_ResearchBench)
                                                    {
                                                        text = "CannotPrioritizeResearch".Translate();
                                                    }
                                                    else if (thing2.IsForbidden(pawn))
                                                    {
                                                        if (!thing2.PositionOnBaseMap().InAllowedArea(pawn))
                                                        {
                                                            text = "CannotPrioritizeForbiddenOutsideAllowedArea".Translate() + ": " + pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap.Label;
                                                        }
                                                        else
                                                        {
                                                            text = "CannotPrioritizeForbidden".Translate(thing2.Label, thing2);
                                                        }
                                                    }
                                                    else if (!canReach)
                                                    {
                                                        text = (thing2.Label + ": " + "NoPath".Translate().CapitalizeFirst()).CapitalizeFirst();
                                                    }
                                                    else
                                                    {
                                                        text = "PrioritizeGeneric".Translate(workGiver_Scanner.PostProcessedGerund(job), thing2.Label).CapitalizeFirst();
                                                        string text2 = workGiver_Scanner.JobInfo(pawn, job);
                                                        if (!string.IsNullOrEmpty(text2))
                                                        {
                                                            text = text + ": " + text2;
                                                        }
                                                        Job localJob = job;
                                                        WorkGiver_Scanner localScanner = workGiver_Scanner;
                                                        job.workGiverDef = workGiver_Scanner.def;
                                                        action = delegate ()
                                                        {
                                                            var job2 = needTransfer ? JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, localJob) : localJob;
                                                            if (pawn.jobs.TryTakeOrderedJobPrioritizedWork(job2, localScanner, clickCell))
                                                            {
                                                                var drawPos = map.IsVehicleMapOf(out var vehicle) ? clickCell.ToVector3Shifted().ToBaseMapCoord(vehicle) : clickCell.ToVector3Shifted();
                                                                var baseMap = map.BaseMap();
                                                                if (workGiver.forceMote != null)
                                                                {
                                                                    MoteMaker.MakeStaticMote(drawPos, baseMap, workGiver.forceMote, 1f);
                                                                }
                                                                if (workGiver.forceFleck != null)
                                                                {
                                                                    FleckMaker.Static(drawPos, baseMap, workGiver.forceFleck, 1f);
                                                                }
                                                            }
                                                        };
                                                    }
                                                }
                                            }
                                            if (DebugViewSettings.showFloatMenuWorkGivers)
                                            {
                                                text += string.Format(" (from {0})", workGiver.defName);
                                            }
                                            FloatMenuOption menuOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, action, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, thing2, "ReservedBy", workGiver_Scanner.GetReservationLayer(pawn, thing2));
                                            if (drafted && workGiver.autoTakeablePriorityDrafted != -1)
                                            {
                                                menuOption.autoTakeable = true;
                                                menuOption.autoTakeablePriority = (float)workGiver.autoTakeablePriorityDrafted;
                                            }
                                            if (!opts.Any((FloatMenuOption op) => op.Label == menuOption.Label))
                                            {
                                                if (workGiver.equivalenceGroup != null)
                                                {
                                                    if (FloatMenuMakerOnVehicle.equivalenceGroupTempStorage[(int)workGiver.equivalenceGroup.index] == null || (FloatMenuMakerOnVehicle.equivalenceGroupTempStorage[(int)workGiver.equivalenceGroup.index].Disabled && !menuOption.Disabled))
                                                    {
                                                        FloatMenuMakerOnVehicle.equivalenceGroupTempStorage[(int)workGiver.equivalenceGroup.index] = menuOption;
                                                        flag = true;
                                                    }
                                                }
                                                else
                                                {
                                                    opts.Add(menuOption);
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        if (needTransfer)
                                        {
                                            pawn.VirtualMapTransfer(map2, pos);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (flag)
                    {
                        for (int j = 0; j < FloatMenuMakerOnVehicle.equivalenceGroupTempStorage.Length; j++)
                        {
                            if (FloatMenuMakerOnVehicle.equivalenceGroupTempStorage[j] != null)
                            {
                                opts.Add(FloatMenuMakerOnVehicle.equivalenceGroupTempStorage[j]);
                                FloatMenuMakerOnVehicle.equivalenceGroupTempStorage[j] = null;
                            }
                        }
                    }
                }
            }
            foreach (WorkTypeDef workTypeDef2 in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                for (int k = 0; k < workTypeDef2.workGiversByPriority.Count; k++)
                {
                    WorkGiverDef workGiver = workTypeDef2.workGiversByPriority[k];
                    if (!drafted || workGiver.canBeDoneWhileDrafted)
                    {
                        if (workGiver.Worker is WorkGiver_Scanner workGiver_Scanner2 && workGiver_Scanner2.def.directOrderable)
                        {
                            JobFailReason.Clear();
                            if (workGiver_Scanner2.PotentialWorkCellsGlobal(pawn).Contains(clickCell) && !workGiver_Scanner2.ShouldSkip(pawn, true))
                            {
                                Action action2 = null;
                                string label = null;
                                PawnCapacityDef pawnCapacityDef2 = workGiver_Scanner2.MissingRequiredCapacity(pawn);
                                if (pawnCapacityDef2 != null)
                                {
                                    label = "CannotMissingHealthActivities".Translate(pawnCapacityDef2.label);
                                }
                                else
                                {
                                    Job job2;
                                    if (!workGiver_Scanner2.HasJobOnCell(pawn, clickCell, true))
                                    {
                                        job2 = null;
                                    }
                                    else
                                    {
                                        job2 = workGiver_Scanner2.JobOnCell(pawn, clickCell, true);
                                    }
                                    if (job2 == null)
                                    {
                                        if (JobFailReason.HaveReason)
                                        {
                                            if (!JobFailReason.CustomJobString.NullOrEmpty())
                                            {
                                                label = "CannotGenericWorkCustom".Translate(JobFailReason.CustomJobString);
                                            }
                                            else
                                            {
                                                label = "CannotGenericWork".Translate(workGiver_Scanner2.def.verb, "AreaLower".Translate());
                                            }
                                            label = label + ": " + JobFailReason.Reason.CapitalizeFirst();
                                        }
                                        else
                                        {
                                            if (!baseClickCell.IsForbidden(pawn))
                                            {
                                                continue;
                                            }
                                            if (!baseClickCell.InAllowedArea(pawn))
                                            {
                                                label = "CannotPrioritizeForbiddenOutsideAllowedArea".Translate() + ": " + pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap.Label;
                                            }
                                            else
                                            {
                                                label = "CannotPrioritizeCellForbidden".Translate();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        WorkTypeDef workType2 = workGiver_Scanner2.def.workType;
                                        if (pawn.jobs.curJob != null && pawn.jobs.curJob.JobIsSameAs(pawn, job2))
                                        {
                                            label = "CannotGenericAlreadyAmCustom".Translate(workGiver_Scanner2.PostProcessedGerund(job2));
                                        }
                                        else if (pawn.workSettings.GetPriority(workType2) == 0)
                                        {
                                            if (pawn.WorkTypeIsDisabled(workType2))
                                            {
                                                label = "CannotPrioritizeWorkTypeDisabled".Translate(workType2.gerundLabel);
                                            }
                                            else if ("CannotPrioritizeNotAssignedToWorkType".CanTranslate())
                                            {
                                                label = "CannotPrioritizeNotAssignedToWorkType".Translate(workType2.gerundLabel);
                                            }
                                            else
                                            {
                                                label = "CannotPrioritizeWorkTypeDisabled".Translate(workType2.pawnLabel);
                                            }
                                        }
                                        else if (baseClickCell.IsForbidden(pawn))
                                        {
                                            if (!baseClickCell.InAllowedArea(pawn))
                                            {
                                                label = "CannotPrioritizeForbiddenOutsideAllowedArea".Translate() + ": " + pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap.Label;
                                            }
                                            else
                                            {
                                                label = "CannotPrioritizeCellForbidden".Translate();
                                            }
                                        }
                                        else if (!pawn.CanReach(clickCell, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                                        {
                                            label = "AreaLower".Translate().CapitalizeFirst() + ": " + "NoPath".Translate().CapitalizeFirst();
                                        }
                                        else
                                        {
                                            label = "PrioritizeGeneric".Translate(workGiver_Scanner2.PostProcessedGerund(job2), "AreaLower".Translate()).CapitalizeFirst();
                                            Job localJob = job2;
                                            WorkGiver_Scanner localScanner = workGiver_Scanner2;
                                            job2.workGiverDef = workGiver_Scanner2.def;
                                            action2 = delegate ()
                                            {
                                                if (pawn.jobs.TryTakeOrderedJob(JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, localJob), JobTag.Misc, true))
                                                {
                                                    localJob.workGiverDef = localScanner.def;
                                                    if (localScanner.def.prioritizeSustains)
                                                    {
                                                        pawn.mindState.priorityWork.Set(clickCell, localScanner.def);
                                                    }
                                                    if (workGiver.forceMote != null)
                                                    {
                                                        MoteMaker.MakeStaticMote(clickCell, pawn.Map, workGiver.forceMote, 1f);
                                                    }
                                                    if (workGiver.forceFleck != null)
                                                    {
                                                        FleckMaker.Static(clickCell, pawn.Map, workGiver.forceFleck, 1f);
                                                    }
                                                }
                                            };
                                        }
                                    }
                                }
                                if (!opts.Any((FloatMenuOption op) => op.Label == label.TrimEnd(Array.Empty<char>())))
                                {
                                    FloatMenuOption floatMenuOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action2, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), pawn, clickCell, "ReservedBy", null);
                                    if (drafted && workGiver.autoTakeablePriorityDrafted != -1)
                                    {
                                        floatMenuOption.autoTakeable = true;
                                        floatMenuOption.autoTakeablePriority = (float)workGiver.autoTakeablePriorityDrafted;
                                    }
                                    opts.Add(floatMenuOption);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void AddUndraftedOrders(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            if (FloatMenuMakerOnVehicle.equivalenceGroupTempStorage == null || FloatMenuMakerOnVehicle.equivalenceGroupTempStorage.Length != DefDatabase<WorkGiverEquivalenceGroupDef>.DefCount)
            {
                FloatMenuMakerOnVehicle.equivalenceGroupTempStorage = new FloatMenuOption[DefDatabase<WorkGiverEquivalenceGroupDef>.DefCount];
            }
            FloatMenuMakerOnVehicle.AddJobGiverWorkOrders(clickPos, pawn, opts, false);
        }

        public static FloatMenuOption GetBedRestFloatMenuOption(Pawn myPawn, Building_Bed bed)
        {
            if (!myPawn.RaceProps.Humanlike || bed.ForPrisoners || !bed.Medical || myPawn.Drafted || bed.Faction != Faction.OfPlayer || !RestUtility.CanUseBedEver(myPawn, bed.def))
            {
                return null;
            }
            if (!HealthAIUtility.ShouldSeekMedicalRest(myPawn))
            {
                if (myPawn.health.surgeryBills.AnyShouldDoNow && !WorkGiver_PatientGoToBedTreatment.AnyAvailableDoctorFor(myPawn))
                {
                    return new FloatMenuOption("UseMedicalBed".Translate() + " (" + "NoDoctor".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                }
                return new FloatMenuOption("UseMedicalBed".Translate() + " (" + "NotInjured".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
            }
            else
            {
                if (myPawn.IsSlaveOfColony && !bed.ForSlaves)
                {
                    return new FloatMenuOption("UseMedicalBed".Translate() + " (" + "NotForSlaves".Translate() + ")", null, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                }
                void Action()
                {
                    if (!bed.ForPrisoners && bed.Medical && myPawn.CanReserveAndReach(bed.Map, bed, PathEndMode.ClosestTouch, Danger.Deadly, bed.SleepingSlotsCount, -1, null, true, out var exitSpot, out var enterSpot))
                    {
                        if (myPawn.CurJobDef == JobDefOf.LayDown && myPawn.CurJob.GetTarget(TargetIndex.A).Thing == bed)
                        {
                            myPawn.CurJob.restUntilHealed = true;
                        }
                        else
                        {
                            Job job = JobAcrossMapsUtility.GotoDestMapJob(myPawn, exitSpot, enterSpot, JobMaker.MakeJob(JobDefOf.LayDown, bed));
                            job.restUntilHealed = true;
                            myPawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), true);
                        }
                        myPawn.mindState.ResetLastDisturbanceTick();
                    }
                }
                return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("UseMedicalBed".Translate(), Action, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0), myPawn, bed, (bed.AnyUnoccupiedSleepingSlot ? "ReservedBy" : "SomeoneElseSleeping").CapitalizeFirst(), null);
            }
        }

        public static void ValidateTakeToBedOption(Pawn pawn, Pawn target, FloatMenuOption option, string cannot, GuestStatus? guestStatus = null)
        {
            if (RestUtilityOnVehicle.FindBedFor(target, pawn, false, false, guestStatus, out _, out _) == null)
            {
                Building_Bed building_Bed = RestUtilityOnVehicle.FindBedFor(target, pawn, false, true, guestStatus, out _, out _);
                if (building_Bed != null)
                {
                    if (building_Bed.Map.reservationManager.TryGetReserver(building_Bed, pawn.Faction, out Pawn pawn2))
                    {
                        option.Label = string.Concat(new string[]
                        {
                            option.Label,
                            " (",
                            building_Bed.def.label,
                            " ",
                            "ReservedBy".Translate(pawn2.LabelShort, pawn2).Resolve().StripTags(),
                            ")"
                        });
                        return;
                    }
                }
                else
                {
                    option.Disabled = true;
                    option.Label = cannot;
                }
            }
        }

        private static bool ScannerShouldSkip(Pawn pawn, WorkGiver_Scanner scanner, Thing t)
        {
            return (!scanner.PotentialWorkThingRequest.Accepts(t) && (scanner.PotentialWorkThingsGlobal(pawn) == null || !scanner.PotentialWorkThingsGlobal(pawn).Contains(t))) || scanner.ShouldSkipAll(pawn, true);
        }

        private static List<Thing> cachedThings = new List<Thing>();

        private static FloatMenuOption[] equivalenceGroupTempStorage;
    }
}