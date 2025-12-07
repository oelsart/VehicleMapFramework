using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_Mine(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("Mine");
    
    private Thing mine;

    public override void SetUp()
    {
        base.SetUp();
        mine = GenSpawn.Spawn(ThingDefOf.MineableSteel, FromRUCorner(GroundMap, 5), GroundMap);
        GroundMap.designationManager.AddDesignation(new Designation(mine, DesignationDefOf.Mine));
    }

    public override void TearDown()
    {
        base.TearDown();
        mine.Destroy();
        mine = null;
    }
}