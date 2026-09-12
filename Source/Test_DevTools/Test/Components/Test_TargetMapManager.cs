using DevTools.Testing;
using RimWorld;
using Verse;

namespace VehicleMapFramework.Test_Logics;

[TestFixture(TestType.Playing)]
internal sealed class Test_TargetMapManager
{
  private static Map CurrentMap => Find.CurrentMap;

  private static TargetMapManager Manager => TargetMapUtility.manager;

  [OneTimeTearDown]
  public void OneTimeTearDown()
  {
    SaveTester.Clear();
  }
  
  [TearDown]
  public void TearDown()
  {
    Manager.TargetInfoTable.Clear();
  }

  [Test]
  public void ValidTargetInfoNotRemoved()
  {
    var thing = ThingMaker.MakeThing(ThingDefOf.WoodLog);
    try
    {
      thing.TargetInfo = new TargetInfo(IntVec3.Zero, CurrentMap);

      // 10800 tick の倍数に合わせて Tick 実行
      var tick = (GenTicks.TicksGame / 10800 + 1) * 10800;
      using (new MockGameTicks(tick))
      {
        Manager.WorldComponentTick();
      }
      
      Expect.IsTrue(Manager.TargetInfoTable.TryGetValue(thing, out var box), "Valid entry should not be removed.");
      Expect.IsTrue(box.Value.IsValid, "TargetInfo should remain valid.");
      Expect.AreEqual(thing.TargetInfo, new TargetInfo(IntVec3.Zero, CurrentMap), "TargetInfo should not be changed.");
    }
    finally
    {
      thing.Destroy();
    }
  }

  [Test]
  public void InvalidTargetInfoRemoved()
  {
    var thing = ThingMaker.MakeThing(ThingDefOf.WoodLog);
    try
    {
      thing.TargetInfo = TargetInfo.Invalid;
      Expect.IsTrue(Manager.TargetInfoTable.TryGetValue(thing, out _), "Invalid entry was registered.");

      var tick = (GenTicks.TicksGame / 10800 + 1) * 10800;
      using (new MockGameTicks(tick))
      {
        Manager.WorldComponentTick();
      }

      Expect.IsFalse(Manager.TargetInfoTable.TryGetValue(thing, out _), "Invalid entry should be removed on interval tick.");
      Expect.AreEqual(thing.TargetInfo, TargetInfo.Invalid, "TargetInfo should remain invalid after cleanup.");
      Expect.IsNull(thing.TargetMap, "TargetMap should be null after cleanup.");
    }
    finally
    {
      thing.Destroy();
    }
  }

  [Test]
  public void NonIntervalTicksDoNotCleanup()
  {
    var thing = ThingMaker.MakeThing(ThingDefOf.WoodLog);
    try
    {
      thing.TargetInfo = TargetInfo.Invalid;

      // 周期ではない tick
      var tick = (GenTicks.TicksGame / 10800 + 1) * 10800 + 1;
      using (new MockGameTicks(tick))
      {
        Manager.WorldComponentTick();
      }

      Expect.IsTrue(Manager.TargetInfoTable.TryGetValue(thing, out _), "Cleanup should not occur outside interval.");
    }
    finally
    {
      thing.Destroy();
    }
  }

  [Test]
  public void GetOrCreate_NullKeyDoesNotThrow()
  {
    Expect.IsNull(Manager.GetOrCreateTargetInfo(null), "GetOrCreateTargetInfo(null) should return null without throwing.");
  }

  [Test]
  public void WithGCCollectedKeysDoesNotThrow()
  {
    // スコープ内でキーを生成し、参照を切って GC を走らせる
    new Action(() =>
    {
      var deadThing = ThingMaker.MakeThing(ThingDefOf.WoodLog);
      deadThing.TargetInfo = new TargetInfo(IntVec3.Zero, CurrentMap);
    })();

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    // GC により KeyValuePair の Key が null になり得る状態で Tick を実行
    var tick = (GenTicks.TicksGame / 10800 + 1) * 10800;
    using (new MockGameTicks(tick))
    {
      try
      {
        Manager.WorldComponentTick();
      }
      catch (Exception ex)
      {
        Test.Fail($"WorldComponentTick should not throw ArgumentNullException even if GC-collected keys exist.\n{ex}");
      }
    }
  }

  [Test]
  public void TargetInfoTableNotNullAfterInit()
  {
    Expect.IsNotNull(Manager.TargetInfoTable, "TargetInfoTable must not be null.");
  }

  [Test]
  public void ValidTargetInfoRestored()
  {
    var thing = ThingMaker.MakeThing(ThingDefOf.WoodLog);

    var expectedTarget = new TargetInfo(CurrentMap.Center + IntVec3.North, CurrentMap);
    thing.TargetInfo = expectedTarget;

    var container = new SaveTester.Container(Manager, thing);
    SaveTester.Save(container);
    Manager.TargetInfoTable.Clear();
    using (new SaveTester.MockLoaded(CurrentMap, thing))
    {
      SaveTester.Load(container);
    }

    Expect.IsNotNull(Manager.TargetInfoTable, "Loaded TargetInfoTable must not be null.");
    
    Expect.IsTrue(thing.TryGetTargetInfo(out var loadedTarget), "TargetInfo should be loaded for the spawned thing.");
    Expect.AreEqual(expectedTarget, loadedTarget, "Loaded TargetInfo must match saved TargetInfo.");

    thing.Destroy();
  }

  [Test]
  public void InvalidOrNullMapEntriesExcluded()
  {
    var thingValid = ThingMaker.MakeThing(ThingDefOf.WoodLog);
    var thingInvalidMap = ThingMaker.MakeThing(ThingDefOf.WoodLog);
    var thingInvalidTarget = ThingMaker.MakeThing(ThingDefOf.WoodLog);
    thingValid.TargetInfo = new TargetInfo(CurrentMap.Center, CurrentMap);
    thingInvalidMap.TargetInfo = new TargetInfo(CurrentMap.Center, null, true); // Map is null
    thingInvalidTarget.TargetInfo = TargetInfo.Invalid;                  // IsValid is false

    var container = new SaveTester.Container(Manager, thingValid, thingInvalidMap, thingInvalidTarget);
    SaveTester.Save(container);
    Manager.TargetInfoTable.Clear();
    using (new SaveTester.MockLoaded(CurrentMap, thingValid, thingInvalidMap, thingInvalidTarget))
    {
      SaveTester.Load(container);
    }
    
    Expect.IsTrue(thingValid.TryGetTargetInfo(out _), "Valid entry should be saved and loaded.");
    Expect.IsFalse(thingInvalidMap.TryGetTargetInfo(out _), "Entry with null Map must be filtered out during save.");
    Expect.IsFalse(thingInvalidTarget.TryGetTargetInfo(out _), "Invalid entry must be filtered out during save.");

    thingValid.Destroy();
    thingInvalidMap.Destroy();
    thingInvalidTarget.Destroy();
  }
}