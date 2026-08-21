using DevTools.Testing;
using RimWorld;
using Verse;

namespace VehicleMapFramework.Test_Logics;

public static class TickWaiter
{
  public static void WaitUntil(Func<bool> func, int timeout = GenDate.TicksPerHour * 3)
  {
    for (var i = 0; i < timeout; i++)
    {
      if (func()) return;
      Find.TickManager.DoSingleTick();
    }
    Test.Fail($"WaitUntil timeout: {timeout} ticks");
  }

  public static void WaitUntilJobEnd(Pawn pawn)
  {
    var curJobDef = pawn.jobs.curDriver is JobDriver_GotoDestMap gotoDestMap ? gotoDestMap.nextJob?.def ?? pawn.CurJobDef : pawn.CurJobDef;
    WaitUntil(() => pawn.CurJobDef != curJobDef &&
                    (pawn.jobs.curDriver is not JobDriver_GotoDestMap gotoDestMap2 ||
                     gotoDestMap2.nextJob?.def != curJobDef));
  }
}