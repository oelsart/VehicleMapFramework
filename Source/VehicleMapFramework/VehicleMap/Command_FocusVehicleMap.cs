using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Command_FocusVehicleMap : Command
{
    public static VehiclePawnWithMap FocuseLockedVehicle { get; set; }

    public static VehiclePawnWithMap FocusedVehicle { get; set; }

    public override string Label
    {
        get
        {
            if (Find.Selector.SingleSelectedObject is not VehiclePawnWithMap vehicle || vehicle == FocuseLockedVehicle)
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
        if (Find.Selector.SingleSelectedObject is VehiclePawnWithMap vehicle && FocuseLockedVehicle != vehicle)
        {
            FocuseLockedVehicle = vehicle;
            FocusedVehicle = vehicle;
        }
        else
        {
            FocuseLockedVehicle = null;
            FocusedVehicle = null;
        }
    }
}
