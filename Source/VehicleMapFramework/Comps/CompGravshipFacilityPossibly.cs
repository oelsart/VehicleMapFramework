using RimWorld;

namespace VehicleMapFramework;

public class CompGravshipFacilityPossibly : CompGravshipFacility
{
  public override string CompInspectStringExtra()
  {
    return GravshipUtility.GetPlayerGravEngine_NewTemp(parent.Map) is null ? null : base.CompInspectStringExtra();
  }
}
