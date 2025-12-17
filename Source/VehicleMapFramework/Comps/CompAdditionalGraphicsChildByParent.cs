using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleMapFramework;

public class CompAdditionalGraphicsChildByParent : CompAdditionalGraphicsChild
{
    private CompProperties_AdditionalGraphicsChildByParent Props => (CompProperties_AdditionalGraphicsChildByParent)props;

    public override IEnumerable<Graphic> Graphics =>
        Props.graphicsByParent.TryGetValue(parentThing.def, out var list)
            ? list.Select(g => g.Graphic)
            : [];
}