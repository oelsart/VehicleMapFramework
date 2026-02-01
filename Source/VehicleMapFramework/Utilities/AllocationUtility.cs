using System;
using Verse;

namespace VehicleMapFramework;

public static class AllocationUtility
{
    public static int AdjacentCellsInsideCardinalNonAlloc(this IntVec3 c, Map map, Span<IntVec3> result)
    {
        var count = 0;
        if (c.InBounds(map)) result[count++] = c;
        if (new IntVec3(c.x, c.y, c.z + 1).InBounds(map)) result[count++] = new IntVec3(c.x, c.y, c.z + 1);
        if (new IntVec3(c.x + 1, c.y, c.z).InBounds(map)) result[count++] = new IntVec3(c.x + 1, c.y, c.z);
        if (new IntVec3(c.x, c.y, c.z - 1).InBounds(map)) result[count++] = new IntVec3(c.x, c.y, c.z - 1);
        if (new IntVec3(c.x - 1, c.y, c.z).InBounds(map)) result[count++] = new IntVec3(c.x - 1, c.y, c.z);
    
        return count;
    }
}