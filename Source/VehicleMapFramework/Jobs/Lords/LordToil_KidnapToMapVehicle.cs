using System.Collections.Generic;
using RimWorld;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class LordToil_KidnapToMapVehicle : LordToil_KidnapCover
{
  protected override DutyDef DutyDef => VMF_DefOf.VMF_Kidnap;

  protected virtual DutyDef DutyDefVehicle => DutyDefOf_Vehicles.VF_RangedAggressive;
  
  protected virtual DutyDef AssaultDutyDef => DutyDefOf.AssaultColony;

  public override void UpdateAllDuties()
  {
    List<Thing> list = null;
    for (var i = 0; i < lord.ownedPawns.Count; i++)
    {
      var pawn = lord.ownedPawns[i];
      if (!pawn.Spawned) continue;
      
      Thing thing = null;
      if (pawn is VehiclePawn vehicle)
      {
        vehicle.mindState.duty = new PawnDuty(DutyDefVehicle);
      }
      else
      {
        if (!cover ||
            pawn.RaceProps.Humanlike &&
            TryFindGoodOpportunisticTaskTarget(pawn, out thing, list) &&
            !GenAI.InDangerousCombat(pawn))
        {
          if (pawn.mindState.duty == null || pawn.mindState.duty.def != DutyDef)
          {
            pawn.mindState.duty = new PawnDuty(DutyDef);
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
          }
          list ??= [];
          list.Add(thing);
        }
        else
        {
          pawn.mindState.duty = new PawnDuty(AssaultDutyDef);
        }
      }
    }
  }

  protected override bool TryFindGoodOpportunisticTaskTarget(Pawn pawn, out Thing target, List<Thing> alreadyTakenTargets)
  {
    if (pawn.mindState.duty != null && pawn.mindState.duty.def == DutyDef && pawn.carryTracker.CarriedThing is Pawn)
    {
      target = pawn.carryTracker.CarriedThing;
      return true;
    }
    var result = KidnapToMapVehiclesAIUtility.TryFindGoodKidnapVictim(pawn, JobGiver_Kidnap.VictimSearchRadiusInitial,
      out var victim, out _, alreadyTakenTargets);
    target = victim;
    return result;
  }
}