using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleMapFramework;

public class Trigger_KidnapVictimPresentCrossMap : Trigger
{
  private const int CheckInterval = 120;
  private const int MinTicksSinceDamage = 300;
  
  private TriggerData_PawnCycleInd Data => (TriggerData_PawnCycleInd)data;

  public Trigger_KidnapVictimPresentCrossMap()
  {
    data = new TriggerData_PawnCycleInd();
  }

  public override bool ActivateOn(Lord lord, TriggerSignal signal)
  {
    if (signal.type == TriggerSignalType.Tick && Find.TickManager.TicksGame % CheckInterval == 0)
    {
      if (Find.TickManager.TicksGame - lord.lastPawnHarmTick > MinTicksSinceDamage)
      {
        if (data is not TriggerData_PawnCycleInd)
          data = new TriggerData_PawnCycleInd();
        var _data = Data;
        _data.pawnCycleInd++;
        if (_data.pawnCycleInd >= lord.ownedPawns.Count)
        {
          _data.pawnCycleInd = 0;
        }
        if (lord.ownedPawns.Any())
        {
          var pawn = lord.ownedPawns[_data.pawnCycleInd];
          if (pawn.Spawned && !pawn.Downed && pawn.MentalStateDef == null &&
              KidnapToMapVehiclesAIUtility.TryFindGoodKidnapVictim(pawn, JobGiver_Kidnap.VictimSearchRadiusInitial, out _, out _) &&
              !GenAI.InDangerousCombat(pawn))
          {
            return true;
          }
        }
      }
    }
    return false;
  }
}