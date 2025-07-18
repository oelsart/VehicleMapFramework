using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Targeting;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using Vehicles;
using Vehicles.World;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace VehicleMapFramework
{
    public class CompVehicleLauncherGravshipVehicle : CompVehicleLauncherWithMap
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
                if (gizmo is Command_ActionHighlighter takeoffCommand)
                {
                    if (Vehicle is VehiclePawnWithMap vehicle && Vehicle.def.HasModExtension<VehicleMapProps_Gravship>())
                    {
                        AcceptanceReport? report = null;

                        if (GravshipUtility.GetPlayerGravEngine(vehicle.VehicleMap) is not Building_GravEngine engine)
                        {
                            takeoffCommand.Disable("CannotLaunchNoEngine".Translate().CapitalizeFirst());
                            continue;
                        }
                        var pocketMapProperties = vehicle.VehicleMap.generatorDef?.pocketMapProperties;
                        var flag = pocketMapProperties?.canLaunchGravship ?? false;
                        pocketMapProperties?.canLaunchGravship = true;
                        CompPilotConsole console = null;
                        if ((console = engine.GravshipComponents.OfType<CompPilotConsole>().FirstOrDefault(c => (report = c.CanUseNow()).Value.Accepted)) is null)
                        {
                            takeoffCommand.Disable(report?.Reason ?? "PilotConsoleInaccessible".Translate().CapitalizeFirst());
                            pocketMapProperties?.canLaunchGravship = flag;
                            continue;
                        }
                        pocketMapProperties?.canLaunchGravship = flag;

                        var pilot = vehicle.handlers.FirstOrDefault(h => h.role.key == "pilot").thingOwner.InnerListForReading.FirstOrDefault();
                        if (pilot is null)
                        {
                            takeoffCommand.Disable("VMF_CannotLaunchNoPilot".Translate());
                            continue;
                        }
                        var copilot = vehicle.handlers.FirstOrDefault(h => h.role.key == "copilot").thingOwner.InnerListForReading.FirstOrDefault();
                        takeoffCommand.action = () => StartChoosingDestination(vehicle, engine, console, pilot, copilot);
                    }
                }
            }
        }

        private void StartChoosingDestination(VehiclePawn vehicle, Building_GravEngine engine, CompPilotConsole console, Pawn pilot, Pawn copilot)
        {
            if (AnyLeftToLoad)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmSendNotCompletelyLoadedPods".Translate(Vehicle.LabelCapNoCount), OpenDialog, false, null, WindowLayer.Dialog));
                return;
            }
            OpenDialog();

            void OpenDialog()
            {
                var assignedSeats = new Dictionary<Pawn, VehicleRoleHandler>();
                VehicleRoleHandler copilotHandler = null;
                foreach (var handler in vehicle.handlers)
                {
                    if (handler is VehicleRoleHandlerBuildable)
                    {
                        if (handler.role.key == "copilot") copilotHandler = handler;

                        for (var i = 0; i < handler.thingOwner.Count; i++)
                        {
                            var pawn = handler.thingOwner[i];
                            assignedSeats[pawn] = handler;
                            vehicle.DisembarkPawn(pawn);
                        }
                    }
                }
                var ritual = (Precept_Ritual)pilot.Ideo.GetPrecept(PreceptDefOf.GravshipLaunch);
                var ritualObligation = ritual.activeObligations?.FirstOrDefault(o => ritual.obligationTargetFilter.CanUseTarget(console.parent, o).canUse);
                var outcome = DefDatabase<RitualOutcomeEffectDef>.GetNamed("GravshipLaunch");
                var forcedForRole = new Dictionary<string, Pawn>
                {
                    ["pilot"] = pilot,
                };
                if (copilot is not null)
                {
                    forcedForRole["copilot"] = copilot;
                }

                Dialog_BeginRitual dialog = null;
                dialog = new Dialog_BeginRitual(ritual.LabelCap, ritual, console.parent, engine.Map, assignment =>
                {
                    ChoosingDestination(assignment);
                    return true;
                }, pilot, ritualObligation, (pawn, voluntary, allowOtherIdeos) => pawn.GetLord() == null && (!pawn.RaceProps.Animal || ritual.behavior.def.roles.Any(r =>
                {
                    return r.AppliesToPawn(pawn, out var text6, console.parent, null, null, null, true);
                })) && !pawn.IsSubhuman && (!ritual.ritualOnlyForIdeoMembers || ritual.def.allowSpectatorsFromOtherIdeos || (pawn.Ideo == ritual.ideo || !voluntary || allowOtherIdeos) || pawn.IsPrisonerOfColony || pawn.RaceProps.Animal || (!forcedForRole.NullOrEmpty() && forcedForRole.ContainsValue(pawn))),
                null, null, forcedForRole, outcome);
                Find.WindowStack.Add(dialog);

                void ChoosingDestination(RitualRoleAssignments assignment)
                {
                    var qualityRange = (FloatRange)AccessTools.Method(typeof(Dialog_BeginRitual), "PredictedQuality").Invoke(dialog, [null]);
                    var quality = qualityRange.RandomInRange;
                    engine.launchInfo = new LaunchInfo
                    {
                        pilot = pilot,
                        copilot = copilot,
                        quality = quality,
                        doNegativeOutcome = Rand.Chance(GravshipUtility.NegativeLandingOutcomeFromQuality(quality))
                    };
                    foreach (var assigned in assignedSeats)
                    {
                        vehicle.TryAddPawn(assigned.Key, assigned.Value);
                    }
                    var copilot2 = assignment.FirstAssignedPawn("copilot");
                    if (copilot2 is not null && copilotHandler is not null)
                    {
                        vehicle.TryAddPawn(copilot2, copilotHandler);
                    }
                    CameraJumper.TryJump(CameraJumper.GetWorldTarget(Vehicle));
                    Find.WorldSelector.ClearSelection();
                    PlanetTile curTile = Vehicle.Map.Tile;
                    PlanetLayer curLayer = curTile.Layer;
                    PlanetTile cachedClosestLayerTile = PlanetTile.Invalid;
                    StringBuilder cannotPlaceTileReason = new();
                    int GetMaxLaunchDistance(PlanetLayer layer)
                    {
                        return Mathf.FloorToInt((engine?.MaxLaunchDistance / layer.Def.rangeDistanceFactor).GetValueOrDefault());
                    }

                    Find.TilePicker.StartTargeting(tile =>
                    {
                        cannotPlaceTileReason.Clear();
                        if (!GravshipUtility.TryGetPathFuelCost(curTile, tile, out var cost, out var distance, 10f, engine.FuelUseageFactor) && !DebugSettings.ignoreGravshipRange)
                        {
                            Messages.Message("CannotLaunchDestination".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                            return false;
                        }
                        if (!engine.HasSignalJammer && Find.WorldObjects.TryGetWorldObjectAt<MapParent>(tile, out var wo) && wo.RequiresSignalJammerToReach)
                        {
                            Messages.Message("TransportPodDestinationRequiresSignalJammer".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                            return false;
                        }
                        if (cost > engine.TotalFuel && !DebugSettings.ignoreGravshipRange)
                        {
                            Messages.Message("CannotLaunchNotEnoughFuel".Translate().CapitalizeFirst(), MessageTypeDefOf.RejectInput, historical: false);
                            return false;
                        }
                        if (distance > GetMaxLaunchDistance(tile.Layer) && !DebugSettings.ignoreGravshipRange)
                        {
                            Messages.Message("TransportPodDestinationBeyondMaximumRange".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                            return false;
                        }
                        if (tile == parent.Tile && !Vehicle.Map.listerThings.AnyThingWithDef(ThingDefOf.GravAnchor))
                        {
                            Messages.Message("CannotLandOnSameTile".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                            return false;
                        }
                        MapParent mapParent = Find.World.worldObjects.MapParentAt(tile);
                        if (mapParent != null && mapParent.HasMap)
                        {
                            return true;
                        }
                        if (!TileFinder.IsValidTileForNewSettlement(tile, cannotPlaceTileReason, forGravship: true))
                        {
                            Messages.Message(cannotPlaceTileReason.ToString(), MessageTypeDefOf.RejectInput, historical: false);
                            return false;
                        }
                        return true;
                    }, tile =>
                    {
                        SettlementProximityGoodwillUtility.CheckConfirmSettle(tile, () =>
                        {
                            var target = Find.World.worldObjects.MapParentAt(tile);
                            if (target is null) return;

                            var result = Select(target);
                            var data = new TargetData<GlobalTargetInfo>();
                            data.targets.Add(target);
                            switch (result.action)
                            {
                                case TargeterAction.Cancel:
                                    SoundDefOf.CancelMode.PlayOneShotOnCamera(null);
                                    break;
                                case TargeterAction.Reject:
                                    SoundDefOf.ClickReject.PlayOneShotOnCamera(null);
                                    break;
                                case TargeterAction.Accept:
                                case TargeterAction.Submit:
                                    Assert.IsFalse(result.options.NullOrEmpty());
                                    if (result.options.NullOrEmpty())
                                    {
                                        Trace.Fail("Finalizing results with no options to choose.");
                                        return;
                                    }
                                    if (result.options.Count == 1)
                                    {
                                        ChooseOption(result.options[0]);
                                        return;
                                    }
                                    List<FloatMenuOption> list = [];
                                    foreach (ITargetOption option in result.options)
                                    {
                                        list.Add(new FloatMenuOption(option.Label, delegate
                                        {
                                            ChooseOption(option);
                                        }));
                                    }
                                    Find.WindowStack.Add(new FloatMenu(list));
                                    void ChooseOption(ITargetOption option2)
                                    {
                                        var arrivalOption = option2 as ArrivalOption;
                                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                                        if (arrivalOption?.continueWith != null)
                                        {
                                            arrivalOption.continueWith(data);
                                        }
                                        else
                                        {
                                            Launch(data, arrivalOption?.arrivalAction);
                                            SoundDefOf.Gravship_Launch.PlayOneShotOnCamera();
                                        }
                                    }
                                    break;
                            }
                        }, () => ChoosingDestination(assignment), engine);
                    }, () =>
                    {
                        WorldObject singleSelectedObject = Find.WorldSelector.SingleSelectedObject;
                        PlanetTile planetTile = GenWorld.MouseTile();
                        PlanetTile planetTile2 = ((!planetTile.Valid && singleSelectedObject != null) ? singleSelectedObject.Tile : planetTile);
                        Vector2 mousePosition = Event.current.mousePosition;
                        GUI.DrawTexture(new Rect(mousePosition.x + 8f, mousePosition.y + 8f, 32f, 32f), TexData.TargeterMouseAttachment);
                        if (planetTile2.Valid)
                        {
                            bool flag = false;
                            if (TileFinder.IsValidTileForNewSettlement(planetTile2, null, forGravship: true))
                            {
                                string text;
                                if (GravshipUtility.TryGetPathFuelCost(curTile, planetTile2, out var cost, out var distance, 10f, engine.FuelUseageFactor))
                                {
                                    flag = cost <= engine.TotalFuel && distance <= GetMaxLaunchDistance(PlanetLayer.Selected);
                                    text = $"{"Cost".Translate().CapitalizeFirst()}: {"FuelAmount".Translate(cost, ThingDefOf.Chemfuel)}";
                                    if (distance > GetMaxLaunchDistance(PlanetLayer.Selected))
                                    {
                                        text += string.Format(" ({0})", "TransportPodDestinationBeyondMaximumRange".Translate());
                                    }
                                    else if (!flag)
                                    {
                                        text += string.Format(" ({0})", "CannotLaunchNotEnoughFuel".Translate().CapitalizeFirst());
                                    }
                                    else if (!engine.HasSignalJammer && singleSelectedObject is MapParent && singleSelectedObject.RequiresSignalJammerToReach)
                                    {
                                        flag = false;
                                        text += string.Format(" ({0})", "TransportPodDestinationRequiresSignalJammer".Translate());
                                    }
                                }
                                else
                                {
                                    text = "CannotLaunchDestination".Translate();
                                }
                                if (singleSelectedObject != null && !planetTile.Valid)
                                {
                                    Widgets.WorldAttachedLabel(singleSelectedObject.DrawPos, text, 0f, 0f, flag ? Color.white : ColorLibrary.RedReadable);
                                }
                                else
                                {
                                    Widgets.MouseAttachedLabel(text, 0f, 0f, flag ? Color.white : ColorLibrary.RedReadable);
                                }
                            }
                        }
                    }, () =>
                    {
                        int maxLaunchDistance = GetMaxLaunchDistance(PlanetLayer.Selected);
                        int num = GravshipUtility.MaxDistForFuel(engine.TotalFuel, curLayer, PlanetLayer.Selected, 10f, engine.FuelUseageFactor);
                        PlanetTile planetTile = curTile;
                        if (curTile.Layer != Find.WorldSelector.SelectedLayer)
                        {
                            if (cachedClosestLayerTile.Layer != Find.WorldSelector.SelectedLayer || !cachedClosestLayerTile.Valid)
                            {
                                cachedClosestLayerTile = Find.WorldSelector.SelectedLayer.GetClosestTile(curTile);
                            }
                            planetTile = cachedClosestLayerTile;
                        }
                        GenDraw.DrawWorldRadiusRing(planetTile, maxLaunchDistance, CompPilotConsole.GetThrusterRadiusMat(planetTile));
                        if (num < maxLaunchDistance)
                        {
                            GenDraw.DrawWorldRadiusRing(planetTile, num, CompPilotConsole.GetFuelRadiusMat(planetTile));
                        }
                    }, allowEscape: true, () =>
                    {
                        CameraJumper.TryJump(parent, CameraJumper.MovementMode.Cut);
                    }, "ChooseWhereToLand".Translate(), showRandomButton: false, selectTileBehindObject: true, hideFormCaravanGizmo: true, canCancel: true, "MessageNoLandingSiteSelected".Translate());
                }
            }
        }
    }
}
