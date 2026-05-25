using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class Designator_RemoveVehicleSegment : Designator_Deconstruct
{

  public Designator_RemoveVehicleSegment()
  {
    defaultLabel = "VMF_RemoveSegment".Translate();
    defaultDesc = "VMF_RemoveSegmentDesc".Translate();
    icon = ContentFinder<Texture2D>.Get("UI/Designators/RemoveBridge");
    soundSucceeded = SoundDefOf.Designate_RemoveBridge;
    hotKey = KeyBindingDefOf.Misc5;
  }

  protected override DesignationDef Designation => VMF_DefOf.VMF_RemoveSegment;

  public override AcceptanceReport CanDesignateThing(Thing t)
  {
    return t.HasComp<CompMapExpander>() && Map.designationManager.DesignationOn(t, Designation) is null;
  }

  public override void DesignateThing(Thing t)
  {
    Thing.allowDestroyNonDestroyable = true;
    base.DesignateThing(t);
    Thing.allowDestroyNonDestroyable = false;
  }
}
