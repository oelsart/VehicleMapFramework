using SmashTools;
using Verse;

namespace VehicleInteriors
{
    public class CompVehicleStructure : ThingComp
    {
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {
                vehicle.TakeDamage(dinfo, (this.parent.Position.ToVector3() - VehicleMapUtility.OffsetFor(vehicle, Rot8.North)).ToIntVec3().ToIntVec2);
            }
            absorbed = true;
        }
    }
}
