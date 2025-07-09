using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompBuildableUpgrades : ThingComp
{
    public CompProperties_BuildableUpgrades Props => (CompProperties_BuildableUpgrades)props;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        if (parent.IsOnVehicleMapOf(out var vehicle))
        {
            foreach (var upgrade in Props.upgrades)
            {
                if (upgrade is VehicleUpgradeBuildable buildable)
                {
                    buildable.parent = this;
                    buildable.Unlock(vehicle, respawningAfterLoad);
                }
                else
                {
                    upgrade.Unlock(vehicle, respawningAfterLoad);
                }
            }
        }
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        if (map.IsVehicleMapOf(out var vehicle))
        {
            foreach (var upgrade in Props.upgrades)
            {
                if (upgrade is VehicleUpgradeBuildable buildable)
                {
                    buildable.parent = this;
                    buildable.Refund(vehicle);
                }
                else
                {
                    upgrade.Refund(vehicle);
                }
            }

            //あふれた分の燃料を消費させる
            CompFueledTravel comp;
            if ((comp = vehicle.CompFueledTravel) != null)
            {
                var fuel = comp.Fuel - comp.FuelCapacity;
                if (fuel > 0)
                {
                    comp.ConsumeFuel(fuel);
                }
            }
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (parent.IsOnVehicleMapOf(out var vehicle))
        {
            var turretRoleUpgrades = Props.upgrades.Where(u => u is VehicleUpgrade u2 && (u2.roles?.Any(r => r.handlingTypes == HandlingType.Turret) ?? false)).ToList();
            if (turretRoleUpgrades.Count != 0)
            {
                var turret = vehicle.CompVehicleTurrets?.Turrets.FirstOrDefault(t => handlerUniqueIDs.Any(h => (h.turretIds?.Contains(t.key) ?? false) || (h.turretIds?.Contains(t.groupKey) ?? false)));
                Command_Action command_Action = new()
                {
                    action = delegate ()
                    {
                        foreach (var upgrade in turretRoleUpgrades)
                        {
                            if (upgrade is VehicleUpgradeBuildable buildable)
                            {
                                buildable.parent = this;
                                buildable.Refund(vehicle);
                            }
                            else
                            {
                                upgrade.Refund(vehicle);
                            }
                        }
                        foreach (var upgrade in turretRoleUpgrades)
                        {
                            if (upgrade is VehicleUpgradeBuildable buildable)
                            {
                                buildable.parent = this;
                                buildable.Unlock(vehicle, false);
                            }
                            else
                            {
                                upgrade.Unlock(vehicle, false);
                            }
                        }
                    },
                    defaultLabel = "VMF_Reassign".Translate(),
                    defaultDesc = "VMF_ReassignDesc".Translate(),
                    icon = turret?.GizmoIcon ?? BaseContent.ClearTex,
                };
                yield return command_Action;
            }
        }
    }

    public override void PostExposeData()
    {
        Scribe_Collections.Look(ref handlerUniqueIDs, "handlerUniqueIDs", LookMode.Deep);
    }

    public List<UpgradeID> handlerUniqueIDs = [];
}

public class UpgradeID : IExposable
{
    public string key;

    public string editKey;

    public List<string> turretIds;

    public int id;

    public UpgradeID() { }

    public UpgradeID(string key, string editKey, List<string> turretIds, int id)
    {
        this.key = key;
        this.editKey = editKey;
        this.turretIds = turretIds;
        this.id = id;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref key, "key");
        Scribe_Values.Look(ref editKey, "editKey");
        Scribe_Collections.Look(ref turretIds, "turretIds", LookMode.Value);
        Scribe_Values.Look(ref id, "id");
    }
}
