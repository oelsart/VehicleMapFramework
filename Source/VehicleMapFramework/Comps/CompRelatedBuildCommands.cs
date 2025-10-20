using System.Collections.Generic;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class CompRelatedBuildCommands : ThingComp
{
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        return BuildRelatedCommandUtility.RelatedBuildCommands(parent.def);
    }
}