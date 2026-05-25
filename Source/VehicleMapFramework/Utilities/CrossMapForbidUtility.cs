using RimWorld;
using Verse;

namespace VehicleMapFramework;

public static class CrossMapForbidUtility
{
  extension(IntVec3 c)
  {
    public bool IsForbidden(Pawn pawn, Thing thing)
    {
      var map = thing?.MapHeld ?? pawn.TargetMapOrPawnMap;
      if (map is null || map == pawn.Map)
        return c.IsForbidden(pawn);

      var flag = pawn is { DepartMap: null };
      try
      {
        if (flag) pawn.DepartMap = pawn.Map;
        using var _ = new VirtualTeleporter(pawn, map);
        return c.IsForbidden(pawn);
      }
      finally
      {
        if (flag) pawn.DepartMap = null;
      }
    }

    public bool IsForbidden(Pawn pawn, Map map)
    {
      if (map == pawn.Map)
        return c.IsForbidden(pawn);

      var flag = pawn is { DepartMap: null };
      try
      {
        if (flag) pawn.DepartMap = pawn.Map;
        using var _ = new VirtualTeleporter(pawn, map);
        return c.IsForbidden(pawn);
      }
      finally
      {
        if (flag) pawn.DepartMap = null;
      }
    }
  }
}
