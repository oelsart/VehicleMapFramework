using Verse;
using Waylith.Injection;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class CompAllowDangerTerrains : ThingComp
{
  public CompProperties_AllowDangerTerrains Props => (CompProperties_AllowDangerTerrains)props;

  public override void Notify_Equipped(Pawn pawn)
  {
    if (!Props.allowedDangerTerrains.NullOrEmpty())
    {
      pawn.AllowedDangerTerrains = Props.allowedDangerTerrains;
      CrossMapReachabilityCache.ClearCacheFor(pawn);
    }
  }

  public override void Notify_Unequipped(Pawn pawn)
  {
    pawn.AllowedDangerTerrains = null;
    CrossMapReachabilityCache.ClearCacheFor(pawn);
  }
}

public static class DangerTerrainHelper
{
  extension(Pawn pawn)
  {
    public extern ref List<TerrainDef> AllowedDangerTerrains { [InjectedField] get; }
  }
}