using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class WorkGiver_BuildRoofs(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("BuildRoofs");

    private Thing wall;
    
    private IntVec3 cell;
    
    public override void SetUp()
    {
        base.SetUp();
        wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
        cell = FromRUCorner(GroundMap, 3);
        GenSpawn.Spawn(wall, cell, GroundMap);
        GroundMap.areaManager.BuildRoof[cell] = true;
    }

    public override void TearDown()
    {
        base.TearDown();
        wall.Destroy();
        wall = null;
    }
}