using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class CompAdditionalGraphicsChild : ThingComp
{
  [UsedImplicitly] public ThingWithComps parentThing;

  private CompProperties_DrawAdditionalGraphics Props => (CompProperties_DrawAdditionalGraphics)props;

  public virtual IEnumerable<Graphic> Graphics => Props.graphics.Select(g => g.Graphic);

  public override void PostSpawnSetup(bool respawningAfterLoad)
  {
    if (!respawningAfterLoad)
    {
      parentThing = parent.Position.GetFirstThingWithComp<CompDrawAdditionalGraphicsOpacity>(parent.Map);
      parentThing?.GetComp<CompDrawAdditionalGraphicsOpacity>()?.children.Add(parent);
    }
  }

  public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
  {
    parentThing?.GetComp<CompDrawAdditionalGraphicsOpacity>()?.children.Remove(parent);
  }

  public override void PostExposeData()
  {
    base.PostExposeData();
    Scribe_References.Look(ref parentThing, "parentThing");
  }
}
