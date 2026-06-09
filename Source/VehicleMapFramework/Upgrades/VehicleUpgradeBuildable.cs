using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SmashTools;
using Vehicles;
using Verse;
using static Vehicles.VehicleUpgrade;
using PawnOverlayRenderer = Vehicles.PawnOverlayRenderer;

namespace VehicleMapFramework;

public class VehicleUpgradeBuildable : VehicleUpgrade
{
    public CompBuildableUpgrades parent;

    public override void Unlock(VehiclePawn vehicle, bool unlockingPostLoad)
    {
        if (!roles.NullOrEmpty())
        {
            foreach (var roleUpgrade in roles)
            {
                if (roleUpgrade is RoleUpgradeBuildable roleUpgradeBuildable)
                {
                    if (!unlockingPostLoad && roleUpgradeBuildable.handlingTypes.HasValue && roleUpgradeBuildable.handlingTypes.Value.HasFlag(HandlingType.Turret))
                    {
                        Find.WindowStack.Add(new Dialog_ChooseVehicleRoles(vehicle, roleUpgradeBuildable, this));
                    }
                    else
                    {
                        UpgradeRole(vehicle, roleUpgradeBuildable, false, unlockingPostLoad);
                    }
                }
                else
                {
                    UpgradeRole(vehicle, roleUpgrade, false, unlockingPostLoad);
                }
            }
        }
        if (retextureDef != null && !unlockingPostLoad)
        {
            vehicle.SetRetexture(retextureDef);
        }
        if (!armor.NullOrEmpty())
        {
            foreach (var armorUpgrade in armor)
            {
                if (!armorUpgrade.key.NullOrEmpty() && !armorUpgrade.statModifiers.NullOrEmpty() && parent?.parent != null)
                {
                    var component = vehicle.statHandler.GetComponent(armorUpgrade.key);
                    var type = armorUpgrade.type;
                    if (type != UpgradeType.Add)
                    {
                        if (type == UpgradeType.Set)
                        {
                            component.SetArmorModifiers[parent.parent.ThingID] = armorUpgrade.statModifiers;
                        }
                    }
                    else
                    {
                        component.AddArmorModifiers[parent.parent.ThingID] = armorUpgrade.statModifiers;
                    }
                }
            }
        }
        if (!health.NullOrEmpty())
        {
            foreach (var healthUpgrade in health)
            {
                if (!healthUpgrade.key.NullOrEmpty() && parent?.parent != null)
                {
                    var component2 = vehicle.statHandler.GetComponent(healthUpgrade.key);
                    if (healthUpgrade.value != null)
                    {
                        var type = healthUpgrade.type;
                        if (type != UpgradeType.Add)
                        {
                            if (type == UpgradeType.Set)
                            {
                                component2.SetHealthModifier = healthUpgrade.value.Value;
                            }
                        }
                        else
                        {
                            component2.AddHealthModifiers[parent.parent.ThingID] = healthUpgrade.value.Value;
                        }
                        component2.SetHealth(component2.MaxHealth);
                    }
                    if (healthUpgrade.depth != null)
                    {
                        component2.depthOverride = healthUpgrade.depth;
                    }
                }
            }
        }
    }

    public override void Refund(VehiclePawn vehicle)
    {
        if (!roles.NullOrEmpty())
        {
            for (var i = roles.Count - 1; i >= 0; i--)
            {
                if (roles[i] is RoleUpgradeBuildable roleUpgradeBuildable)
                {
                    UpgradeRole(vehicle, roleUpgradeBuildable, true, false);
                }
                else
                {
                    UpgradeRole(vehicle, roles[i], true, false);
                }
            }
        }
        if (retextureDef != null)
        {
            vehicle.SetRetexture(null);
        }
        if (!armor.NullOrEmpty())
        {
            foreach (var armorUpgrade in armor)
            {
                if (!armorUpgrade.key.NullOrEmpty() && !armorUpgrade.statModifiers.NullOrEmpty() && parent?.parent != null)
                {
                    var component = vehicle.statHandler.GetComponent(armorUpgrade.key);
                    var type = armorUpgrade.type;
                    if (type != UpgradeType.Add)
                    {
                        if (type == UpgradeType.Set)
                        {
                            component.SetArmorModifiers.Remove(parent.parent.ThingID);
                        }
                    }
                    else
                    {
                        component.AddArmorModifiers.Remove(parent.parent.ThingID);
                    }
                }
            }
        }
        if (!health.NullOrEmpty())
        {
            foreach (var healthUpgrade in health)
            {
                if (!healthUpgrade.key.NullOrEmpty() && parent?.parent != null)
                {
                    var component2 = vehicle.statHandler.GetComponent(healthUpgrade.key);
                    if (healthUpgrade.value != null)
                    {
                        var type = healthUpgrade.type;
                        if (type != UpgradeType.Add)
                        {
                            if (type == UpgradeType.Set)
                            {
                                component2.SetHealthModifier = -1f;
                            }
                        }
                        else
                        {
                            component2.AddHealthModifiers.Remove(parent.parent.ThingID);
                        }
                    }
                    if (healthUpgrade.depth != null)
                    {
                        component2.depthOverride = null;
                    }
                }
            }
        }
    }

