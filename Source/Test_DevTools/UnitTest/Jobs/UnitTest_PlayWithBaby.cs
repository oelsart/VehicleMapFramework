using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_PlayWithBaby(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("PlayWithBaby");

    private Pawn baby;

    private Thing toybox;

    public override void SetUp()
    {
        base.SetUp();
        baby = TestUtility.GenerateBaby(Pawn.Faction);
        baby.needs.BindDirectNeedFields();
        baby.needs.play.CurLevel = 0.1f;
        GenSpawn.Spawn(baby, new IntVec3(3, 0, 3), GroundMap);
        toybox = ThingMaker.MakeThing(ThingDefOf.ToyBox, ThingDefOf.WoodLog);
        GenSpawn.Spawn(toybox, new IntVec3(3, 0, 4), GroundMap);
    }

    public override void TearDown()
    {
        baby.Destroy();
        baby = null;
        toybox.Destroy();
        toybox = null;
        base.TearDown();
    }
}