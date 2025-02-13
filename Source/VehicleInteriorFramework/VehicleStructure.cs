using RimWorld;
using SmashTools;
using Verse;

namespace VehicleInteriors
{
    public class VehicleStructure : Building
    {
        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (this.IsOnVehicleMapOf(out var vehicle) && dinfo.Def != DamageDefOf.Bomb)
            {
                vehicle.TakeDamage(dinfo, (this.Position.ToVector3() - VehicleMapUtility.OffsetFor(vehicle, Rot8.North)).ToIntVec3().ToIntVec2);
            }
            base.PreApplyDamage(ref dinfo, out absorbed);
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (this.IsOnVehicleMapOf(out var vehicle))
            {
                vehicle.structureCellsDirty = true;
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (this.IsOnVehicleMapOf(out var vehicle))
            {
                vehicle.structureCellsDirty = true;
            }
            base.DeSpawn(mode);
        }
    }
}
