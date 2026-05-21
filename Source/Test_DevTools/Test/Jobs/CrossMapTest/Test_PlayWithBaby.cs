using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_PlayWithBaby(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
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
        var map = GroundMap;
        GenSpawn.Spawn(baby, FromRUCorner(map, 3), map);
        toybox = ThingMaker.MakeThing(ThingDefOf.ToyBox, ThingDefOf.WoodLog);
        GenSpawn.Spawn(toybox, FromRUCorner(map, 4), map);
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