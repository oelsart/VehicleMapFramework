using DevTools.Testing;
using RimWorld;
using UnityEngine.Assertions;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_ConstructFinishFrames(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{

  private Frame frame;

  public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("ConstructFinishFrames");

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
    Expect.IsTrue(frame.Destroyed);
    if (!frame.Destroyed) frame.Destroy();
    var list = FromRUCorner(GroundMap, 3).GetThingList(GroundMap);
    for (var i = list.Count - 1; i >= 0; i--) list[i].Destroy();
    frame = null;
    base.TearDown();
  }
}
