using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleRoleBuildable : VehicleRole
{

  public CompBuildableUpgrades upgradeComp;
  public VehicleRoleBuildable() { }

  public VehicleRoleBuildable(VehicleRoleBuildable reference)
  {
    if (string.IsNullOrEmpty(reference.key))
    {
      Log.Error("Missing Key on VehicleRole " + reference.label);
    }
    CopyFrom(reference);
    upgradeComp = reference.upgradeComp;
  }
}
