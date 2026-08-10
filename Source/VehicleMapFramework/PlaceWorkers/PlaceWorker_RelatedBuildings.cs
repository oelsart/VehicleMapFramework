using Verse;

namespace VehicleMapFramework;

public class PlaceWorker_RelatedBuildings : PlaceWorker
{
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
    Thing thingToIgnore = null,
    Thing thing = null)
  {
    if (checkingDef is ThingDef thingDef &&
        loc.GetThingList(map).Any(t => t.def.building?.relatedBuildCommands?.Contains(thingDef) ?? false))
    {
      return true;
    }

    return "VMF_ForceOnRelatedBuilding".Translate();
  }
}