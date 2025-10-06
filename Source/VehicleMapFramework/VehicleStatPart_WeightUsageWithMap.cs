using SmashTools;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleStatPart_WeightUsageWithMap : VehicleStatPart_WeightUsage
{
    private float Modifier(VehiclePawnWithMap vehicle)
    {
        var num = 0f;
        if (usageCurve != null)
        {
            var statValue = vehicle.GetStatValue(VMF_DefOf.MaximumPayload);
            if (statValue > 0f)
            {
                num = VehicleMapUtility.VehicleMapMass(vehicle) * VehicleMapFramework.settings.weightFactor / statValue;
            }
            num = usageCurve.Evaluate(num);
        }
        else
        {
            num = VehicleMapUtility.VehicleMapMass(vehicle) * VehicleMapFramework.settings.weightFactor;
        }
        return num;
    }

    public override float TransformValue(VehiclePawn vehicle, float value)
    {
        if (vehicle is VehiclePawnWithMap vehicleWithMap)
        {
            return operation.Apply(value, Modifier(vehicleWithMap));
        }
        return value;
    }

    public override string ExplanationPart(VehiclePawn vehicle)
    {
        if (vehicle is not VehiclePawnWithMap vehicleWithMap) return null;
        var statValue = vehicle.GetStatValue(VMF_DefOf.MaximumPayload).ToStringByStyle(ToStringStyle.FloatTwo);
        var value = string.Format(formatString.NullOrEmpty() ? statDef.formatString : formatString,
            VehicleMapUtility.VehicleMapMass(vehicleWithMap), statValue);
        return "VMF_StatsReport_MaximumPayload".Translate(value);
    }
}
