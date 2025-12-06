using System.Collections.Generic;
using Verse;

namespace VehicleMapFramework;

public class CompRemoveSegmentGizmo : ThingComp
{
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield return new Designator_RemoveVehicleSegment();
    }
}