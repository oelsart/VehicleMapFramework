using DevTools.Testing;
using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_Mine(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{

  private Thing mine;

  public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("Mine");

  public override void SetUp()
  {
    base.SetUp();
    mine = GenSpawn.Spawn(ThingDefOf.MineableSteel, FromRUCorner(GroundMap, 5), GroundMap);
    GroundMap.designationManager.AddDesignation(new Designation(mine, DesignationDefOf.Mine));
  }

  public override void TearDown()
  {
    Expect.IsTrue(mine.Destroyed);
    var list = FromRUCorner(GroundMap, 5).GetThingList(GroundMap);
    for (var i = list.Count - 1; i >= 0; i--) list[i].Destroy();
    if (!mine.Destroyed) mine.Destroy();
    mine = null;
    base.TearDown();
  }
}
