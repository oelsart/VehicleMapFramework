using System.Collections.Generic;
using System.Linq;
using SmashTools;
using Vehicles;
using Verse;
using PawnOverlayRenderer = Vehicles.PawnOverlayRenderer;

namespace VehicleMapFramework;


public class RoleUpgradeBuildable : VehicleUpgrade.RoleUpgrade
{
  public static VehicleRoleBuildable RoleFromUpgrade(RoleUpgradeBuildable upgrade,
    CompBuildableUpgrades compBuildableUpgrades, out RoleUpgradeBuildable upgrade2, List<string> turretIds = null)
  {
    upgrade2 = new RoleUpgradeBuildable
    {
      key = upgrade.key,
      label = upgrade.label,
      editKey = upgrade.editKey,
      remove = upgrade.remove,
      slots = upgrade.slots,
      slotsToOperate = upgrade.slotsToOperate,
      comfort = upgrade.comfort,
      turretIds = !turretIds.NullOrEmpty() ? turretIds : upgrade.turretIds,
      hitbox = upgrade.hitbox,
      exposed = upgrade.exposed,
      chanceToHit = upgrade.chanceToHit,
      pawnRenderer = upgrade.pawnRenderer
    };
    upgrade2.handlingTypes = (upgrade.handlingTypes == HandlingType.Turret && upgrade2.turretIds.NullOrEmpty())
      ? HandlingType.None
      : upgrade.handlingTypes;

    if (!upgrade2.turretIds.NullOrEmpty())
    {
      upgrade2.label += ": " + upgrade2.turretIds!.Select(i => i.CapitalizeFirst()).ToCommaList();
    }

    VehicleRoleBuildable vehicleRole = new()
    {
      key = upgrade2.key,
      label = upgrade2.label,
      upgradeComp = compBuildableUpgrades,
      sourceRenderer = upgrade.pawnRenderer
    };
    if (compBuildableUpgrades.parent.IsOnVehicleMapOf(out var vehicle))
    {
      var pawnRenderer = upgrade.pawnRenderer;
      if (pawnRenderer != null)
      {
        var thing = compBuildableUpgrades.parent;
        var rot = thing.Rotation;

        upgrade2.pawnRenderer = new PawnOverlayRenderer
        {
          showBody = pawnRenderer.showBody,
          north = new Rot4(pawnRenderer.north.AsInt + rot.AsInt),
          east = new Rot4(pawnRenderer.east.AsInt + rot.AsInt),
          south = new Rot4(pawnRenderer.south.AsInt + rot.AsInt),
          west = new Rot4(pawnRenderer.west.AsInt + rot.AsInt),
          northEast = new Rot4(pawnRenderer.northEast.AsInt + rot.AsInt),
          southEast = new Rot4(pawnRenderer.southEast.AsInt + rot.AsInt),
          southWest = new Rot4(pawnRenderer.southWest.AsInt + rot.AsInt),
          northWest = new Rot4(pawnRenderer.northWest.AsInt + rot.AsInt),
          layer = pawnRenderer.layer,
          layerNorth = pawnRenderer.layerNorth,
          layerEast = pawnRenderer.layerEast,
          layerSouth = pawnRenderer.layerSouth,
          layerWest = pawnRenderer.layerWest,
          layerNorthEast = pawnRenderer.layerNorthEast,
          layerSouthEast = pawnRenderer.layerSouthEast,
          layerSouthWest = pawnRenderer.layerSouthWest,
          layerNorthWest = pawnRenderer.layerNorthWest,
          angle = pawnRenderer.angle,
          angleNorth = pawnRenderer.angleNorth ?? (pawnRenderer.angleSouth + 180f) ?? pawnRenderer.angle,
          angleEast = pawnRenderer.angleEast ?? -pawnRenderer.angleWest ?? pawnRenderer.angle,
          angleSouth = pawnRenderer.angleSouth ?? (pawnRenderer.angleNorth + 180f) ?? pawnRenderer.angle,
          angleWest = pawnRenderer.angleWest ?? -pawnRenderer.angleEast ?? pawnRenderer.angle,
          angleNorthEast = rot.IsHorizontal
            ? Ext_Math.RotateAngle(
              (rot == Rot4.West ? pawnRenderer.angleWest : pawnRenderer.angleEast).GetValueOrDefault(), -45f)
            : pawnRenderer.angleNorthEast ?? pawnRenderer.angleNorthWest ?? pawnRenderer.angle + 45f,
          angleSouthEast = rot.IsHorizontal
            ? Ext_Math.RotateAngle(
              (rot == Rot4.East ? pawnRenderer.angleWest : pawnRenderer.angleEast).GetValueOrDefault(), 45f)
            : pawnRenderer.angleSouthEast ?? pawnRenderer.angleSouthWest ?? pawnRenderer.angle - 45f,
          angleSouthWest = rot.IsHorizontal
            ? Ext_Math.RotateAngle(
              (rot == Rot4.East ? pawnRenderer.angleWest : pawnRenderer.angleEast).GetValueOrDefault(), -45f)
            : pawnRenderer.angleSouthWest ?? pawnRenderer.angleSouthEast ?? pawnRenderer.angle + 45f,
          angleNorthWest = rot.IsHorizontal
            ? Ext_Math.RotateAngle(
              (rot == Rot4.West ? pawnRenderer.angleWest : pawnRenderer.angleEast).GetValueOrDefault(), 45f)
            : pawnRenderer.angleNorthWest ?? pawnRenderer.angleNorthEast ?? pawnRenderer.angle - 45f
        };
        upgrade2.pawnRenderer.SetDrawOffsets(vehicle, vehicleRole);
      }

      upgrade2.hitbox ??= new ComponentHitbox
      {
        Hitbox =
        [
          .. compBuildableUpgrades.parent.OccupiedRect()
            .MovedBy(VehicleMapUtility.MapCellToHitbox(vehicle)).Cells2D
        ]
      };
    }

    vehicleRole.CopyFrom(upgrade2);
    return vehicleRole;
  }
}