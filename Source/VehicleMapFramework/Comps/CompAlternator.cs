using RimWorld;
using System.Linq;
using Vehicles;
using Verse;
using static VehicleMapFramework.CompProperties_Alternator;

namespace VehicleMapFramework;

public class CompAlternator : CompPowerPlant
{
    new CompProperties_Alternator Props => (CompProperties_Alternator)props;

    public override void UpdateDesiredPowerOutput()
    {
        base.UpdateDesiredPowerOutput();
        CompFueledTravel compFueledTravel;
        ThingDef fuelType;
        FuelProperties fuelProps;
        float comsumptionRatePerTick;
        if (!parent.IsOnVehicleMapOf(out var vehicle) ||
            (compFueledTravel = vehicle.CompFueledTravel) == null ||
            (fuelType = compFueledTravel.Props?.fuelType) == null ||
            compFueledTravel.Props.ElectricPowered ||
            (fuelProps = Props.fuelConsumptionRates.FirstOrDefault(f => f.fuelDef == fuelType)) == null ||
            compFueledTravel.Fuel < (comsumptionRatePerTick = fuelProps.fuelConsumptionRate / 60000f))
        {
            PowerOutput = 0f;
            return;
        }
        if (PowerOutput > 0f)
        {
            compFueledTravel.ConsumeFuel(comsumptionRatePerTick);
        }
    }
}
