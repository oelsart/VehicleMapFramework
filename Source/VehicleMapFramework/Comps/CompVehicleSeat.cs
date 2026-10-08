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
  public readonly List<(VehicleRoleHandler handler, VehicleUpgrade.RoleUpgrade upgrade)> handlers = [];
  public Dictionary<int, int> expiryTicks = [];

  Thing IAttackTarget.Thing => parent;

  LocalTargetInfo IAttackTarget.TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;

  float IAttackTarget.TargetPriorityFactor => 1f;

  bool IAttackTarget.ThreatDisabled(IAttackTargetSearcher _)
  {
    return !handlers.SelectMany(h => h.handler.thingOwner.InnerListForReading).Any();
  }

  string ILoadReferenceable.GetUniqueLoadID()
  {
    return parent.GetUniqueLoadID() + "_CompVehicleSeat";
  }

  protected void TickShort()
  {
    if (!parent.IsOnVehicleMapOf(out var vehicle))
      return;
    
    foreach (var handler in vehicle.Handlers)
    {
      if (expiryTicks.TryGetValue(handler.uniqueID, out var ticks) && Find.TickManager.TicksGame >= ticks)
      {
        if (!vehicle.Spawned || !vehicle.Drafted)
        {
          for (var i = handler.thingOwner.Count - 1; i >= 0; i--)
          {
            vehicle.DisembarkPawn(handler.thingOwner[i]);
          }
        }
        expiryTicks.Remove(handler.uniqueID);
      }
    }
  }

  protected void CleanupExpiryTicks()
  {
    if (!parent.IsOnVehicleMapOf(out var vehicle))
      return;
    
    foreach (var handler in vehicle.Handlers)
    {
      if (expiryTicks.ContainsKey(handler.uniqueID) && handler.thingOwner.Count == 0)
      {
        expiryTicks.Remove(handler.uniqueID);
      }
    }
  }

  public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
  {
    if (parent.IsOnVehicleMapOf(out var vehicle) &&
        selPawn.CanReach(parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, parent.Map))
    {
      foreach (var floatMenuOption in from handler in vehicle.handlers
               where handler.AreSlotsAvailableAndReservable && handlerUniqueIDs.Any(h => h.id == handler.uniqueID)
               let reservationManager = vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>()
               let canOperate = handler.CanOperateRole(selPawn)
               let reservedCount =
                 reservationManager?.GetReservation<VehicleHandlerReservation>(vehicle)
                   ?.ClaimantsOnHandler(handler) ?? 0
               let label = canOperate
                 ? "VF_BoardVehicle".Translate(handler.role.label,
                   (handler.role.Slots - (handler.thingOwner.Count + reservedCount)).ToString())
                 : "VF_BoardVehicleGroupFail".Translate(handler.role.label,
                   "VF_BoardFailureNonCombatant".Translate(selPawn.LabelShort))
               select new FloatMenuOption(label, () =>
               {
                 var job = JobMaker.MakeJob(VMF_DefOf.VMF_BoardAcrossMaps, parent);
                 vehicle.GiveLoadJob(selPawn, handler);
                 selPawn.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder);
                 if (!selPawn.Spawned)
                 {
                   return;
                 }

                 reservationManager?.Reserve<VehicleRoleHandler, VehicleHandlerReservation>(vehicle, selPawn,
                   selPawn.CurJob, handler);
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
               where !vehicle.Drafted || !vehicle.Spawned || !handler.RequiredForMovement
               select new Command_ActionPawnDrawer
               {
                 defaultLabel = "VF_DisembarkSinglePawn".Translate((NamedArgument)pawn.LabelShort),
                 groupable = false,
                 pawn = pawn,
                 action = () =>
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
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      if (parent.IsOnVehicleMapOf(out var vehicle))
      {
        vehicle.VehicleSeatComps.Add(this);
        vehicle.CompVehicleTurrets?.RecacheTurretPermissions();
        vehicle.RecachePawnCount();
        handlers.AddRange(vehicle.handlers.Where(h => handlerUniqueIDs.Any(i => h.uniqueID == i.id))
          .Select(h => (h, Props.upgrades.OfType<VehicleUpgrade>().SelectMany(u => u.roles)
            .FirstOrDefault(r => r?.key == h.role.key))));
        vehicle.AddEvent(VehicleEventDefOf.ScanShort, TickShort);
        vehicle.AddEvent(VehicleEventDefOf.PawnExited, CleanupExpiryTicks);
      }
    });
  }

  public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
  {
    base.PostDeSpawn(map, mode);
    handlers.Clear();
    if (map.IsVehicleMapOf(out var vehicle))
    {
      vehicle.VehicleSeatComps.Remove(this);
      vehicle.RemoveEvent(VehicleEventDefOf.ScanShort, TickShort);
    }
  }

  public override void PostDraw()
  {
    base.PostDraw();
    if (!VehicleMapFramework.settings.drawPlanet && parent.IsOnVehicleMapOf(out var vehicle) && !vehicle.Spawned &&
        !handlers.NullOrEmpty())
    {
      foreach (var handler in handlers)
      {
        if (handler.handler.role.PawnRenderer != null)
        {
          foreach (var pawn in handler.handler.thingOwner)
          {
            var drawLoc = parent.DrawPos + handler.upgrade.pawnRenderer.DrawOffsetFor(parent.BaseRotation());
            var value = handler.handler.role.PawnRenderer.RotFor(parent.BaseRotation());
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

  public override void PostExposeData()
  {
    base.PostExposeData();
    Scribe_Collections.Look(ref expiryTicks, nameof(expiryTicks), LookMode.Value, LookMode.Value);
    expiryTicks ??= [];
  }
}