using RimWorld;
using Verse;

namespace VehicleMapFramework;

public static class CrossMapForbidUtility
{
  extension(IntVec3 c)
  {
    public bool IsForbidden(Pawn pawn, Thing thing)
    {
      var map = thing?.MapHeld ?? pawn.PawnTargetMap;
      if (map is null || map == pawn.Map)
        return c.IsForbidden(pawn);
      
      var pos = pawn.PositionOnAnotherMap(map);
      using var _ = new VirtualTeleporter(pawn, map, pos, setDepartMap: pawn is { DepartMap: null });
      return c.IsForbidden(pawn);
    }

    public bool IsForbidden(Pawn pawn, Map map)
    {
      if (map == pawn.Map)
        return c.IsForbidden(pawn);

      var pos = pawn.PositionOnAnotherMap(map);
      using var _ = new VirtualTeleporter(pawn, map, pos, setDepartMap: pawn is { DepartMap: null });
      return c.IsForbidden(pawn);
    }

    private bool IsForbidden(Pawn pawn)
    {
      return ForbidUtility.CaresAboutForbidden(pawn, true) &&
             (!c.InAllowedArea(pawn) || pawn.mindState.maxDistToSquadFlag > 0f &&
               !c.InHorDistOf(pawn.DutyLocation(), pawn.mindState.maxDistToSquadFlag));
    }
  }
}