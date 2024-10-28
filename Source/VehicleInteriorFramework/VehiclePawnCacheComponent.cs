using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

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
