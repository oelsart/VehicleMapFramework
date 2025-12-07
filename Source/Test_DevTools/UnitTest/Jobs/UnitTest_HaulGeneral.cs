using DevTools.Testing;
using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_HaulGeneral(VehicleGroup group) : WorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral");
    
    protected virtual bool DisablePUAH => true;

    private Thing woodLog;

    private Zone_Stockpile zone;

    private PuahDisabler puahDisabler;

    public override void SetUp()
    {
        if (DisablePUAH) puahDisabler = new PuahDisabler();
        woodLog = ThingMaker.MakeThing(ThingDefOf.WoodLog);
        woodLog.stackCount = 10;
        GenSpawn.Spawn(woodLog, Pawn.Position, Pawn.Map);
        zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Pawn.Map.zoneManager);
        Pawn.Map.zoneManager.RegisterZone(zone);
        var map = Vehicle.VehicleMap;
        foreach (var cell in CellRect.FromLimits(FromRUCorner(map, 2), FromRUCorner(map, 3)).Cells)
            zone.AddCell(cell);
    }

    public override void ExecuteStep1()
    {
        base.ExecuteStep1();
        if (DisablePUAH) puahDisabler.Dispose();
        Pawn.RemoveTargetInfo();
        woodLog.Destroy();
        zone.Delete();
    }

    public override void ExecuteStep2()
    {
        if (DisablePUAH) puahDisabler = new PuahDisabler();
        woodLog = ThingMaker.MakeThing(ThingDefOf.WoodLog);
        woodLog.stackCount = 10;
        var map = Pawn.Map;
        GenSpawn.Spawn(woodLog, Pawn.Position, map);
        zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
        map.zoneManager.RegisterZone(zone);
        foreach (var cell in CellRect.FromLimits(FromRUCorner(map, 6), FromRUCorner(map, 7)).Cells)
            zone.AddCell(cell);
        results[1] = RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
        Expect.IsNotNull(results[1].job);
        zone.Delete();

        zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Vehicle.VehicleMap.zoneManager);
        Vehicle.VehicleMap.zoneManager.RegisterZone(zone);
        map.haulDestinationManager.AddHaulDestination(zone);
        zone.AddCell(new IntVec3(1, 0, 1));
        
        results[1] = RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
        Expect.IsNotNull(results[1].job);
        Expect.AreNotEqual(results[0], results[1]);
        Expect.IsTrue(results[1].job?.globalTarget.Map == Vehicle.VehicleMap);
    }

    public override void TearDown()
    {
        if (DisablePUAH) puahDisabler.Dispose();
        woodLog.Destroy();
        zone.Delete();
        Pawn.RemoveTargetInfo();
        woodLog = null;
        zone = null;
    }
}