using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class PlaceWorker_UniqueVehicle : PlaceWorker
{
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
    Thing thingToIgnore = null,
    Thing thing = null)
  {
    if (checkingDef is not VehicleBuildDef vehicleBuildDef) return true;

    var vehicleDef = vehicleBuildDef.thingToSpawn;
    if (!UniqueVehicleUtility.AllowGenerate(vehicleDef))
    {
      return "VMF_UniqueVehicleExceedsLimit".Translate(vehicleDef.label);
    }

    return true;
  }
}