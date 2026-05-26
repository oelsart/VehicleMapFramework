using System.Collections.Generic;
using JetBrains.Annotations;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class CompProperties_AdditionalGraphicsChildByParent : CompProperties
{
  public Dictionary<ThingDef, List<GraphicData>> graphicsByParent;

  public CompProperties_AdditionalGraphicsChildByParent()
  {
    compClass = typeof(CompAdditionalGraphicsChildByParent);
  }
}
