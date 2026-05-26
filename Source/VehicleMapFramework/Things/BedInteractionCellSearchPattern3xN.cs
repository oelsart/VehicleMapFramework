using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class BedInteractionCellSearchPattern3xN : BedInteractionCellSearchPattern
{
    public override void BedCellOffsets(List<IntVec3> offsets, IntVec2 size, int slot)
    {
        if (size == IntVec2.One)
        {
            BedCellOffsets1x1(offsets);
            return;
        }
        var flag = slot == 0;
        var flag2 = slot == BedUtility.GetSleepingSlotsCount(size) - 1;
        BedCellOffsets2xN(offsets, flag, flag2);
    }
}