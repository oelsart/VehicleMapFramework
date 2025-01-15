using RimWorld;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Vehicles;
using Verse;
using static Vehicles.VehicleUpgrade;

namespace VehicleInteriors
{
    public class VehicleUpgradeBuildable : VehicleUpgrade
    {
        public CompBuildableUpgrades parent;

        public override void Unlock(VehiclePawn vehicle, bool unlockingPostLoad)
        {
            if (!this.roles.NullOrEmpty<VehicleUpgrade.RoleUpgrade>() )
            {
                foreach (VehicleUpgrade.RoleUpgrade roleUpgrade in this.roles)
                {
                    if (roleUpgrade is RoleUpgradeBuildable roleUpgradeBuildable)
                    {
                        if (!unlockingPostLoad && roleUpgradeBuildable.handlingTypes.HasValue && roleUpgradeBuildable.handlingTypes.Value.HasFlag(HandlingTypeFlags.Turret))
                        {
                            Find.WindowStack.Add(new Dialog_ChooseVehicleRoles(vehicle, roleUpgradeBuildable, this));
                        }
                        else
                        {
                            this.UpgradeRole(vehicle, roleUpgradeBuildable, false, unlockingPostLoad);
                        }
                    }
                    else
                    {
                        this.UpgradeRole(vehicle, roleUpgrade, false, unlockingPostLoad);
                    }
                }
            }
            if (this.retextureDef != null && !unlockingPostLoad)
            {
                vehicle.SetRetexture(this.retextureDef);
            }
            if (!this.armor.NullOrEmpty<VehicleUpgrade.ArmorUpgrade>())
            {
                foreach (VehicleUpgrade.ArmorUpgrade armorUpgrade in this.armor)
                {
                    if (!armorUpgrade.key.NullOrEmpty() && !armorUpgrade.statModifiers.NullOrEmpty<StatModifier>())
                    {
                        VehicleComponent component = vehicle.statHandler.GetComponent(armorUpgrade.key);
                        UpgradeType type = armorUpgrade.type;
                        if (type != UpgradeType.Add)
                        {
                            if (type == UpgradeType.Set)
                            {
                                component.SetArmorModifiers[this.node.key] = armorUpgrade.statModifiers;
                            }
                        }
                        else
                        {
                            component.AddArmorModifiers[this.node.key] = armorUpgrade.statModifiers;
                        }
                    }
                }
            }
            if (!this.health.NullOrEmpty<VehicleUpgrade.HealthUpgrade>())
            {
                foreach (VehicleUpgrade.HealthUpgrade healthUpgrade in this.health)
                {
                    if (!healthUpgrade.key.NullOrEmpty())
                    {
                        VehicleComponent component2 = vehicle.statHandler.GetComponent(healthUpgrade.key);
                        if (healthUpgrade.value != null)
                        {
                            UpgradeType type = healthUpgrade.type;
                            if (type != UpgradeType.Add)
                            {
                                if (type == UpgradeType.Set)
                                {
                                    component2.SetHealthModifier = (float)healthUpgrade.value.Value;
                                }
                            }
                            else
                            {
                                component2.AddHealthModifiers[this.node.key] = (float)healthUpgrade.value.Value;
                            }
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
            if (!this.roles.NullOrEmpty<VehicleUpgrade.RoleUpgrade>())
            {
                for (var i = 0; i < this.roles.Count; i++)
                {
                    if (this.roles[i] is RoleUpgradeBuildable roleUpgradeBuildable)
                    {
                        this.UpgradeRole(vehicle, roleUpgradeBuildable, true, false);
                    }
                    else
                    {
                        this.UpgradeRole(vehicle, this.roles[i], true, false);
                    }
                }
            }
            if (this.retextureDef != null)
            {
                vehicle.SetRetexture(null);
            }
            if (!this.armor.NullOrEmpty<VehicleUpgrade.ArmorUpgrade>())
            {
                foreach (VehicleUpgrade.ArmorUpgrade armorUpgrade in this.armor)
                {
                    if (!armorUpgrade.key.NullOrEmpty() && !armorUpgrade.statModifiers.NullOrEmpty<StatModifier>())
                    {
                        VehicleComponent component = vehicle.statHandler.GetComponent(armorUpgrade.key);
                        UpgradeType type = armorUpgrade.type;
                        if (type != UpgradeType.Add)
                        {
                            if (type == UpgradeType.Set)
                            {
                                component.SetArmorModifiers.Remove(this.node.key);
                            }
                        }
                        else
                        {
                            component.AddArmorModifiers.Remove(this.node.key);
                        }
                    }
                }
            }
            if (!this.health.NullOrEmpty<VehicleUpgrade.HealthUpgrade>())
            {
                foreach (VehicleUpgrade.HealthUpgrade healthUpgrade in this.health)
                {
                    if (!healthUpgrade.key.NullOrEmpty())
                    {
                        VehicleComponent component2 = vehicle.statHandler.GetComponent(healthUpgrade.key);
                        if (healthUpgrade.value != null)
                        {
                            UpgradeType type = healthUpgrade.type;
                            if (type != UpgradeType.Add)
                            {
                                if (type == UpgradeType.Set)
                                {
                                    component2.SetHealthModifier = -1f;
                                }
                            }
                            else
                            {
                                component2.AddHealthModifiers.Remove(this.node.key);
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
                var uniqueID = this.parent.handlerUniqueIDs.FirstOrDefault(h => h.key == roleUpgrade.key && h.editKey == roleUpgrade.editKey);
                if (uniqueID == default)
                {
                    Log.Error("[VehicleMapFramework] No uniqueID corresponding to this role upgrade found.");
                    return;
                }
                var handlers = vehicle.handlers;
                var index = handlers.FindIndex(h => h.uniqueID == uniqueID.id); //indexで検索しないと後のvehicle.handlers.Remove(handler)で最初の要素が消去されてしまう
                if (index == -1)
                {
                    Log.Error("Unable to edit " + roleUpgrade.editKey + ". Matching VehicleRole not found.");
                    return;
                }
                var handler = handlers[index];
                handler.role.RemoveUpgrade(roleUpgrade);
                for (var i = 0; i < handler.handlers.Count; i++)
                {
                    vehicle.DisembarkPawn(handler.handlers[i]);
                }
                vehicle.handlers.RemoveAt(index);
                this.parent.handlerUniqueIDs.RemoveAll(h => h.id == handler.uniqueID);
                vehicle.CompVehicleTurrets?.RecacheTurretPermissions();
                return;
            }
            else
            {
                if (!unlockingAfterLoad)
                {
                    var role = RoleUpgradeBuildable.RoleFromUpgrade(roleUpgrade, this, out var roleUpgrade2, turretIds);
                    role.ResolveReferences(vehicle.VehicleDef);
                    role.AddUpgrade(roleUpgrade2);
                    var handler = new VehicleHandlerBuildable(vehicle, role);
                    vehicle.handlers.Add(handler);
                    if (role.PawnRenderer != null)
                    {
                        vehicle.ResetRenderStatus();
                    }   
                    this.parent.handlerUniqueIDs.Add(new UpgradeID(roleUpgrade2.key, roleUpgrade2.editKey, roleUpgrade2.turretIds, handler.uniqueID));
                }
                else
                {
                    var uniqueID = this.parent.handlerUniqueIDs.FirstOrDefault(h => h.key == roleUpgrade.key && h.editKey == roleUpgrade.editKey);
                    if (uniqueID == default)
                    {
                        Log.Error("[VehicleMapFramework] No uniqueID corresponding to this role upgrade found.");
                        return;
                    }
                    VehicleHandler handler = vehicle.handlers.FirstOrDefault(h => h.uniqueID == uniqueID.id);
                    if (handler == null)
                    {
                        Log.Error("Unable to edit " + roleUpgrade.editKey + ". Matching VehicleRole not found.");
                        return;
                    }
                    var role = RoleUpgradeBuildable.RoleFromUpgrade(roleUpgrade, this, out var roleUpgrade2, uniqueID.turretIds);
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
        public static VehicleRoleBuildable RoleFromUpgrade(RoleUpgradeBuildable upgrade, VehicleUpgradeBuildable parentUpgrade, out RoleUpgradeBuildable upgrade2, List<string> turretIds = null)
        {
            upgrade2 = new RoleUpgradeBuildable()
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
            upgrade2.handlingTypes = (upgrade.handlingTypes == HandlingTypeFlags.Turret && upgrade2.turretIds.NullOrEmpty()) ? HandlingTypeFlags.None : upgrade.handlingTypes;


            if (!upgrade2.turretIds.NullOrEmpty())
            {
                upgrade2.label += ": " + upgrade2.turretIds.Select(i => i.CapitalizeFirst()).ToCommaList();
            }

            VehicleRoleBuildable vehicleRole = new VehicleRoleBuildable
            {
                key = upgrade2.key,
                label = upgrade2.label,
                upgradeSingle = parentUpgrade
            };
            if (parentUpgrade.parent.parent.IsOnVehicleMapOf(out var vehicle))
            {
                var pawnRenderer = upgrade.pawnRenderer;
                if (pawnRenderer != null)
                {
                    var thing = parentUpgrade.parent.parent;
                    var position = GenThing.TrueCenter(thing.Position, thing.Rotation, thing.def.Size, 0f);
                    var vehiclePos = vehicle.DrawPos.WithY(0f);
                    var rot = thing.Rotation;
                    var angle = rot.AsAngle;
                    var intClockwise = new Rot8(rot).AsIntClockwise;

                    upgrade2.pawnRenderer = new Vehicles.PawnOverlayRenderer
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
                        drawOffset = position.OrigToVehicleMap(vehicle, Rot8.North) - vehiclePos + pawnRenderer.drawOffset,
                        drawOffsetNorth = position.OrigToVehicleMap(vehicle, Rot8.North) - vehiclePos + pawnRenderer.DrawOffsetFor(new Rot4(Rot4.NorthInt + rot.AsInt)),
                        drawOffsetSouth = position.OrigToVehicleMap(vehicle, Rot8.South) - vehiclePos + pawnRenderer.DrawOffsetFor(new Rot4(Rot4.SouthInt + rot.AsInt)),
                        drawOffsetEast = position.OrigToVehicleMap(vehicle, Rot8.East) - vehiclePos + pawnRenderer.DrawOffsetFor(new Rot4(Rot4.EastInt + rot.AsInt)),
                        drawOffsetWest = position.OrigToVehicleMap(vehicle, Rot8.West) - vehiclePos + pawnRenderer.DrawOffsetFor(new Rot4(Rot4.WestInt + rot.AsInt)),
                        drawOffsetNorthEast = position.OrigToVehicleMap(vehicle, Rot8.NorthEast) - vehiclePos + (rot.IsHorizontal ? pawnRenderer.DrawOffsetFor(rot).RotatedBy(45f) : pawnRenderer.DrawOffsetFor(new Rot8(Rot8.FromIntClockwise((intClockwise + Rot8.NorthEast.AsIntClockwise) % 8)))),
                        drawOffsetNorthWest = position.OrigToVehicleMap(vehicle, Rot8.NorthWest) - vehiclePos + (rot.IsHorizontal ? pawnRenderer.DrawOffsetFor(rot).RotatedBy(-45f) : pawnRenderer.DrawOffsetFor(new Rot8(Rot8.FromIntClockwise((intClockwise + Rot8.NorthWest.AsIntClockwise) % 8)))),
                        drawOffsetSouthEast = position.OrigToVehicleMap(vehicle, Rot8.SouthEast) - vehiclePos + (rot.IsHorizontal ? pawnRenderer.DrawOffsetFor(rot.Opposite).RotatedBy(-45f) : pawnRenderer.DrawOffsetFor(new Rot8(Rot8.FromIntClockwise((intClockwise + Rot8.SouthEast.AsIntClockwise) % 8)))),
                        drawOffsetSouthWest = position.OrigToVehicleMap(vehicle, Rot8.SouthWest) - vehiclePos + (rot.IsHorizontal ? pawnRenderer.DrawOffsetFor(rot.Opposite).RotatedBy(45f) : pawnRenderer.DrawOffsetFor(new Rot8(Rot8.FromIntClockwise((intClockwise + Rot8.SouthWest.AsIntClockwise) % 8)))),
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

                if (upgrade2.hitbox == null)
                {
                    var orig = Vector3.zero.OrigToVehicleMap(vehicle).VehicleMapToOrig(vehicle).ToIntVec3();
                    upgrade2.hitbox = new ComponentHitbox
                {
                    Hitbox = parentUpgrade.parent.parent.OccupiedRect().MovedBy(orig).Cells2D.ToList()
                };
                }
            }
            vehicleRole.CopyFrom(upgrade2);
            return vehicleRole;
        }
    }
}
