using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_HaulGeneralCrossMap(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("HaulGeneral");
    
    protected virtual bool DisablePUAH => true;
    
    private Thing steel;

    private Thing shelf;
    
    private PuahDisabler puahDisabler;
    
    public override void SetUp()
    {
        base.SetUp();
        steel = GenSpawn.Spawn(ThingDefOf.Steel, Pawn.Position + IntVec3.NorthEast, VehicleMap);
        shelf = ThingMaker.MakeThing(ThingDefOf.Shelf, ThingDefOf.WoodLog);
        GenSpawn.Spawn(shelf, new IntVec3(3, 0, 3), GroundMap);
        if (DisablePUAH) puahDisabler = new PuahDisabler();
    }

    public override void TearDown()
    {
        base.TearDown();
        steel.Destroy();
        shelf.Destroy();
        steel = null;
        shelf = null;
        if (DisablePUAH) puahDisabler.Dispose();
    }
}