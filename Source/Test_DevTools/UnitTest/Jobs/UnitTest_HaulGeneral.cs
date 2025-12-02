using DevTools.Testing;
using RimWorld;
using UnityEngine.Assertions;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_HaulGeneral(VehicleGroup group) : WorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral");

    private Thing woodLog;

    private Zone_Stockpile zone;

    public override void SetUp()
    {
        woodLog = ThingMaker.MakeThing(ThingDefOf.WoodLog);
        woodLog.stackCount = 10;
        GenSpawn.Spawn(woodLog, Pawn.Position, Pawn.Map);
        zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Pawn.Map.zoneManager);
        Pawn.Map.zoneManager.RegisterZone(zone);
        foreach (var cell in CellRect.FromLimits(2, 2, 3, 3).Cells)
            zone.AddCell(cell);
    }

    public override void ExecuteStep2()
    {
        base.ExecuteStep2();
        TargetMapManager.RemoveTargetInfo(Pawn);
        zone.Delete();
        
        var vehicle = Vehicle as VehiclePawnWithMap;
        Assert.IsNotNull(vehicle);
        zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, vehicle!.VehicleMap.zoneManager);
        vehicle.VehicleMap.zoneManager.RegisterZone(zone);
        Pawn.Map.haulDestinationManager.AddHaulDestination(zone);
        zone.AddCell(new IntVec3(1, 0, 1));
        
        results[1] = RunWorkGiverAfterPatch(Pawn, Vehicle, WorkGiverDef);
        Expect.IsNotNull(results[1].job);
        Expect.AreNotEqual(results[0], results[1]);
        Expect.IsTrue(results[1].job?.globalTarget.Map == vehicle.VehicleMap);
        Pawn.Map.haulDestinationManager.RemoveHaulDestination(zone);
    }

    public override void TearDown()
    {
        woodLog.Destroy();
        zone.Delete();
    }
}