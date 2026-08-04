using System.Collections.Generic;
using Vehicles;
using Verse;

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
          if (!unlockingPostLoad && roleUpgradeBuildable.handlingTypes.HasValue &&
              roleUpgradeBuildable.handlingTypes.Value.HasFlag(HandlingType.Turret))
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

  public void UpgradeRole(VehiclePawn vehicle, RoleUpgradeBuildable roleUpgrade, bool isRefund, bool unlockingAfterLoad,
    List<string> turretIds = null)
  {
    if (roleUpgrade.remove ^ isRefund)
    {
      var uniqueID =
        parent.handlerUniqueIDs.FirstOrDefault(h => h.key == roleUpgrade.key && h.editKey == roleUpgrade.editKey);
      if (uniqueID is null)
      {
        VMF_Log.Error("No uniqueID corresponding to this role upgrade found.");
        return;
      }

      var handlers = vehicle.handlers;
      var index = handlers.FindIndex(h =>
        h.uniqueID == uniqueID.id); //indexで検索しないと後のvehicle.handlers.Remove(handler)で最初の要素が消去されてしまう
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

        (parent.handlerUniqueIDs ??= []).Add(new UpgradeID(roleUpgrade2.key, roleUpgrade2.editKey,
          roleUpgrade2.turretIds, handler.uniqueID));
      }
      else
      {
        var uniqueID =
          parent.handlerUniqueIDs.FirstOrDefault(h => h.key == roleUpgrade.key && h.editKey == roleUpgrade.editKey);
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