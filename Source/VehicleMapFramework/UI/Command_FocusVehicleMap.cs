using UnityEngine;
using Verse;
using static VehicleMapFramework.FocusVehicleScope;

namespace VehicleMapFramework;

public sealed class Command_FocusVehicleMap : Command
{
  public override string Label
  {
    get
    {
      if (Find.Selector.SingleSelectedObject is not VehiclePawnWithMap vehicle || vehicle == FocusLockedVehicle)
      {
        return "VMF_UnfocusVehicleMap".Translate();
      }

      return "VMF_FocusVehicleMap".Translate();
    }
  }

  public Command_FocusVehicleMap()
  {
    Order = 5000;
  }

  public override void ProcessInput(Event ev)
  {
    if (Find.Selector.SingleSelectedObject is VehiclePawnWithMap vehicle && FocusLockedVehicle != vehicle)
    {
      FocusLockedVehicle = vehicle;
      FocusedVehicle = vehicle;
    }
    else
    {
      FocusLockedVehicle = null;
      FocusedVehicle = null;
    }
  }
}