    public void UpgradeRole(VehiclePawn vehicle, RoleUpgradeBuildable roleUpgrade, bool isRefund, bool unlockingAfterLoad, List<string> turretIds = null)
    {
        if (roleUpgrade.remove ^ isRefund)
        {
            var uniqueID = parent.handlerUniqueIDs.FirstOrDefault(h => h.key == roleUpgrade.key && h.editKey == roleUpgrade.editKey);
            if (uniqueID is null)
            {
                VMF_Log.Error("No uniqueID corresponding to this role upgrade found.");
                return;
            }
            var handlers = vehicle.handlers;
            var index = handlers.FindIndex(h => h.uniqueID == uniqueID.id); //indexで検索しないと後のvehicle.handlers.Remove(handler)で最初の要素が消去されてしまう
            if (index == -1)
            {
                VMF_Log.Error("Unable to edit " + roleUpgrade.editKey + ". Matching VehicleRole not found.");
                return;
            }
            var handler = handlers[index];
            for (var i = handler.thingOwner.Count - 1; i >= 0; i--)
            {
                vehicle.DisembarkPawn(handler.thingOwner[i]);
            }
            handler.role.RemoveUpgrade(roleUpgrade);
            vehicle.handlers.RemoveAt(index);
            parent.handlerUniqueIDs.RemoveAll(h => h.id == handler.uniqueID);
            vehicle.CompVehicleTurrets?.RecacheTurretPermissions();
        }
        else
        {
            if (!unlockingAfterLoad)
            {
                var role = RoleUpgradeBuildable.RoleFromUpgrade(roleUpgrade, parent, out var roleUpgrade2, turretIds);
                role.ResolveReferences(vehicle.VehicleDef);
                role.AddUpgrade(roleUpgrade2);
                var handler = new VehicleRoleHandlerBuildable(vehicle, role);
                vehicle.Handlers.Add(handler);
                if (role.PawnRenderer != null)
                {
                    vehicle.ResetRenderStatus();
                }
                (parent.handlerUniqueIDs ??= []).Add(new UpgradeID(roleUpgrade2.key, roleUpgrade2.editKey, roleUpgrade2.turretIds, handler.uniqueID));
            }
            else
            {
                var uniqueID = parent.handlerUniqueIDs.FirstOrDefault(h => h.key == roleUpgrade.key && h.editKey == roleUpgrade.editKey);
                if (uniqueID is null)
                {
                    VMF_Log.Error("No uniqueID corresponding to this role upgrade found.");
                    return;
                }
                var handler = vehicle.handlers.FirstOrDefault(h => h.uniqueID == uniqueID.id);
                if (handler == null)
                {
                    VMF_Log.Error("Unable to edit " + roleUpgrade.editKey + ". Matching VehicleRole not found.");
                    return;
                }
                var role = RoleUpgradeBuildable.RoleFromUpgrade(roleUpgrade, parent, out var roleUpgrade2, uniqueID.turretIds);
                role.ResolveReferences(vehicle.VehicleDef);
                role.AddUpgrade(roleUpgrade2);
                handler.role = role;
                if (role.PawnRenderer != null)
                {
                    vehicle.ResetRenderStatus();
                }
            }
        }
    }
}

