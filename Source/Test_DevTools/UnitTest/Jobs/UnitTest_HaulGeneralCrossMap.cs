using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_HaulGeneralCrossMap(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral");
    
    private Thing steel;

    private Thing shelf;
    
    public override void SetUp()
    {
        base.SetUp();
        steel = GenSpawn.Spawn(ThingDefOf.Steel, Pawn.Position + IntVec3.NorthEast, VehicleMap);
        shelf = ThingMaker.MakeThing(ThingDefOf.Shelf, ThingDefOf.WoodLog);
        GenSpawn.Spawn(shelf, new IntVec3(3, 0, 3), GroundMap);
    }

    public override void TearDown()
    {
        base.TearDown();
        steel.Destroy();
        shelf.Destroy();
        steel = null;
        shelf = null;
    }
}