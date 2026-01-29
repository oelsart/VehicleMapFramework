using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Targeting;
using UnityEngine;
using Vehicles;
using Vehicles.World;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace VehicleMapFramework;

public class CompVehicleLauncherGravshipVehicle : CompVehicleLauncherWithMap, ITargeterSource<GlobalTargetInfo, ArrivalOption>
{
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
            if (gizmo is not Command_ActionHighlighter { Disabled: false } takeoffCommand) continue;
            if (!CanLaunchGravship(out var reason, out var engine, out var console, out var pilot, out var copilot))
            {
                takeoffCommand.Disable(reason);
            }
            else
            {
                takeoffCommand.action = () => StartChoosingDestination(Vehicle, engine, console, pilot, copilot);
            }
        }
    }

    public bool CanLaunchGravship(out string disableReason, out Building_GravEngine engine, out CompPilotConsole console, out Pawn pilot, out Pawn copilot)
    {
        disableReason = null;
        engine = null;
        console = null;
        pilot = null;
        copilot = null;
        if (Vehicle is not VehiclePawnWithMap vehicle ||
            !Vehicle.def.HasModExtension<VehicleMapProps_Gravship>()) return false;
        engine = GravshipUtility.GetPlayerGravEngine_NewTemp(vehicle.VehicleMap);
        if (engine is null)
        {
            disableReason = "CannotLaunchNoEngine".Translate().CapitalizeFirst();
            return false;
        }
        var pocketMapProperties = vehicle.VehicleMap.generatorDef?.pocketMapProperties;
        var flag = pocketMapProperties?.canLaunchGravship ?? false;
        try
        {
            pocketMapProperties?.canLaunchGravship = true;
            AcceptanceReport? report = null;
            if ((console = engine.GravshipComponents.OfType<CompPilotConsole>().FirstOrDefault(c => (report = c.CanUseNow()).Value.Accepted)) is null)
            {
                disableReason = report?.Reason ?? "PilotConsoleInaccessible".Translate().CapitalizeFirst();
                return false;
            }
        }
        finally
        {
            pocketMapProperties?.canLaunchGravship = flag;
        }

        pilot = vehicle.handlers?.FirstOrDefault(h => h.role?.key == "pilot")?.thingOwner?.InnerListForReading?.FirstOrDefault();
        if (pilot is null)
        {
            disableReason = "VMF_CannotLaunchNoPilot".Translate().CapitalizeFirst();
            return false;
        }
        copilot = vehicle.handlers?.FirstOrDefault(h => h.role?.key == "copilot")?.thingOwner?.InnerListForReading?.FirstOrDefault();
        return true;
    }

    private void StartChoosingDestination(VehiclePawn vehicle, Building_GravEngine engine, CompPilotConsole console, Pawn pilot, Pawn copilot)
    {
        if (AnyLeftToLoad)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmSendNotCompletelyLoadedPods".Translate(vehicle.LabelCapNoCount), OpenDialog));
            return;
        }
        OpenDialog();
        return;

        void OpenDialog()
        {
            var assignedSeats = new Dictionary<Pawn, VehicleRoleHandler>();
            VehicleRoleHandler copilotHandler = null;
            foreach (var handler in vehicle.handlers.Where(handler => handler is VehicleRoleHandlerBuildable))
            {
                if (handler.role?.key == "copilot") copilotHandler = handler;

                for (var index = handler.thingOwner.Count - 1; index >= 0; index--)
                {
                    var pawn = handler.thingOwner[index];
                    assignedSeats[pawn] = handler;
                    vehicle.DisembarkPawn(pawn);
                }
            }
            var ritual = (Precept_Ritual)pilot.Ideo.GetPrecept(PreceptDefOf.GravshipLaunch);
            var ritualObligation = ritual.activeObligations?.FirstOrDefault(o => ritual.obligationTargetFilter.CanUseTarget(
                console.parent,
                o).canUse);
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
            dialog = new Dialog_BeginRitual(ritual.LabelCap,
                ritual,
                console.parent,
                engine.Map,
                assignment =>
                {
                    ChoosingDestination(assignment);
                    return true;
                },
                pilot,
                ritualObligation,
                (pawn,
                        voluntary,
                        allowOtherIdeos) => pawn.GetLord() == null &&
                                            (!pawn.RaceProps.Animal ||
                                             ritual.behavior.def.roles.Any(r => r.AppliesToPawn(pawn,
                                                 out _,
                                                 console.parent,
                                                 null,
                                                 null,
                                                 null,
                                                 true))) &&
                                            !pawn.IsSubhuman &&
                                            (!ritual.ritualOnlyForIdeoMembers ||
                                             ritual.def.allowSpectatorsFromOtherIdeos ||
                                             (pawn.Ideo == ritual.ideo || !voluntary || allowOtherIdeos) ||
                                             pawn.IsPrisonerOfColony ||
                                             pawn.RaceProps.Animal ||
                                             (!forcedForRole.NullOrEmpty() && forcedForRole.ContainsValue(pawn))),
                null,
                null,
                forcedForRole,
                outcome);
            Find.WindowStack.Add(dialog);
            return;

            void ChoosingDestination(RitualRoleAssignments assignment)
            {
                var qualityRange = (FloatRange)AccessTools.Method(typeof(Dialog_BeginRitual),
                    "PredictedQuality").Invoke(dialog,
                [
                    null
                ]);
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
                    vehicle.TryAddPawn(assigned.Key,
                        assigned.Value);
                }
                var copilot2 = assignment.FirstAssignedPawn("copilot");
                if (copilot2 is not null && copilotHandler is not null)
                {
                    vehicle.TryAddPawn(copilot2,
                        copilotHandler);
                }
                CameraJumper.TryJump(CameraJumper.GetWorldTarget(Vehicle));
                Find.WorldSelector.ClearSelection();
                var curTile = Vehicle.Map.Tile;
                var curLayer = curTile.Layer;
                var cachedClosestLayerTile = PlanetTile.Invalid;
                StringBuilder cannotPlaceTileReason = new();

                Find.TilePicker.StartTargeting_NewTemp(tile =>
                    {
                        cannotPlaceTileReason.Clear();
                        if (!GravshipUtility.TryGetPathFuelCost(curTile,
                                tile,
                                out var cost,
                                out var distance,
                                10f,
                                engine.FuelUseageFactor) &&
                            !DebugSettings.ignoreGravshipRange)
                        {
                            Messages.Message("CannotLaunchDestination".Translate(),
                                MessageTypeDefOf.RejectInput,
                                historical: false);
                            return false;
                        }

                        if (!engine.HasSignalJammer &&
                            Find.WorldObjects.TryGetWorldObjectAt<MapParent>(tile,
                                out var wo) &&
                            wo.RequiresSignalJammerToReach)
                        {
                            Messages.Message("TransportPodDestinationRequiresSignalJammer".Translate(),
                                MessageTypeDefOf.RejectInput,
                                historical: false);
                            return false;
                        }

                        if (cost > engine.TotalFuel && !DebugSettings.ignoreGravshipRange)
                        {
                            Messages.Message("CannotLaunchNotEnoughFuel".Translate()
                                    .CapitalizeFirst(),
                                MessageTypeDefOf.RejectInput,
                                historical: false);
                            return false;
                        }

                        if (distance > GetMaxLaunchDistance(tile.Layer) && !DebugSettings.ignoreGravshipRange)
                        {
                            Messages.Message("TransportPodDestinationBeyondMaximumRange".Translate(),
                                MessageTypeDefOf.RejectInput,
                                historical: false);
                            return false;
                        }

                        if (tile != parent.Tile || Vehicle.Map.listerThings.AnyThingWithDef(ThingDefOf.GravAnchor))
                            return true;
                        Messages.Message("CannotLandOnSameTile".Translate(),
                            MessageTypeDefOf.RejectInput,
                            historical: false);
                        return false;

                    },
                    tile =>
                    {
                        var target = Find.World.worldObjects.MapParentAt(tile) ?? new GlobalTargetInfo(tile);
                        var result = Select(target);
                        var data = new TargetData<GlobalTargetInfo>();
                        data.targets.Add(target);
                        switch (result.action)
                        {
                            case TargeterAction.Cancel:
                                SoundDefOf.CancelMode.PlayOneShotOnCamera();
                                break;
                            case TargeterAction.Reject:
                                SoundDefOf.ClickReject.PlayOneShotOnCamera();
                                break;
                            case TargeterAction.Accept:
                            case TargeterAction.Submit:
                                if (result.options.NullOrEmpty())
                                {
                                    //Trace.Fail("Finalizing results with no options to choose.");
                                    return;
                                }

                                if (result.options.Count == 1)
                                {
                                    ChooseOption(result.options[0]);
                                    return;
                                }

                                List<FloatMenuOption> list =
                                [
                                ];
                                list.AddRange(result.options.Select(option => new FloatMenuOption(option.Label, () => ChooseOption(option))));

                                Find.WindowStack.Add(new FloatMenu(list));
                                break;

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
                                        Launch(data,
                                            arrivalOption?.arrivalAction);
                                        SoundDefOf.Gravship_Launch.PlayOneShotOnCamera();
                                    }
                                }
                            case TargeterAction.None:
                            default:
                                break;
                        }
                    },
                    () =>
                    {
                        var singleSelectedObject = Find.WorldSelector.SingleSelectedObject;
                        var planetTile = GenWorld.MouseTile();
                        var planetTile2 = ((!planetTile.Valid && singleSelectedObject != null)
                            ? singleSelectedObject.Tile
                            : planetTile);
                        var mousePosition = Event.current.mousePosition;
                        GUI.DrawTexture(new Rect(mousePosition.x + 8f,
                                mousePosition.y + 8f,
                                32f,
                                32f),
                            TexData.TargeterMouseAttachment);
                        if (!planetTile2.Valid) return;
                        var flag = false;
                        if (!TileFinder.IsValidTileForNewSettlement(planetTile2,
                                forGravship: true)) return;
                        string text;
                        if (GravshipUtility.TryGetPathFuelCost(curTile,
                                planetTile2,
                                out var cost,
                                out var distance,
                                10f,
                                engine.FuelUseageFactor))
                        {
                            flag = cost <= engine.TotalFuel &&
                                   distance <= GetMaxLaunchDistance(PlanetLayer.Selected);
                            text =
                                $"{"Cost".Translate().CapitalizeFirst()}: {"FuelAmount".Translate(cost, ThingDefOf.Chemfuel)}";
                            if (distance > GetMaxLaunchDistance(PlanetLayer.Selected))
                            {
                                text += $" ({"TransportPodDestinationBeyondMaximumRange".Translate()})";
                            }
                            else if (!flag)
                            {
                                text += $" ({"CannotLaunchNotEnoughFuel".Translate()
                                    .CapitalizeFirst()})";
                            }
                            else if (!engine.HasSignalJammer &&
                                     singleSelectedObject is MapParent &&
                                     singleSelectedObject.RequiresSignalJammerToReach)
                            {
                                flag = false;
                                text += $" ({"TransportPodDestinationRequiresSignalJammer".Translate()})";
                            }
                        }
                        else
                        {
                            text = "CannotLaunchDestination".Translate();
                        }

                        if (singleSelectedObject != null && !planetTile.Valid)
                        {
                            Widgets.WorldAttachedLabel(singleSelectedObject.DrawPos,
                                text,
                                0f,
                                0f,
                                flag
                                    ? Color.white
                                    : ColorLibrary.RedReadable);
                        }
                        else
                        {
                            Widgets.MouseAttachedLabel(text,
                                0f,
                                0f,
                                flag
                                    ? Color.white
                                    : ColorLibrary.RedReadable);
                        }
                    },
                    () =>
                    {
                        var maxLaunchDistance = GetMaxLaunchDistance(PlanetLayer.Selected);
                        var num = GravshipUtility.MaxDistForFuel(engine.TotalFuel,
                            curLayer,
                            PlanetLayer.Selected,
                            10f,
                            engine.FuelUseageFactor);
                        var planetTile = curTile;
                        if (curTile.Layer != Find.WorldSelector.SelectedLayer)
                        {
                            if (cachedClosestLayerTile.Layer != Find.WorldSelector.SelectedLayer ||
                                !cachedClosestLayerTile.Valid)
                            {
                                cachedClosestLayerTile =
                                    Find.WorldSelector.SelectedLayer.GetClosestTile_NewTemp(curTile);
                            }

                            planetTile = cachedClosestLayerTile;
                        }

                        GenDraw.DrawWorldRadiusRing(planetTile,
                            maxLaunchDistance,
                            CompPilotConsole.GetThrusterRadiusMat(planetTile));
                        if (num < maxLaunchDistance)
                        {
                            GenDraw.DrawWorldRadiusRing(planetTile,
                                num,
                                CompPilotConsole.GetFuelRadiusMat(planetTile));
                        }
                    },
                    allowEscape: true,
                    () =>
                    {
                        CameraJumper.TryJump(parent,
                            CameraJumper.MovementMode.Cut);
                    },
                    "ChooseWhereToLand".Translate(),
                    showRandomButton: false,
                    selectTileBehindObject: true,
                    hideFormCaravanGizmo: true,
                    canCancel: true,
                    noTileChosenMessage: "MessageNoLandingSiteSelected".Translate());
                return;

                int GetMaxLaunchDistance(PlanetLayer layer)
                {
                    return Mathf.FloorToInt((engine.MaxLaunchDistance / layer.Def.rangeDistanceFactor));
                }
            }
        }
    }
}