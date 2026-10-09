using System.Collections;
using DevTools.Testing;
using RimWorld;
using UnityEngine.Assertions;
using Vehicles.Testing;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.Test_DevTools;

internal class Test_HaulGeneral(VehicleGroup group) : WorkGiverTestBase(group)
{
  private PuahDisabler puahDisabler;

  private Thing woodLog;

  private Zone_Stockpile zone;

  public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral");

  public override Type BeforePatchingType => typeof(BeforePatching);

  public override Type AfterPatchingType => typeof(AfterPatching);

  protected virtual bool DisablePUAH => true;

  protected new class BeforePatching(Test_HaulGeneral parent) : WorkGiverTestBase.BeforePatching(parent)
  {
    public override void SetUp()
    {
      if (parent.DisablePUAH) parent.puahDisabler = new PuahDisabler();
      parent.woodLog = ThingMaker.MakeThing(ThingDefOf.WoodLog);
      var map = Pawn.Map;
      GenSpawn.Spawn(parent.woodLog, Pawn.Position + new IntVec3(3, 0, 3), map);
      parent.zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
      map.zoneManager.RegisterZone(parent.zone);
      parent.zone.AddCell(FromRUCorner(map, 9));
    }

    public override IEnumerator RunBefore()
    {
      yield return base.RunBefore();
      yield return Pawn.WaitUntilIdle();
      Expect.AreEqual(parent.woodLog.Map, Find.CurrentMap);
      Expect.AreEqual(parent.woodLog.Position, FromRUCorner(Find.CurrentMap, 9));
      if (parent.DisablePUAH) parent.puahDisabler.Dispose();
      Test_WorkGivers.ClearPawnState(Pawn);
      parent.woodLog.Destroy();
      parent.zone.Delete();
    }
  }

  protected new class AfterPatching(Test_HaulGeneral parent) : WorkGiverTestBase.AfterPatching(parent)
  {
    public override IEnumerator RunAfter()
    {
      if (parent.DisablePUAH) parent.puahDisabler = new PuahDisabler();
      parent.woodLog = ThingMaker.MakeThing(ThingDefOf.WoodLog);
      var map = Pawn.Map;
      GenSpawn.Spawn(parent.woodLog, Pawn.Position - new IntVec3(3, 0, 3), map);
      parent.zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
      map.zoneManager.RegisterZone(parent.zone);
      parent.zone.AddCell(FromRUCorner(map, 9));
      Results[1] = RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
      Assert.IsNotNull(Results[1].job);
      Pawn.jobs.StartJob(Results[1].job, JobCondition.InterruptForced);
      yield return Pawn.WaitUntilIdle();
      Assert.AreEqual(parent.woodLog.Map, Pawn.Map);
      Assert.IsTrue(parent.zone.AllContainedThings.Contains(parent.woodLog));
      
      parent.zone.Delete();
      parent.woodLog.Destroy();
      Test_WorkGivers.ClearPawnState(Pawn);
      Pawn.DeSpawn();
      GenSpawn.Spawn(Pawn, CellFinder.RandomSpawnCellForPawnNear(map.Center, map), map, Rot4.North);
      
      parent.woodLog = ThingMaker.MakeThing(ThingDefOf.WoodLog);
      GenSpawn.Spawn(parent.woodLog, Pawn.Position - new IntVec3(3, 0, 3), Pawn.Map);
      
      parent.zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Vehicle.VehicleMap.zoneManager);
      Vehicle.VehicleMap.zoneManager.RegisterZone(parent.zone);
      map.haulDestinationManager.AddHaulDestination(parent.zone);
      parent.zone.AddCell(new IntVec3(1, 0, 1));

      Results[1] = RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
      Assert.IsNotNull(Results[1].job);
      Pawn.jobs.StartJob(Results[1].job, JobCondition.InterruptForced);
      Expect.AreNotEqual(Results[0], Results[1]);
      Expect.AreEqual(Results[1].job.globalTarget.Map, Vehicle.VehicleMap, "Job target map set");
      yield return Pawn.WaitUntilIdle();
      yield return ExpectOrSuspend.AreEqual(parent.woodLog.Map, Vehicle.VehicleMap, "Wood log not hauled");
      yield return ExpectOrSuspend.AreEqual(parent.woodLog.Position, new IntVec3(1, 0, 1), "Wood log not hauled");
    }

    public override void TearDown()
    {
      if (parent.DisablePUAH) parent.puahDisabler.Dispose();
      parent.woodLog.Destroy();
      parent.zone.Delete();
      Pawn.RemoveTargetInfo();
      parent.woodLog = null;
      parent.zone = null;
      base.TearDown();
    }
  }
}
