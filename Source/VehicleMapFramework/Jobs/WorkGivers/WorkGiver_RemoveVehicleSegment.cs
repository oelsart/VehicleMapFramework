using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class WorkGiver_RemoveVehicleSegment : WorkGiver_RemoveBuilding
{
  protected override DesignationDef Designation => VMF_DefOf.VMF_RemoveSegment;

  protected override JobDef RemoveBuildingJob => VMF_DefOf.VMF_DeconstructVehicleSegment;

  public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
  {
    return t.TryGetComp<CompMapExpander>(out var comp) && !comp.IsOnlyBridge && base.HasJobOnThing(pawn, t, forced);
  }
}
