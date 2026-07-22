using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class JobGiver_GetOnNearestMapVehicle : ThinkNode_JobGiver
{
  public bool allowEnemyVehicle;
  
  protected override Job TryGiveJob(Pawn pawn)
  {
    if (pawn.IsOnVehicleMapOf(out var vehicle) && (allowEnemyVehicle || vehicle.Faction == pawn.Faction))
      return null;
    
    var groundMap = pawn.GroundMap;
    var positionOnBaseMap = pawn.PositionOnBaseMap;
    IEnumerable<VehiclePawnWithMap> vehicles = VehiclePawnWithMapCache.AllVehiclesOn(groundMap);
    if (!allowEnemyVehicle)
    {
      vehicles = vehicles.Where(v => v.Faction == pawn.Faction);
    }
    vehicles = vehicles.OrderBy(v => (positionOnBaseMap - v.Position).LengthHorizontalSquared);
    foreach (var vehicle2 in vehicles)
    {
      if (CrossMapReachabilityUtility.CanReachToMap(pawn.Position, pawn.Map, TraverseParms.For(pawn, avoidPersistentDanger: true),
            vehicle2.VehicleMap,
            out var exitSpot, out var enterSpot, out var spotsQueue))
      {
        return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, spotsQueue);
      }
    }
  
    return null;
  }
}