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
        zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, GroundMap.zoneManager);
        GroundMap.zoneManager.RegisterZone(zone);
        foreach (var cell in CellRect.FromLimits(3, 3, 4, 4).Cells)
            zone.AddCell(cell);
        woodLog1 = GenSpawn.Spawn(ThingDefOf.WoodLog, new IntVec3(3, 0, 3), GroundMap);
        woodLog2 = GenSpawn.Spawn(ThingDefOf.WoodLog, new IntVec3(4, 0, 4), GroundMap);
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