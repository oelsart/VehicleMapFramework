using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_HaulGeneral(VehicleGroup group) : WorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral");

    public override void SetUp()
    {
        var woodLog = ThingMaker.MakeThing(ThingDefOf.WoodLog);
        woodLog.stackCount = 10;
        GenSpawn.Spawn(woodLog, Pawn.Position, Pawn.Map);
        var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Pawn.Map.zoneManager);
        Pawn.Map.zoneManager.RegisterZone(zone);
        foreach (var cell in CellRect.FromLimits(5, 5, 7, 7).Cells)
            zone.AddCell(cell);
    }
}