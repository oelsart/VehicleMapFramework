using RimWorld;
using UnityEngine.Assertions;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_ConstructFinishFrames(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("ConstructFinishFrames");
    
    private Frame frame;
    
    public override void SetUp()
    {
        base.SetUp();
        frame = (Frame)ThingMaker.MakeThing(ThingDefOf.Wall.frameDef, ThingDefOf.WoodLog);
        GenSpawn.Spawn(frame, FromRUCorner(GroundMap, 3), GroundMap).SetFaction(Pawn.Faction);
        var woodLog = ThingMaker.MakeThing(ThingDefOf.WoodLog);
        woodLog.stackCount = frame.ThingCountNeeded(ThingDefOf.WoodLog);
        frame.resourceContainer.TryAddOrTransfer(woodLog);
        Assert.IsTrue(frame.IsCompleted(), "Frame is not completed.");
    }

    public override void TearDown()
    {
        frame.Destroy();
        frame = null;
        base.TearDown();
    }
}