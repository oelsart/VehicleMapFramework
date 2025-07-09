using Verse;

namespace VehicleMapFramework;

public class CompVehicleEnterSpot : ThingComp
{
    public CompProperties_VehicleEnterSpot Props => (CompProperties_VehicleEnterSpot)props;

    public virtual float DistanceSquared(IntVec3 root)
    {
        return (parent.PositionOnBaseMap() - root).LengthHorizontalSquared;
    }
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        if (parent.IsOnVehicleMapOf(out var vehicle))
        {
            vehicle.EnterComps.Add(this);
        }
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map);
        if (map.IsVehicleMapOf(out var vehicle))
        {
            vehicle.EnterComps.Remove(this);
        }
    }
}
