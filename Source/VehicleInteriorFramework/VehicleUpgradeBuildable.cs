using RimWorld;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
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
                VehicleHandler handler = vehicle.handlers.FirstOrDefault(h => h.role is VehicleRoleBuildable buildable && buildable.upgradeSingle == this);
                if (!roleUpgrade.editKey.NullOrEmpty())
                {
                    if (handler == null)
                    {
                        Log.Error("Unable to edit " + roleUpgrade.editKey + ". Matching VehicleRole not found.");
                        return;
                    }
                    handler.role.RemoveUpgrade(roleUpgrade);
                    for (var i = 0; i < handler.handlers.Count; i++)
                    {
                        vehicle.DisembarkPawn(handler.handlers[i]);
                    }
                    vehicle.handlers.Remove(handler);
                    this.parent.handlerUniqueIDs.RemoveAll(h => h.id == handler.uniqueID);
                    return;
                }
                else if (!unlockingAfterLoad)
                {
                    if (handler == null)
                    {
                        Log.Error(string.Format("Unable to remove {0} from {1}. Role not found.", roleUpgrade.key, vehicle.Name));
                        return;
                    }
                    handler.role.RemoveUpgrade(roleUpgrade);
                    for (var i = 0; i < handler.handlers.Count; i++)
                    {
                        vehicle.DisembarkPawn(handler.handlers[i]);
                    }
                    vehicle.handlers.Remove(handler);
                    this.parent.handlerUniqueIDs.RemoveAll(h => h.id == handler.uniqueID);
                    return;
                }
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
                    }
                    var handler = vehicle.handlers.FirstOrDefault(h => h.uniqueID == uniqueID.id);
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
            upgrade2.handlingTypes = upgrade.handlingTypes == HandlingTypeFlags.Turret && upgrade2.turretIds.NullOrEmpty() ? HandlingTypeFlags.None : upgrade.handlingTypes;


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
                var thing = parentUpgrade.parent.parent;
                var position = GenThing.TrueCenter(thing.Position, thing.Rotation, thing.def.Size, 0f);
                var vehiclePos = vehicle.cachedDrawPos.WithY(0f);
                var pawnRenderer = upgrade.pawnRenderer;
                if (pawnRenderer != null)
                {
                    upgrade2.pawnRenderer = new Vehicles.PawnOverlayRenderer
                    {
                        showBody = pawnRenderer.showBody,
                        north = pawnRenderer.north,
                        east = pawnRenderer.east,
                        south = pawnRenderer.south,
                        west = pawnRenderer.west,
                        northEast = pawnRenderer.northEast,
                        southEast = pawnRenderer.southEast,
                        southWest = pawnRenderer.southWest,
                        northWest = pawnRenderer.northWest,
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
                        drawOffsetNorth = position.OrigToVehicleMap(vehicle, Rot8.North) - vehiclePos + pawnRenderer.DrawOffsetFor(Rot8.North),
                        drawOffsetSouth = position.OrigToVehicleMap(vehicle, Rot8.South) - vehiclePos + pawnRenderer.DrawOffsetFor(Rot8.South),
                        drawOffsetEast = position.OrigToVehicleMap(vehicle, Rot8.East) - vehiclePos + pawnRenderer.DrawOffsetFor(Rot8.East),
                        drawOffsetWest = position.OrigToVehicleMap(vehicle, Rot8.West) - vehiclePos + pawnRenderer.DrawOffsetFor(Rot8.West),
                        drawOffsetNorthEast = position.OrigToVehicleMap(vehicle, Rot8.NorthEast) - vehiclePos + pawnRenderer.DrawOffsetFor(Rot8.NorthEast),
                        drawOffsetNorthWest = position.OrigToVehicleMap(vehicle, Rot8.NorthWest) - vehiclePos + pawnRenderer.DrawOffsetFor(Rot8.NorthWest),
                        drawOffsetSouthEast = position.OrigToVehicleMap(vehicle, Rot8.SouthEast) - vehiclePos + pawnRenderer.DrawOffsetFor(Rot8.SouthEast),
                        drawOffsetSouthWest = position.OrigToVehicleMap(vehicle, Rot8.SouthWest) - vehiclePos + pawnRenderer.DrawOffsetFor(Rot8.SouthWest),
                        angle = pawnRenderer.angle,
                        angleNorth = pawnRenderer.angleNorth,
                        angleEast = pawnRenderer.angleEast,
                        angleSouth = pawnRenderer.angleSouth,
                        angleWest = pawnRenderer.angleWest,
                        angleNorthEast = pawnRenderer.angleNorthEast,
                        angleSouthEast = pawnRenderer.angleSouthEast,
                        angleSouthWest = pawnRenderer.angleSouthWest,
                        angleNorthWest = pawnRenderer.angleNorthWest
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
