using System.Collections.Generic;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class CompAdditionalGraphicsChild : ThingComp
{
  [UsedImplicitly] public ThingWithComps parentThing;

  private CompProperties_DrawAdditionalGraphics Props => (CompProperties_DrawAdditionalGraphics)props;

  public virtual List<GraphicData> Graphics => Props.graphics;

  public override void PostSpawnSetup(bool respawningAfterLoad)
  {
    parentThing = parent.Position.GetFirstThingWithComp<CompDrawAdditionalGraphicsOpacity>(parent.Map);
    parentThing?.GetComp<CompDrawAdditionalGraphicsOpacity>()?.children.Add(this);
  }

  public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
  {
    parentThing?.GetComp<CompDrawAdditionalGraphicsOpacity>()?.children.Remove(this);
  }
}
