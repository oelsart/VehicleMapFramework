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
                VehiclePawnWithMapCache.cacheMode = true;
                this.vehicle.VehicleMap.mapDrawer.RegenerateEverythingNow();
                VehiclePawnWithMapCache.cacheMode = false;
            });
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref this.vehicle, "vehicle");
        }
    }
}