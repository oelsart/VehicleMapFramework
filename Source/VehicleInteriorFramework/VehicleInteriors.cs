using Verse;

namespace VehicleInteriors
{
    public class VehicleInteriors : Mod
    {
        public VehicleInteriors(ModContentPack content) : base(content)
        {
            VehicleInteriors.mod = this;
        }

        public static VehicleInteriors mod;
    }
}
