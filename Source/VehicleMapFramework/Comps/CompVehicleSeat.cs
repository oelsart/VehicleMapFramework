using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using SmashTools;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class CompVehicleSeat : CompBuildableUpgrades, IAttackTarget
{
    private readonly List<(VehicleRoleHandler, VehicleUpgrade.RoleUpgrade)> handlers = [];
    
    Thing IAttackTarget.Thing => parent;
    
    LocalTargetInfo IAttackTarget.TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

    float IAttackTarget.TargetPriorityFactor => 1f;

    bool IAttackTarget.ThreatDisabled(IAttackTargetSearcher _) =>
        !handlers.SelectMany(h => h.Item1.thingOwner.InnerListForReading).Any();

    string ILoadReferenceable.GetUniqueLoadID() => parent.GetUniqueLoadID() + "_CompVehicleSeat";

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        if (parent.IsOnVehicleMapOf(out var vehicle) && selPawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly,
                false, false, TraverseMode.ByPawn, parent.Map, out var exitSpot, out var enterSpot, out var spotsQueue))
        {
            foreach (var floatMenuOption in from handler in vehicle.handlers
                     where handler.AreSlotsAvailable && handlerUniqueIDs.Any(h => h.id == handler.uniqueID)
                     let reservationManager = vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>()
                     let canOperate = handler.CanOperateRole(selPawn)
                     let reservedCount =
                         reservationManager?.GetReservation<VehicleHandlerReservation>(vehicle)
                             ?.ClaimantsOnHandler(handler) ?? 0
                     let label = (canOperate
                         ? "VF_BoardVehicle".Translate(handler.role.label,
                             (handler.role.Slots - (handler.thingOwner.Count + reservedCount)).ToString())
                         : "VF_BoardVehicleGroupFail".Translate(handler.role.label,
                             "VF_BoardFailureNonCombatant".Translate(selPawn.LabelShort)))
                     select new FloatMenuOption(label, delegate
                     {
                         var job = new Job(VMF_DefOf.VMF_BoardAcrossMaps, parent).SetSpotsToJobAcrossMaps(selPawn, exitSpot, enterSpot, spotsQueue);
                         vehicle.GiveLoadJob(selPawn, handler);
                         selPawn.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder);
                         if (!selPawn.Spawned)
                         {
                             return;
                         }
                         reservationManager?.Reserve<VehicleRoleHandler, VehicleHandlerReservation>(vehicle, selPawn, selPawn.CurJob, handler);
                     })
                     {
                         Disabled = !canOperate
                     })
            {
                yield return floatMenuOption;
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
            var exitBlocked = !parent.OccupiedRect().ExpandedBy(1).EdgeCells.NotNullAndAny(cell => cell.Walkable(parent.Map));
            foreach (var command_Action_PawnDrawer in from keyIDPair in handlerUniqueIDs
                     select vehicle.handlers.FirstOrDefault(h => h.uniqueID == keyIDPair.id)
                     into handler
                     where handler != null
                     from Pawn pawn in handler.thingOwner
                     where !vehicle.Drafted || !handler.role.HandlingTypes.HasFlag(HandlingType.Movement) ||
                           !vehicle.Spawned
                     select new Command_ActionPawnDrawer
                     {
                         defaultLabel = "VF_DisembarkSinglePawn".Translate((NamedArgument)pawn.LabelShort),
                         groupable = false,
                         pawn = pawn,
                         action = delegate
                         {
                             var caravan = pawn.GetCaravan();
                             caravan?.RemovePawn(pawn);
                             if (Find.WorldPawns.Contains(pawn))
                             {
                                 Find.WorldPawns.RemovePawn(pawn);
                             }
                             vehicle.DisembarkPawn(pawn);
                         }
                     })
            {
                if (exitBlocked)
                {
                    command_Action_PawnDrawer.Disable("VF_DisembarkNoExit".Translate());
                }
                yield return command_Action_PawnDrawer;
            }

            foreach (var gizmo in vehicle.AllComps.OfType<CompOpacityOverlay>().SelectMany(c => c.CompGetGizmosExtra()))
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
            handlers.AddRange(vehicle.handlers.Where(h => handlerUniqueIDs.Any(i => h.uniqueID == i.id))
                .Select(h => (h, Props.upgrades.OfType<VehicleUpgrade>().SelectMany(u => u.roles)
                    .FirstOrDefault(r => r?.key == h.role.key))));
        }
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        handlers.Clear();
    }

    public override void PostDraw()
    {
        base.PostDraw();
        if (!VehicleMapFramework.settings.drawPlanet && parent.IsOnVehicleMapOf(out var vehicle) && !vehicle.Spawned && !handlers.NullOrEmpty())
        {
            foreach (var handler in handlers)
            {
                if (handler.Item1.role.PawnRenderer != null)
                {
                    foreach (var pawn in handler.Item1.thingOwner)
                    {
                        var drawLoc = parent.DrawPos + handler.Item2.pawnRenderer.DrawOffsetFor(parent.BaseRotation());
                        var value = handler.Item1.role.PawnRenderer.RotFor(parent.BaseRotation());
                        pawn.Drawer.renderer.RenderPawnAt(drawLoc, value);
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
}
