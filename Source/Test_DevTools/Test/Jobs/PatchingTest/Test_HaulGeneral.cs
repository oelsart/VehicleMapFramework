using DevTools.Testing;
using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

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
      parent.woodLog.stackCount = 10;
      GenSpawn.Spawn(parent.woodLog, Pawn.Position, Pawn.Map);
      parent.zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Pawn.Map.zoneManager);
      Pawn.Map.zoneManager.RegisterZone(parent.zone);
      var map = Vehicle.VehicleMap;
      foreach (var cell in CellRect.FromLimits(FromRUCorner(map, 2), FromRUCorner(map, 3)))
      {
        parent.zone.AddCell(cell);
      }
    }

    public override void RunBefore()
    {
      base.RunBefore();
      if (parent.DisablePUAH) parent.puahDisabler.Dispose();
      Pawn.RemoveTargetInfo();
      parent.woodLog.Destroy();
      parent.zone.Delete();
    }
  }

  private new class AfterPatching(Test_HaulGeneral parent) : WorkGiverTestBase.AfterPatching(parent)
  {
    public override void RunAfter()
    {
      if (parent.DisablePUAH) parent.puahDisabler = new PuahDisabler();
      parent.woodLog = ThingMaker.MakeThing(ThingDefOf.WoodLog);
      parent.woodLog.stackCount = 10;
      var map = Pawn.Map;
      GenSpawn.Spawn(parent.woodLog, Pawn.Position, map);
      parent.zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
      map.zoneManager.RegisterZone(parent.zone);
      foreach (var cell in CellRect.FromLimits(FromRUCorner(map, 6), FromRUCorner(map, 7)))
      {
        parent.zone.AddCell(cell);
      }
      Results[1] = RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
      Expect.IsNotNull(Results[1].job);
      parent.zone.Delete();

      parent.zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Vehicle.VehicleMap.zoneManager);
      Vehicle.VehicleMap.zoneManager.RegisterZone(parent.zone);
      map.haulDestinationManager.AddHaulDestination(parent.zone);
      parent.zone.AddCell(new IntVec3(1, 0, 1));

      Results[1] = RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
      Expect.IsNotNull(Results[1].job);
      Expect.AreNotEqual(Results[0], Results[1]);
      Expect.IsTrue(Results[1].job?.globalTarget.Map == Vehicle.VehicleMap);
    }

    public override void TearDown()
    {
      if (parent.DisablePUAH) parent.puahDisabler.Dispose();
      parent.woodLog.Destroy();
      parent.zone.Delete();
      Pawn.RemoveTargetInfo();
      parent.woodLog = null;
      parent.zone = null;
    }
  }
}
