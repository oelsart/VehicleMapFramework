using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobGiver_KidnapToMapVehicles : ThinkNode_JobGiver
{
  private const float VictimSearchRadiusOngoing = 18f;
  
  protected override Job TryGiveJob(Pawn pawn)
  {
    if (KidnapToMapVehiclesAIUtility.TryFindGoodKidnapVictim(pawn, VictimSearchRadiusOngoing,
          out var victim, out var to) &&
        !GenAI.InDangerousCombat(pawn))
    {
      var job = JobMaker.MakeJob(JobDefOf.HaulToCell);
      job.targetA = victim;
      job.targetB = to.Cell;
      job.globalTarget = to;
      job.count = 1;
      return job;
    }
    return null;
  }
}