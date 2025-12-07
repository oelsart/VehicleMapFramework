using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_BringBabyToSafety(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("BringBabyToSafety");
    
    private Pawn baby;
    
    private CellRect roomRect;
    
    public override void SetUp()
    {
        base.SetUp();
        var map = GroundMap;
        var c1 = new IntVec3(3, 0, 3).Reversed(map);
        var c2 = new IntVec3(7, 0, 7).Reversed(map);
        roomRect = CellRect.FromLimits(c1, c2);
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