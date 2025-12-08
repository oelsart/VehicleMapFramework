using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_HaulMerge(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulMerge");

    private Thing woodLog1;
    
    private Thing woodLog2;
    
    private Zone_Stockpile zone;

    public override void SetUp()
    {
        base.SetUp();
        var map = GroundMap;
        zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
        map.zoneManager.RegisterZone(zone);
        foreach (var cell in CellRect.FromLimits(FromRUCorner(map, 3), FromRUCorner(map, 4)).Cells)
            zone.AddCell(cell);
        woodLog1 = GenSpawn.Spawn(ThingDefOf.WoodLog, FromRUCorner(map, 3), map);
        woodLog2 = GenSpawn.Spawn(ThingDefOf.WoodLog, FromRUCorner(map, 4), map);
    }

    public override void TearDown()
    {
        zone.Delete();
        woodLog1.Destroy();
        woodLog2.Destroy();
        zone = null;
        woodLog1 = null;
        woodLog2 = null;
        base.TearDown();
    }
}