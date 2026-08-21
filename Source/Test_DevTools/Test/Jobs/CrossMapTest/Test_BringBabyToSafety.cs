using DevTools.Testing;
using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_BringBabyToSafety(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
  private Pawn baby;
  private CellRect roomRect;

  public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("BringBabyToSafety");

  public override void SetUp()
  {
    base.SetUp();
    var map = GroundMap;
    roomRect = CellRect.FromLimits(FromRUCorner(map, 3), FromRUCorner(map, 7));
    baby = GenerateBaby(Pawn.Faction);
    GenSpawn.Spawn(baby, FromRUCorner(map, 5), map);
    baby.mindState.SetAutofeeder(Pawn, AutofeedMode.Childcare);

    foreach (var cell in roomRect.EdgeCells)
    {
      var def = cell == new IntVec3(7, 0, 5).Reversed(map) ? ThingDefOf.Wall : ThingDefOf.Door;
      var edifice = ThingMaker.MakeThing(def, ThingDefOf.WoodLog);
      GenSpawn.Spawn(edifice, cell, GroundMap);
    }
    GroundMap.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

    baby.GetRoom().Temperature = 50f;
  }

  public override void TearDown()
  {
    Expect.IsTrue(baby.Spawned);
    Expect.IsFalse(roomRect.Contains(baby.Position));
    baby.Destroy();
    baby = null;
    foreach (var cell in roomRect.EdgeCells)
    {
      cell.GetEdifice(GroundMap)?.Destroy();
    }
    GroundMap.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
    base.TearDown();
  }
}
