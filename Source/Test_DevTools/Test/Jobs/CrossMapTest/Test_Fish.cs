using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_Fish(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{

  private IntVec3 cell;

  private Zone_Fishing zone;

  public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("Fish");

  public override void SetUp()
  {
    base.SetUp();
    var map = GroundMap;
    zone = new Zone_Fishing(map.zoneManager);
    map.zoneManager.RegisterZone(zone);
    cell = FromRUCorner(map, 4);
    zone.AddCell(cell);
    map.terrainGrid.SetTerrain(cell, TerrainDefOf.WaterShallow);
    cell.GetWaterBody(map).Population = 20;
  }

  public override void TearDown()
  {
    zone.Delete();
    zone = null;
    GroundMap.terrainGrid.SetTerrain(cell, TerrainDefOf.Sand);
    base.TearDown();
  }
}
