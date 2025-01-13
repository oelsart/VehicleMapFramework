using RimWorld;
using SmashTools;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class VehicleStatPart_WeightUsageWithMap : VehicleStatPart_WeightUsage
    {
        protected float Modifier(VehiclePawnWithMap vehicle)
        {
            float num = 0f;
            if (this.usageCurve != null)
            {
                float statValue = vehicle.GetStatValue(VIF_DefOf.MaximumPayload);
                if (statValue > 0f)
                {
                    num = (MassUtility.InventoryMass(vehicle) + VehicleMapUtility.VehicleMapMass(vehicle)) / statValue;
                }
                num = this.usageCurve.Evaluate(num);
            }
            else
            {
                num = MassUtility.InventoryMass(vehicle) + VehicleMapUtility.VehicleMapMass(vehicle);
            }
            return num;
        }

        public override float TransformValue(VehiclePawn vehicle, float value)
        {
            if (vehicle is VehiclePawnWithMap vehicleWithMap)
            {
                return this.operation.Apply(value, this.Modifier(vehicleWithMap));
            }
            else
            {
                return this.operation.Apply(value, base.Modifier(vehicle));
            }
        }

        public override string ExplanationPart(VehiclePawn vehicle)
        {
            string value;
            if (vehicle is VehiclePawnWithMap vehicleWithMap)
            {
                var statValue = vehicle.GetStatValue(VIF_DefOf.MaximumPayload).ToStringByStyle(ToStringStyle.FloatTwo);
                if (this.formatString.NullOrEmpty())
                {
                    value = string.Format(this.statDef.formatString, MassUtility.InventoryMass(vehicleWithMap) + VehicleMapUtility.VehicleMapMass(vehicleWithMap), statValue);
                }
                else
                {
                    value = string.Format(this.formatString, MassUtility.InventoryMass(vehicleWithMap) + VehicleMapUtility.VehicleMapMass(vehicleWithMap), statValue);
                }
            }
            else
            {
                var statValue = vehicle.GetStatValue(VehicleStatDefOf.CargoCapacity).ToStringByStyle(ToStringStyle.FloatTwo);
                if (this.formatString.NullOrEmpty())
                {
                    value = string.Format(this.statDef.formatString, MassUtility.InventoryMass(vehicle) , statValue);
                }
                else
                {
                    value = string.Format(this.formatString, MassUtility.InventoryMass(vehicle), statValue);
                }
            }
            return "VF_StatsReport_CargoWeight".Translate(value);
        }
    }
}
