using RimWorld.Planet;
using Verse;

namespace VehicleInteriors
{
    public class CacheUtility : WorldComponent
    {
        public CacheUtility(World world) : base(world)
        {
        }

        public override void FinalizeInit()
        {
            VehiclePawnWithMapCache.ClearCaches();
        }
    }
}
