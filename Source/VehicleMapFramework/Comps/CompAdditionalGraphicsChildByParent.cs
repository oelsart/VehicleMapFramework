using System.Collections.Generic;
using Verse;

namespace VehicleMapFramework;

public class CompAdditionalGraphicsChildByParent : CompAdditionalGraphicsChild
{
  public CompProperties_AdditionalGraphicsChildByParent Props => (CompProperties_AdditionalGraphicsChildByParent)props;

  public override List<GraphicData> Graphics =>
    Props.graphicsByParent.TryGetValue(parentThing.def, out var list)
      ? list
      : [];
}
