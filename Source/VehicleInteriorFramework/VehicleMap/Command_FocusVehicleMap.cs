using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class Command_FocusVehicleMap : Command
    {
        public static VehiclePawnWithMap FocuseLockedVehicle { get; set; }

        public static VehiclePawnWithMap FocusedVehicle {  get; set; }

        public override string Label
        {
            get
            {
                var vehicle = Find.Selector.SingleSelectedObject as VehiclePawnWithMap;
                if (vehicle == null || vehicle == Command_FocusVehicleMap.FocuseLockedVehicle)
                {
                    return "VIF.UnfocusVehicleMap".Translate();
                }
                return "VIF.FocusVehicleMap".Translate();
            }
        }

        public Command_FocusVehicleMap()
        {
            this.Order = 5000;
        }

        public override void ProcessInput(Event ev)
        {
            var vehicle = Find.Selector.SingleSelectedObject as VehiclePawnWithMap;
            if (vehicle != null && Command_FocusVehicleMap.FocuseLockedVehicle != vehicle)
            {
                Command_FocusVehicleMap.FocuseLockedVehicle = vehicle;
                Command_FocusVehicleMap.FocusedVehicle = vehicle;
            }
            else
            {
                Command_FocusVehicleMap.FocuseLockedVehicle = null;
                Command_FocusVehicleMap.FocusedVehicle = null;
            }
        }
    }
}
