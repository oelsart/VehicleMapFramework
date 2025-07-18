using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class CompVehicleSeat : CompBuildableUpgrades
{
    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        if (parent.IsOnVehicleMapOf(out var vehicle) && selPawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, parent.Map, out var exitSpot, out var enterSpot))
        {
            foreach (var handler in vehicle.handlers)
            {
                if (handler.AreSlotsAvailable && handlerUniqueIDs.Any(h => h.id == handler.uniqueID))
                {
                    VehicleReservationManager reservationManager = vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>();
                    bool canOperate = handler.CanOperateRole(selPawn);
                    int reservedCount = reservationManager?.GetReservation<VehicleHandlerReservation>(vehicle)?.ClaimantsOnHandler(handler) ?? 0;
                    string label = (canOperate ? "VF_BoardVehicle".Translate(handler.role.label, (handler.role.Slots - (handler.thingOwner.Count + reservedCount)).ToString()) : "VF_BoardVehicleGroupFail".Translate(handler.role.label, "VF_BoardFailureNonCombatant".Translate(selPawn.LabelShort)));
                    FloatMenuOption floatMenuOption = new(label, delegate
                    {
                        if (handler == null)
                        {
                            Messages.Message("VF_HandlerNotEnoughRoom".Translate(selPawn, vehicle), MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        Job job = new Job(VMF_DefOf.VMF_BoardAcrossMaps, parent).SetSpotsToJobAcrossMaps(selPawn, exitSpot, enterSpot);
                        vehicle.GiveLoadJob(selPawn, handler);
                        selPawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.DraftedOrder), false);
                        if (!selPawn.Spawned)
                        {
                            return;
                        }
                        reservationManager?.Reserve<VehicleRoleHandler, VehicleHandlerReservation>(vehicle, selPawn, selPawn.CurJob, handler);
                    })
                    {
                        Disabled = !canOperate
                    };
                    yield return floatMenuOption;
                }
            }
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        if (parent.IsOnVehicleMapOf(out var vehicle))
        {
            bool exitBlocked = !parent.OccupiedRect().ExpandedBy(1).EdgeCells.NotNullAndAny(cell => cell.Walkable(parent.Map));
            foreach (var keyIDPair in handlerUniqueIDs)
            {
                var handler = vehicle.handlers.FirstOrDefault(h => h.uniqueID == keyIDPair.id);
                if (handler != null)
                {
                    foreach (var pawn in handler.thingOwner)
                    {
                        if (vehicle.Drafted && handler.role.HandlingTypes.HasFlag(HandlingType.Movement) && vehicle.Spawned) continue;

                        Command_ActionPawnDrawer command_Action_PawnDrawer = new()
                        {
                            defaultLabel = "VF_DisembarkSinglePawn".Translate(pawn.LabelShort),
                            groupable = false,
                            pawn = pawn,
                            action = delegate ()
                            {
                                var caravan = pawn.GetCaravan();
                                caravan?.RemovePawn(pawn);
                                if (Find.WorldPawns.Contains(pawn))
                                {
                                    Find.WorldPawns.RemovePawn(pawn);
                                }
                                vehicle.DisembarkPawn(pawn);
                            }
                        };
                        if (exitBlocked)
                        {
                            command_Action_PawnDrawer.Disable("VF_DisembarkNoExit".Translate());
                        }
                        yield return command_Action_PawnDrawer;
                    }
                }
            }

            foreach (var gizmo in vehicle.AllComps.Where(c => c is CompOpacityOverlay).SelectMany(c => c.CompGetGizmosExtra()))
            {
                yield return gizmo;
            }
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (parent.IsOnVehicleMapOf(out var vehicle))
        {
            vehicle.CompVehicleTurrets?.RecacheTurretPermissions();
            vehicle.RecachePawnCount();
            handlersToDraw = vehicle.handlers.Where(h => handlerUniqueIDs.Any(i => h.uniqueID == i.id))
                .Select(h => (h, Props.upgrades.SelectMany(u => (u as VehicleUpgrade).roles).FirstOrDefault(r => r?.key == h.role.key)));
        }
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map);
        handlersToDraw = null;
    }

    public override void PostDraw()
    {
        base.PostDraw();
        if (!VehicleMapFramework.settings.drawPlanet && parent.IsOnVehicleMapOf(out var vehicle) && !vehicle.Spawned && !handlersToDraw.NullOrEmpty())
        {
            foreach (var handler in handlersToDraw)
            {
                if (handler.Item1.role.PawnRenderer != null)
                {
                    foreach (Pawn pawn in handler.Item1.thingOwner)
                    {
                        Vector3 drawLoc = parent.DrawPos + handler.Item2.pawnRenderer.DrawOffsetFor(parent.BaseRotation());
                        Rot4 value = handler.Item1.role.PawnRenderer.RotFor(parent.BaseRotation());
                        pawn.Drawer.renderer.RenderPawnAt(drawLoc, new Rot4?(value), false);
                    }
                }
            }
        }
    }

    public override string CompInspectStringExtra()
    {
        if (VehicleMapFramework.settings.weightFactor == 0f) return null;

        if (parent.IsOnVehicleMapOf(out var vehicle))
        {
            var str = base.CompInspectStringExtra();
            var stat = vehicle.GetStatValue(VMF_DefOf.MaximumPayload);

            return str + $"{VMF_DefOf.MaximumPayload.LabelCap}:" +
                $" {(VehicleMapUtility.VehicleMapMass(vehicle) * VehicleMapFramework.settings.weightFactor).ToStringEnsureThreshold(2, 0)} /" +
                $" {stat.ToStringEnsureThreshold(2, 0)} {"kg".Translate()}";
        }
        return null;
    }

    private IEnumerable<(VehicleRoleHandler, VehicleUpgrade.RoleUpgrade)> handlersToDraw;
}
