using RimWorld;
using Verse;

namespace VehicleMapFramework;

public static class CrossMapForbidUtility
{
    extension(IntVec3 c)
    {
        public bool IsForbidden(Pawn pawn, Thing thing)
        {
            var map = thing?.MapHeld;
            if (map is null || map == pawn.Map)
                return c.IsForbidden(pawn);
        
            using var _ = new VirtualTeleporter(pawn, map);
            return c.IsForbidden(pawn);
        }

        public bool IsForbidden(Pawn pawn, Map map)
        {
            if (map == pawn.Map)
                return c.IsForbidden(pawn);
            using var _ = new VirtualTeleporter(pawn, map);
            return c.IsForbidden(pawn);
        }
    }
}