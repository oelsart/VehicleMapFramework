using RimWorld;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;

public static class CrossMapForbidUtility
{
  extension(IntVec3 c)
  {
    public bool IsForbidden(Pawn pawn, Thing thing)
    {
      using var disablePrefix = new Patch_ForbidUtility_IsForbidden.PrefixDisabler();
      var map = thing?.MapHeld ?? pawn.PawnTargetMap;
      if (map is null || map == pawn.Map)
        return c.IsForbidden(pawn);
      
      var pos = pawn.PositionOnAnotherMap(map);
      using var teleporter = new VirtualTeleporter(pawn, map, pos, setDepartMap: pawn is { DepartMap: null });
      return c.IsForbidden(pawn);
    }

    public bool IsForbidden(Pawn pawn, Map map)
    {
      using var disablePrefix = new Patch_ForbidUtility_IsForbidden.PrefixDisabler();
      if (map == pawn.Map)
        return c.IsForbidden(pawn);

      var pos = pawn.PositionOnAnotherMap(map);
      using var teleporter = new VirtualTeleporter(pawn, map, pos, setDepartMap: pawn is { DepartMap: null });
      return c.IsForbidden(pawn);
    }
  }
}