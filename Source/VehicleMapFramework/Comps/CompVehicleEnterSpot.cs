using Verse;

namespace VehicleMapFramework;

public class CompVehicleEnterSpot : ThingComp
{
    public CompProperties_VehicleEnterSpot Props => (CompProperties_VehicleEnterSpot)props;

    public virtual bool Available
    {
        get
        {
            if (!parent.IsOnVehicleMapOf(out var vehicle))
            {
                return false;
            }
            var opposite = parent.Position + parent.Rotation.Opposite.AsIntVec3;
            return vehicle.CachedOutOfBoundsCells.Contains(opposite) ||
                   vehicle.CachedExpandableCells.Contains(opposite) && vehicle.CachedImpassableCells.Contains(opposite);
        }
     }

    public virtual IntVec3 EnterVehiclePosition => CrossMapReachabilityUtility.EnterVehiclePosition(parent);

    public virtual float DistanceSquared(IntVec3 root)
    {
        return (parent.PositionOnBaseMap - root).LengthHorizontalSquared;
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            if (parent.IsOnVehicleMapOf(out var vehicle))
            {
                vehicle.EnterComps.Add(this);
            }
            CrossMapReachabilityCache.ClearCacheFor(parent.Map);
        });
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        if (map.IsVehicleMapOf(out var vehicle))
        {
            vehicle.EnterComps.Remove(this);
        }
        CrossMapReachabilityCache.ClearCacheFor(map);
    }
}
