using System;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public sealed class Command_FocusVehicleMap : Command
{
  // TODO 1.7: staticとstructをこのクラスから独立させる
  public static VehiclePawnWithMap FocusLockedVehicle { get; set; }

  public static VehiclePawnWithMap FocusedVehicle { get; set; }

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

  public static FocusVehicleScope FocusVehicle(VehiclePawnWithMap vehicle)
  {
    return new FocusVehicleScope(vehicle);
  }

  public readonly struct FocusVehicleScope : IDisposable
  {
    private readonly VehiclePawnWithMap tmpFocused;

    public FocusVehicleScope(VehiclePawnWithMap vehicle)
    {
      tmpFocused = FocusedVehicle;
      FocusedVehicle = vehicle;
    }

    public void Dispose()
    {
      FocusedVehicle = tmpFocused;
    }
  }
}