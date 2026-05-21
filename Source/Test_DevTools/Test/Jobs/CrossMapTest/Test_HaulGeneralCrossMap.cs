using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_HaulGeneralCrossMap(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
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
        GenSpawn.Spawn(shelf, FromRUCorner(GroundMap, 3), GroundMap);
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