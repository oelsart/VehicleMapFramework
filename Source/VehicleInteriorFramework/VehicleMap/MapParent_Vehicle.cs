using RimWorld.Planet;
using Verse;

namespace VehicleInteriors
{
    public class MapParent_Vehicle : MapParent
    {
        public VehiclePawnWithMap vehicle;

        public override string Label
        {
            get
            {
                return $"{this.vehicle.Label}{"VMF_VehicleMap".Translate()}";
            }
        }

        public override void FinalizeLoading()
        {
            base.FinalizeLoading();
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                this.vehicle.VehicleMap.mapDrawer.RegenerateEverythingNow();
            });
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref this.vehicle, "vehicle");
        }
    }
}