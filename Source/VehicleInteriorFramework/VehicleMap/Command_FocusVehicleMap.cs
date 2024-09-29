using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    public class Command_FocusVehicleMap : Command
    {
        public override string Label
        {
            get
            {
                var vehicle = Find.Selector.SingleSelectedObject as VehiclePawnWithInterior;
                if (vehicle == null || vehicle == VehicleMapUtility.FocusedVehicle)
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
            if (vehicle == VehicleMapUtility.FocusedVehicle)
            {
                vehicle = null;
            }
            VehicleMapUtility.FocusedVehicle = vehicle;
        }
    }
}
