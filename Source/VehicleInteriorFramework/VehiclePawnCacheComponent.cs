using RimWorld.Planet;

namespace VehicleInteriors
{
    public class VehiclePawnCacheComponent : WorldComponent
    {
        public VehiclePawnCacheComponent(World world) : base(world)
        {
        }

        public override void FinalizeInit()
        {
            VehiclePawnWithMapCache.ClearCaches();
        }
    }
}
