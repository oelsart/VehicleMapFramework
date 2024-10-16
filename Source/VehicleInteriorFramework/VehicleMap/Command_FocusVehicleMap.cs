using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class Command_FocusVehicleMap : Command
    {
        public static VehiclePawnWithInterior FocuseLockedVehicle { get; set; }

        public static VehiclePawnWithInterior FocusedVehicle {  get; set; }

        public override string Label
        {
            get
            {
                var vehicle = Find.Selector.SingleSelectedObject as VehiclePawnWithInterior;
                if (vehicle == null || vehicle == Command_FocusVehicleMap.FocuseLockedVehicle)
                {
                    return "Unfocus Vehicle Map";
                }
                return "Focus Vehicle Map";
            }
        }

        public Command_FocusVehicleMap()
        {
            this.Order = 5000;
        }

        public override void ProcessInput(Event ev)
        {
            var vehicle = Find.Selector.SingleSelectedObject as VehiclePawnWithInterior;
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
