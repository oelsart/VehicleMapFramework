using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobGiver_StealToMapVehicles : JobGiver_Steal
{
  private const float ItemsSearchRadiusOngoing = 12f;
  
  protected override Job TryGiveJob(Pawn pawn)
  {
    if (StealToMapVehiclesAIUtility.TryFindBestItemToSteal(pawn, ItemsSearchRadiusOngoing,
          out var thing, out var to) && !GenAI.InDangerousCombat(pawn))
    {
      var job = JobMaker.MakeJob(JobDefOf.HaulToCell);
      job.targetA = thing;
      job.targetB = to.Cell;
      job.globalTarget = to;
      job.count = Mathf.Min(thing.stackCount, (int)(pawn.GetStatValue(StatDefOf.CarryingCapacity) / thing.def.VolumePerUnit));
      return job;
    }
    return null;
  }
}