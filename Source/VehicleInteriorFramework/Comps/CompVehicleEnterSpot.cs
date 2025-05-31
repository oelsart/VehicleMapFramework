using Verse;

namespace VehicleInteriors
{
    public class CompVehicleEnterSpot : ThingComp
    {
        public CompProperties_VehicleEnterSpot Props => (CompProperties_VehicleEnterSpot)this.props;

        public virtual float DistanceSquared(IntVec3 root)
        {
            return (this.parent.PositionOnBaseMap() - root).LengthHorizontalSquared;
        }
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {
                vehicle.EnterComps.Add(this);
            }
        }

        public override void PostDeSpawn(Map map)
        {
            base.PostDeSpawn(map);
            if (map.IsVehicleMapOf(out var vehicle))
            {
                vehicle.EnterComps.Remove(this);
            }
        }
    }
}
