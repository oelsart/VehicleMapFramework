using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public class VehicleRoleBuildable : VehicleRole
    {
        public VehicleRoleBuildable()
        {
        }

        public VehicleRoleBuildable(VehicleRoleBuildable reference)
        {
            if (string.IsNullOrEmpty(reference.key))
            {
                Log.Error("Missing Key on VehicleRole " + reference.label);
            }
            this.CopyFrom(reference);
            this.upgradeSingle = reference.upgradeSingle;
        }

        public VehicleUpgradeBuildable upgradeSingle;
    }
}