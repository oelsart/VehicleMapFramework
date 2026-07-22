using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobGiver_ExitWithMapVehicle : ThinkNode_JobGiver
{
  private const int VehicleWaitForPawnTicks = 300;

  protected override Job TryGiveJob(Pawn pawn)
  {
    if (pawn.IsOnVehicleMapOf(out var vehicle) && vehicle.Faction == pawn.Faction)
      return null;

    var groundMap = pawn.GroundMap;
    var positionOnBaseMap = pawn.PositionOnBaseMap;
    var vehicles = VehiclePawnWithMapCache.AllVehiclesOn(groundMap)
      .Where(v => v.Faction == pawn.Faction)
      .OrderBy(v => (positionOnBaseMap - v.Position).LengthHorizontalSquared);
    foreach (var vehicle2 in vehicles)
    {
      if (CrossMapReachabilityUtility.CanReachToMap(pawn.Position, pawn.Map, TraverseParms.For(pawn),
            vehicle2.VehicleMap,
            out var exitSpot, out var enterSpot, out var spotsQueue))
      {
        if (vehicle2.CurJobDef != JobDefOf.Wait_MaintainPosture)
          PawnUtility.ForceWait(vehicle2, VehicleWaitForPawnTicks, maintainPosture: true);
        return JobAcrossMapsUtility.GotoDestMapJob(pawn, exitSpot, enterSpot, spotsQueue);
      }
    }

    var exitSpot2 = TargetInfo.Invalid;
    var enterSpot2 = TargetInfo.Invalid;
    List<TraverseSpots> spotsQueue2 = null;
    if (TryFindBestExitSpot(out var cell))
    {
      var job = JobMaker.MakeJob(VMF_DefOf.VMF_GotoAcrossMaps, cell)
        .SetSpotsToJobAcrossMaps(pawn, exitSpot2, enterSpot2, spotsQueue2);
      job.exitMapOnArrival = true;
      return job;
    }

    return null;

    bool TryFindBestExitSpot(out IntVec3 spot)
    {
      var num = 0;
      for (var i = 0; i < 30; i++)
      {
        var flag = CellFinder.TryFindRandomCellNear(positionOnBaseMap, groundMap, num, null, out var result);
        num += 4;
        if (flag)
        {
          var num2 = result.x;
          var intVec = new IntVec3(0, 0, result.z);
          if (groundMap.Size.z - result.z < num2)
          {
            num2 = groundMap.Size.z - result.z;
            intVec = new IntVec3(result.x, 0, groundMap.Size.z - 1);
          }
          if (groundMap.Size.x - result.x < num2)
          {
            num2 = groundMap.Size.x - result.x;
            intVec = new IntVec3(groundMap.Size.x - 1, 0, result.z);
          }
          if (result.z < num2)
          {
            intVec = new IntVec3(result.x, 0, 0);
          }
          
          
          if (intVec.Standable(groundMap) &&
              CrossMapReachabilityUtility.CanReach(pawn.Map, pawn.Position, intVec, PathEndMode.OnCell,
                TraverseParms.For(pawn, avoidPersistentDanger: false), groundMap,
                out exitSpot2, out enterSpot2, out spotsQueue2))
          {
            spot = intVec;
            return true;
          }
        }
      }
      spot = pawn.Position;
      return false;
    }
  }
}