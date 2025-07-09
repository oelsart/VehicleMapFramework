using RimWorld;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompAlternator : CompPowerPlant
{
    new CompProperties_Alternator Props => (CompProperties_Alternator)props;

    public VehiclePawnWithMap Vehicle
    {
        get
        {
            if (vehicle == null)
            {
                if (!parent.IsOnVehicleMapOf(out vehicle))
                {
                    VMF_Log.Error("Alternator is not on vehicle map.");
                }
            }
            return vehicle;
        }
    }

    private float ConsumptionRatePerTick
    {
        get
        {
            return fuelProps.fuelConsumptionRate / 60000f;
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);

        compFueledTravel = Vehicle?.CompFueledTravel;
        if (compFueledTravel == null || compFueledTravel.Props.ElectricPowered) return;

        var fuelType = compFueledTravel.Props.fuelType;
        if (fuelType == null) return;

        fuelProps = Props.fuelConsumptionRates.FirstOrDefault(f => f.fuelDef == fuelType);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map);
        vehicle = null;
        compFueledTravel = null;
        fuelProps = null;
    }

    public override void UpdateDesiredPowerOutput()
    {
        base.UpdateDesiredPowerOutput();
        if (compFueledTravel == null || fuelProps == null || compFueledTravel.Props.ElectricPowered || compFueledTravel.Fuel < ConsumptionRatePerTick)
        {
            base.PowerOutput = 0f;
        }

        if (base.PowerOutput > 0f)
        {
            compFueledTravel.ConsumeFuel(ConsumptionRatePerTick);
        }
    }

    private VehiclePawnWithMap vehicle;

    private CompFueledTravel compFueledTravel;

    private CompProperties_Alternator.FuelProperties fuelProps;
}
