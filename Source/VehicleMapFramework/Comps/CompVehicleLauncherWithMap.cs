using System.Collections.Generic;
using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompVehicleLauncherWithMap : CompVehicleLauncher
{
  public override IEnumerable<Gizmo> CompGetGizmosExtra()
  {
    foreach (var gizmo in base.CompGetGizmosExtra())
    {
      if (gizmo is Command_ActionHighlighter takeoffCommand)
      {
        takeoffCommand.Disabled = false;
        if (!CanLaunchWithCargoCapacityWithMap(out var reason))
        {
          takeoffCommand.Disable(reason);
        }
      }

      yield return gizmo;
    }
  }

  public bool CanLaunchWithCargoCapacityWithMap(out string disableReason)
  {
    if (!CanLaunchWithCargoCapacity(out disableReason))
      return false;

    if ((Vehicle.MovementPermissions & VehiclePermissions.Mobile) == 0 && Vehicle is VehiclePawnWithMap vehicleWithMap)
    {
      var maximumPayload = Vehicle.GetStatValue(VMF_DefOf.MaximumPayload);
      var mass2 = CollectionsMassCalculator.MassUsage(vehicleWithMap.VehicleMap.listerThings.AllThings,
        IgnorePawnsInventoryMode.DontIgnore, true);
      if (mass2 > maximumPayload)
      {
        disableReason = "VF_CannotLaunchOverEncumbered".Translate(Vehicle.LabelShort);
        return false;
      }
    }

    return true;
  }
}
