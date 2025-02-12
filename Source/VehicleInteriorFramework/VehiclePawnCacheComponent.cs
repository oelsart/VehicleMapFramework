using RimWorld.Planet;

namespace VehicleInteriors
{
    public class VehiclePawnCacheComponent : WorldComponent
    {
        public VehiclePawnCacheComponent(World world) : base(world)
        {
            Command_FocusVehicleMap.FocuseLockedVehicle = null;
            Command_FocusVehicleMap.FocusedVehicle = null;
        }

        public override void FinalizeInit()
        {
            VehiclePawnWithMapCache.ClearCaches();
        }
    }
}