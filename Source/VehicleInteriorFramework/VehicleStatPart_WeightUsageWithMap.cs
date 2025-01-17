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
                float statValue = vehicle.GetStatValue(VMF_DefOf.MaximumPayload);
                if (statValue > 0f)
                {
                    num = VehicleMapUtility.VehicleMapMass(vehicle) / statValue;
                }
                num = this.usageCurve.Evaluate(num);
            }
            else
            {
                num = VehicleMapUtility.VehicleMapMass(vehicle);
            }
            return num;
        }

        public override float TransformValue(VehiclePawn vehicle, float value)
        {
            if (vehicle is VehiclePawnWithMap vehicleWithMap)
            {
                return this.operation.Apply(value, this.Modifier(vehicleWithMap));
            }
            return value;
        }

        public override string ExplanationPart(VehiclePawn vehicle)
        {
            string value;
            if (vehicle is VehiclePawnWithMap vehicleWithMap)
            {
                var statValue = vehicle.GetStatValue(VMF_DefOf.MaximumPayload).ToStringByStyle(ToStringStyle.FloatTwo);
                if (this.formatString.NullOrEmpty())
                {
                    value = string.Format(this.statDef.formatString, VehicleMapUtility.VehicleMapMass(vehicleWithMap), statValue);
                }
                else
                {
                    value = string.Format(this.formatString, VehicleMapUtility.VehicleMapMass(vehicleWithMap), statValue);
                }
                return "VMF_StatsReport_MaximumPayload".Translate(value);
            }
            return null;
        }
    }
}
