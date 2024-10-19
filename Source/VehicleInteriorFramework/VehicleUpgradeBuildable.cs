using RimWorld;
using SmashTools;
using System.Linq;
using Vehicles;
using Verse;
using static Vehicles.VehicleUpgrade;

namespace VehicleInteriors
{
    public class VehicleUpgradeBuildable : VehicleUpgrade
    {
        public CompBuildableUpgrade parent;

        public override void Unlock(VehiclePawn vehicle, bool unlockingPostLoad)
        {
            if (!this.roles.NullOrEmpty<VehicleUpgrade.RoleUpgrade>() )
            {
                foreach (VehicleUpgrade.RoleUpgrade roleUpgrade in this.roles)
                {
                    if (roleUpgrade is RoleUpgradeBuildable roleUpgradeBuildable)
                    {
                        this.UpgradeRole(vehicle, roleUpgradeBuildable, false, unlockingPostLoad);
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

        public void UpgradeRole(VehiclePawn vehicle, RoleUpgradeBuildable roleUpgrade, bool isRefund, bool unlockingAfterLoad)
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
                    this.parent.handlerUniqueIDs.Remove(new UpgradeID(roleUpgrade.key, roleUpgrade.editKey, handler.uniqueID));
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
                    this.parent.handlerUniqueIDs.Remove(new UpgradeID(roleUpgrade.key, roleUpgrade.editKey, handler.uniqueID));
                    return;
                }
            }
            else
            {
                if (!unlockingAfterLoad)
                {
                    var role = RoleUpgradeBuildable.RoleFromUpgrade(roleUpgrade, this);
                    role.ResolveReferences(vehicle.VehicleDef);
                    role.AddUpgrade(roleUpgrade);
                    var handler = new VehicleHandlerBuildable(vehicle, role);
                    vehicle.handlers.Add(handler);
                    if (role.PawnRenderer != null)
                    {
                        vehicle.ResetRenderStatus();
                    }
                    this.parent.handlerUniqueIDs.Add(new UpgradeID(roleUpgrade.key, roleUpgrade.editKey, handler.uniqueID));
                }
                else
                {
                    var uniqueID = this.parent.handlerUniqueIDs.FirstOrDefault(h => h.key == roleUpgrade.key && h.editKey == roleUpgrade.editKey);
                    if (uniqueID == default)
                    {
                        Log.Error("[Vehicle Map Framework] No uniqueID corresponding to this role upgrade found.");
                    }
                    var handler = vehicle.handlers.FirstOrDefault(h => h.uniqueID == uniqueID.id);
                    if (handler == null)
                    {
                        Log.Error("Unable to edit " + roleUpgrade.editKey + ". Matching VehicleRole not found.");
                        return;
                    }
                    var role = RoleUpgradeBuildable.RoleFromUpgrade(roleUpgrade, this);
                    role.ResolveReferences(vehicle.VehicleDef);
                    role.AddUpgrade(roleUpgrade);
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
        public static VehicleRoleBuildable RoleFromUpgrade(RoleUpgradeBuildable upgrade, VehicleUpgradeBuildable parentUpgrade)
        {
            VehicleRoleBuildable vehicleRole = new VehicleRoleBuildable
            {
                key = upgrade.key,
                label = upgrade.label,
                upgradeSingle = parentUpgrade
            };
            upgrade.hitbox = new ComponentHitbox
            {
                Hitbox = parentUpgrade.parent.parent.OccupiedRect().Cells2D.ToList()
            };
            if (parentUpgrade.parent.parent.IsOnVehicleMapOf(out var vehicle))
            {
                var position = parentUpgrade.parent.parent.Position;
                var vehiclePos = vehicle.cachedDrawPos;
                upgrade.pawnRenderer = new Vehicles.PawnOverlayRenderer
                {
                    drawOffsetNorth = position.OrigToVehicleMap(vehicle, Rot8.North) - vehiclePos,
                    drawOffsetSouth = position.OrigToVehicleMap(vehicle, Rot8.South) - vehiclePos,
                    drawOffsetEast = position.OrigToVehicleMap(vehicle, Rot8.East) - vehiclePos,
                    drawOffsetWest = position.OrigToVehicleMap(vehicle, Rot8.West) - vehiclePos,
                    drawOffsetNorthEast = position.OrigToVehicleMap(vehicle, Rot8.NorthEast) - vehiclePos,
                    drawOffsetNorthWest = position.OrigToVehicleMap(vehicle, Rot8.NorthWest) - vehiclePos,
                    drawOffsetSouthEast = position.OrigToVehicleMap(vehicle, Rot8.SouthEast) - vehiclePos,
                    drawOffsetSouthWest = position.OrigToVehicleMap(vehicle, Rot8.SouthWest) - vehiclePos,
                };
            }
            vehicleRole.CopyFrom(upgrade);
            return vehicleRole;
        }
    }
}
