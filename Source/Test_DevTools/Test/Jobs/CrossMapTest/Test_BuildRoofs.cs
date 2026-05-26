using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_BuildRoofs(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{

  private IntVec3 cell;

  private Thing wall;

  public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("BuildRoofs");

  public override void SetUp()
  {
    base.SetUp();
    wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
    cell = FromRUCorner(GroundMap, 3);
    GenSpawn.Spawn(wall, cell, GroundMap);
    GroundMap.areaManager.BuildRoof[cell] = true;
  }

  public override void TearDown()
  {
    base.TearDown();
    wall.Destroy();
    wall = null;
  }
}
