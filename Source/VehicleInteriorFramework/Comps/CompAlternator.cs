using RimWorld;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class CompAlternator : CompPowerPlant
    {
        new CompProperties_Alternator Props => (CompProperties_Alternator)this.props;

        public VehiclePawnWithMap Vehicle
        {
            get
            {
                if (vehicle == null)
                {
                    if (!this.parent.IsOnVehicleMapOf(out vehicle))
                    {
                        Log.Error("[VehicleMapFramework] Alternator is not on vehicle map.");
                    }
                }
                return vehicle;
            }
        }

        private float ConsumptionRatePerTick
        {
            get
            {
                return this.fuelProps.fuelConsumptionRate / 60000f;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            this.compFueledTravel = this.Vehicle?.CompFueledTravel;
            if (compFueledTravel == null || compFueledTravel.Props.electricPowered) return;

            var fuelType = compFueledTravel.Props.fuelType;
            if (fuelType == null) return;

            this.fuelProps = this.Props.fuelConsumptionRates.FirstOrDefault(f => f.fuelDef == fuelType);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map);
            this.vehicle = null;
            this.compFueledTravel = null;
            this.fuelProps = null;
        }

        public override void UpdateDesiredPowerOutput()
        {
            base.UpdateDesiredPowerOutput();
            if (compFueledTravel == null || fuelProps == null || compFueledTravel.Props.electricPowered || compFueledTravel.Fuel < ConsumptionRatePerTick)
            {
                base.PowerOutput = 0f;
            }

            if (base.PowerOutput > 0f)
            {
                this.compFueledTravel.ConsumeFuel(ConsumptionRatePerTick);
            }
        }

        private VehiclePawnWithMap vehicle;

        private CompFueledTravel compFueledTravel;

        private CompProperties_Alternator.FuelProperties fuelProps;
    }
}
