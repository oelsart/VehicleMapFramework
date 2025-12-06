using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_BringBabyToSafety(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("BringBabyToSafety");
    
    private Pawn baby;
    
    private readonly CellRect roomRect = CellRect.FromLimits(3, 3, 7, 7);
    
    public override void SetUp()
    {
        base.SetUp();
        baby = TestUtility.GenerateBaby(Pawn.Faction);
        GenSpawn.Spawn(baby, new IntVec3(5, 0, 5), GroundMap);
        baby.mindState.SetAutofeeder(Pawn, AutofeedMode.Childcare);

        foreach (var cell in roomRect.EdgeCells)
        {
            var def = cell == new IntVec3(7, 0, 5) ? ThingDefOf.Wall : ThingDefOf.Door;
            var edifice = ThingMaker.MakeThing(def, ThingDefOf.WoodLog);
            GenSpawn.Spawn(edifice, cell, GroundMap);
        }
        GroundMap.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();

        baby.GetRoom().Temperature = 50f;
    }

    public override void TearDown()
    {
        baby.Destroy();
        baby = null;
        foreach (var cell in roomRect.EdgeCells)
        {
            cell.GetEdifice(GroundMap);
        }
        GroundMap.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
        base.TearDown();
    }
}