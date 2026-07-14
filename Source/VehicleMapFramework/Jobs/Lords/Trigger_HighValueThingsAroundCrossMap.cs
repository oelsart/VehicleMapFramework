using RimWorld;
using Verse;
using Verse.AI.Group;

namespace VehicleMapFramework;

public class Trigger_HighValueThingsAroundCrossMap : Trigger
{
  private const int CheckInterval = 120;
  private const int MinTicksSinceDamage = 300;
  
  public override bool ActivateOn(Lord lord, TriggerSignal signal)
  {
    if (signal.type == TriggerSignalType.Tick && Find.TickManager.TicksGame % CheckInterval == 0)
    {
      if (TutorSystem.TutorialMode)
      {
        return false;
      }
      if (Find.TickManager.TicksGame - lord.lastPawnHarmTick > MinTicksSinceDamage)
      {
        var num = StealToMapVehiclesAIUtility.TotalMarketValueAround(lord.ownedPawns);
        var num2 = StealAIUtility.StartStealingMarketValueThreshold(lord);
        return num > num2;
      }
    }
    return false;
  }
}