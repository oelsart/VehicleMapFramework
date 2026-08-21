using Verse;

namespace VehicleMapFramework;

public abstract class CompVehicleEnterSpot : ThingComp
{
  public CompProperties_VehicleEnterSpot Props => (CompProperties_VehicleEnterSpot)props;

  protected abstract bool Available { get; }
  
  public abstract bool ShouldOffsetOnEdge { get; }

  protected abstract TargetInfo AccessSpot { get; }

  public TargetInfo AvailableAccessSpot => Available ? AccessSpot : TargetInfo.Invalid;

  public abstract float MovePerTick(Pawn pawn);

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

  public enum Kind
  {
    RampOnly,
    GroundAccessOnly,
    DirectAccessOnly,
    All
  }
}
