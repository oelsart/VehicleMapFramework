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
        foreach (var cell in CellRect.FromLimits(2, 2, 3, 3).Cells)
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
        GenSpawn.Spawn(woodLog, Pawn.Position, Pawn.Map);
        zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Pawn.Map.zoneManager);
        Pawn.Map.zoneManager.RegisterZone(zone);
        foreach (var cell in CellRect.FromLimits(2, 2, 3, 3).Cells)
            zone.AddCell(cell);
        results[1] = RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
        Expect.IsNotNull(results[1].job);
        zone.Delete();

        var vehicle = (VehiclePawnWithMap)Vehicle;
        zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, vehicle!.VehicleMap.zoneManager);
        vehicle.VehicleMap.zoneManager.RegisterZone(zone);
        Pawn.Map.haulDestinationManager.AddHaulDestination(zone);
        zone.AddCell(new IntVec3(1, 0, 1));
        
        results[1] = RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
        Expect.IsNotNull(results[1].job);
        Expect.AreNotEqual(results[0], results[1]);
        Expect.IsTrue(results[1].job?.globalTarget.Map == vehicle.VehicleMap);
    }

    public override void TearDown()
    {
        if (DisablePUAH) puahDisabler.Dispose();
        woodLog.Destroy();
        zone.Delete();
        Pawn.RemoveTargetInfo();
    }
}