public class RoleUpgradeBuildable : RoleUpgrade
{
    public static VehicleRoleBuildable RoleFromUpgrade(RoleUpgradeBuildable upgrade, CompBuildableUpgrades compBuildableUpgrades, out RoleUpgradeBuildable upgrade2, List<string> turretIds = null)
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
            pawnRenderer = upgrade.pawnRenderer,
        };
        upgrade2.handlingTypes = (upgrade.handlingTypes == HandlingType.Turret && upgrade2.turretIds.NullOrEmpty()) ? HandlingType.None : upgrade.handlingTypes;


        if (!upgrade2.turretIds.NullOrEmpty())
        {
            upgrade2.label += ": " + upgrade2.turretIds!.Select(i => i.CapitalizeFirst()).ToCommaList();
        }

        VehicleRoleBuildable vehicleRole = new()
        {
            key = upgrade2.key,
            label = upgrade2.label,
            upgradeComp = compBuildableUpgrades
        };
        if (compBuildableUpgrades.parent.IsOnVehicleMapOf(out var vehicle))
        {
            var pawnRenderer = upgrade.pawnRenderer;
            if (pawnRenderer != null)
            {
                var thing = compBuildableUpgrades.parent;
                var cacheMode = VehicleSectionLayerManager.CacheMode;
                VehicleSectionLayerManager.CacheMode = true;
                var position = GenThing.TrueCenter(thing.Position, thing.Rotation, thing.def.Size, 0f);
                VehicleSectionLayerManager.CacheMode = cacheMode;
                var vehiclePos = vehicle.cachedDrawPos;
                var rot = thing.Rotation;
                Rot8 rot8 = rot;
                var data = vehicle.VehicleDef.graphicData;

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
                    drawOffset = position.ToBaseMapCoord(vehicle, Rot8.North) - vehiclePos + data.DrawOffsetForRot(Rot4.North) + pawnRenderer.drawOffset,
                    drawOffsetNorth = position.ToBaseMapCoord(vehicle, Rot8.North) - vehiclePos + data.DrawOffsetForRot(Rot4.North) + pawnRenderer.DrawOffsetFor(new Rot4(Rot4.NorthInt + rot.AsInt)),
                    drawOffsetSouth = position.ToBaseMapCoord(vehicle, Rot8.South) - vehiclePos + data.DrawOffsetForRot(Rot4.South) + pawnRenderer.DrawOffsetFor(new Rot4(Rot4.SouthInt + rot.AsInt)),
                    drawOffsetEast = position.ToBaseMapCoord(vehicle, Rot8.East) - vehiclePos + data.DrawOffsetForRot(Rot4.East) + pawnRenderer.DrawOffsetFor(new Rot4(Rot4.EastInt + rot.AsInt)),
                    drawOffsetWest = position.ToBaseMapCoord(vehicle, Rot8.West) - vehiclePos + data.DrawOffsetForRot(Rot4.West) + pawnRenderer.DrawOffsetFor(new Rot4(Rot4.WestInt + rot.AsInt)),
                    drawOffsetNorthEast = position.ToBaseMapCoord(vehicle, Rot8.NorthEast) - vehiclePos + data.DrawOffsetForRot(Rot4.North).RotatedBy(45f) + (rot.IsHorizontal ? pawnRenderer.DrawOffsetFor(rot).RotatedBy(45f) : pawnRenderer.DrawOffsetFor(rot8.Rotated(Rot8.NorthEast))),
                    drawOffsetNorthWest = position.ToBaseMapCoord(vehicle, Rot8.NorthWest) - vehiclePos + data.DrawOffsetForRot(Rot4.North).RotatedBy(-45f) + (rot.IsHorizontal ? pawnRenderer.DrawOffsetFor(rot).RotatedBy(-45f) : pawnRenderer.DrawOffsetFor(rot8.Rotated(Rot8.NorthWest))),
                    drawOffsetSouthEast = position.ToBaseMapCoord(vehicle, Rot8.SouthEast) - vehiclePos + data.DrawOffsetForRot(Rot4.South).RotatedBy(-45f) + (rot.IsHorizontal ? pawnRenderer.DrawOffsetFor(rot.Opposite).RotatedBy(-45f) : pawnRenderer.DrawOffsetFor(rot8.Rotated(Rot8.SouthEast))),
                    drawOffsetSouthWest = position.ToBaseMapCoord(vehicle, Rot8.SouthWest) - vehiclePos + data.DrawOffsetForRot(Rot4.South).RotatedBy(45f) + (rot.IsHorizontal ? pawnRenderer.DrawOffsetFor(rot.Opposite).RotatedBy(45f) : pawnRenderer.DrawOffsetFor(rot8.Rotated(Rot8.SouthWest))),
                    angle = pawnRenderer.angle,
                    angleNorth = pawnRenderer.angleNorth ?? (pawnRenderer.angleSouth + 180f) ?? pawnRenderer.angle,
                    angleEast = pawnRenderer.angleEast ?? -pawnRenderer.angleWest ?? pawnRenderer.angle,
                    angleSouth = pawnRenderer.angleSouth ?? (pawnRenderer.angleNorth + 180f) ?? pawnRenderer.angle,
                    angleWest = pawnRenderer.angleWest ?? -pawnRenderer.angleEast ?? pawnRenderer.angle,
                    angleNorthEast = rot.IsHorizontal ? Ext_Math.RotateAngle((rot == Rot4.West ? pawnRenderer.angleWest : pawnRenderer.angleEast).GetValueOrDefault(), -45f) : pawnRenderer.angleNorthEast ?? pawnRenderer.angleNorthWest ?? pawnRenderer.angle + 45f,
                    angleSouthEast = rot.IsHorizontal ? Ext_Math.RotateAngle((rot == Rot4.East ? pawnRenderer.angleWest : pawnRenderer.angleEast).GetValueOrDefault(), 45f) : pawnRenderer.angleSouthEast ?? pawnRenderer.angleSouthWest ?? pawnRenderer.angle - 45f,
                    angleSouthWest = rot.IsHorizontal ? Ext_Math.RotateAngle((rot == Rot4.East ? pawnRenderer.angleWest : pawnRenderer.angleEast).GetValueOrDefault(), -45f) : pawnRenderer.angleSouthWest ?? pawnRenderer.angleSouthEast ?? pawnRenderer.angle + 45f,
                    angleNorthWest = rot.IsHorizontal ? Ext_Math.RotateAngle((rot == Rot4.West ? pawnRenderer.angleWest : pawnRenderer.angleEast).GetValueOrDefault(), 45f) : pawnRenderer.angleNorthWest ?? pawnRenderer.angleNorthEast ?? pawnRenderer.angle - 45f
                };
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
