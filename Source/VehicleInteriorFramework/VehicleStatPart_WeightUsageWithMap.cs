using RimWorld;
using SmashTools;
using Vehicles;
using Verse;

namespace VehicleInteriors;

public class VehicleStatPart_WeightUsageWithMap : VehicleStatPart_WeightUsage
{
    protected float Modifier(VehiclePawnWithMap vehicle)
    {
        float num = 0f;
        if (usageCurve != null)
        {
            float statValue = vehicle.GetStatValue(VMF_DefOf.MaximumPayload);
            if (statValue > 0f)
            {
                num = VehicleMapUtility.VehicleMapMass(vehicle) * VehicleInteriors.settings.weightFactor / statValue;
            }
            num = usageCurve.Evaluate(num);
        }
        else
        {
            num = VehicleMapUtility.VehicleMapMass(vehicle) * VehicleInteriors.settings.weightFactor;
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
        string value;
        if (vehicle is VehiclePawnWithMap vehicleWithMap)
        {
            var statValue = vehicle.GetStatValue(VMF_DefOf.MaximumPayload).ToStringByStyle(ToStringStyle.FloatTwo);
            if (formatString.NullOrEmpty())
            {
                value = string.Format(statDef.formatString, VehicleMapUtility.VehicleMapMass(vehicleWithMap), statValue);
            }
            else
            {
                value = string.Format(formatString, VehicleMapUtility.VehicleMapMass(vehicleWithMap), statValue);
            }
            return "VMF_StatsReport_MaximumPayload".Translate(value);
        }
        return null;
    }
}
