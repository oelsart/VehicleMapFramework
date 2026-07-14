using Vehicles;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleMapFramework;

public class LordToil_ExitMapVehicle(
  LocomotionUrgency locomotion = LocomotionUrgency.None,
  bool canDig = false,
  bool interruptCurrentJob = false) : LordToil_ExitMap(locomotion, canDig, interruptCurrentJob)
{
  public override DutyDef ExitDuty => VMF_DefOf.VMF_ExitMapWithMapVehicle;

  protected virtual DutyDef ExitDutyVehicle => VMF_DefOf.VMF_ExitMapBest;

  public override void UpdateAllDuties()
  {
    var _data = Data;
    for (var i = 0; i < lord.ownedPawns.Count; i++)
    {
      var pawn = lord.ownedPawns[i];
      var dutyDef = pawn is VehiclePawn ? ExitDutyVehicle : ExitDuty;
      var pawnDuty = new PawnDuty(dutyDef)
      {
        locomotion = _data.locomotion,
        canDig = _data.canDig
      };
      pawn.mindState.duty = pawnDuty;
      if (Data.interruptCurrentJob && pawn.jobs.curJob is not null)
      {
        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
      }
    }
  }
}