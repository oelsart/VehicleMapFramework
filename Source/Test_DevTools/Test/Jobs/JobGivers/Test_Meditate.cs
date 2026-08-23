using DevTools.Testing;
using RimWorld;
using UnityEngine.Assertions;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics.JobGivers;

[LoadIfRoyaltyActive]
[TestFixture(TestType.Playing)]
internal sealed class Test_Meditate : IGenericTest
{
  public VehicleGroup Group { get; set; }

  private IGenericTest This => this;

  private JobGiver_Meditate meditate;

  [OneTimeSetUp]
  public void OneTimeSetUp()
  {
    This.SetGroup();
    MakePawnPerfect(This.Pawn);
    var timetable = This.Pawn.timetable;
    for (var i = 0; i < timetable.times.Count; i++)
    {
      This.Pawn.timetable.SetAssignment(i, TimeAssignmentDefOf.Meditate);
    }
    var faction = Find.FactionManager.AllFactions
      .FirstOrDefault(f => f.def.RoyalTitlesAwardableInSeniorityOrderForReading.Count > 0);
    Assert.IsNotNull(faction, "faction");
    This.Pawn.royalty.SetTitle(faction, RoyalTitleDefOf.Knight, false);
    Expect.GreaterThan(This.Pawn.royalty.AllTitlesInEffectForReading.Count, 0, "AllTitlesInEffectForReading");
    meditate = new JobGiver_Meditate();
  }

  [OneTimeTearDown]
  public void OneTimeTearDown()
  {
    This.DisposeGroup();
    meditate = null;
  }

  [TearDown]
  public void TearDown()
  {
    if (This.Pawn.Spawned)
      This.Pawn.DeSpawn();
    Assert.IsFalse(This.Pawn.Spawned);
    CrossMapReachabilityCache.ClearCache();
  }

  [Test]
  public void Throne()
  {
    var throne = (Building_Throne)ThingMaker.MakeThing(ThingDefOf.Throne, ThingDefOf.WoodLog);
    throne.SetFaction(This.Pawn.Faction);
    // アサインしてないThroneはなぜかPathFinderを使って到達できるか最終確認されているので、アサインしてテストしている
    Assert.IsTrue(This.Pawn.ownership.ClaimThrone(throne));
    Assert.AreEqual(This.Pawn.ownership.AssignedThrone, throne);
    var cellRect = This.Vehicle.ValidMapRect;
    using (new RoomScope(cellRect, This.VehicleMap))
    {
      Assert.IsNotNull(GenSpawn.Spawn(This.Pawn, FromRUCorner(This.Map, 3), This.Map));
      Assert.IsNotNull(GenSpawn.Spawn(throne, cellRect.CenterCell, This.VehicleMap));
      var result = meditate.TryIssueJobPackage(This.Pawn, default);
      Assert.IsNotNull(result.Job);
      var job = result.Job.ActualJob(This.Pawn);
      Expect.AreEqual(job.def, JobDefOf.Reign, "JobDef");
      Expect.AreEqual(job.targetA, throne, "targetA");
      Expect.AreEqual(job.targetC, throne, "targetC");
    }

    throne.DeSpawn(DestroyMode.WillReplace);
    This.Pawn.DeSpawn(DestroyMode.WillReplace);
    var cellRect2 = CellRect.FromLimits(FromRUCorner(This.Map, 9), FromRUCorner(This.Map, 3));
    CrossMapReachabilityCache.ClearCache();
    using (new RoomScope(cellRect2, This.Map))
    {
      Assert.IsNotNull(GenSpawn.Spawn(This.Pawn, This.VehicleMap.Center, This.VehicleMap));
      Assert.IsNotNull(GenSpawn.Spawn(throne, cellRect2.CenterCell, This.Map));
      var result = meditate.TryIssueJobPackage(This.Pawn, default);
      Assert.IsNotNull(result.Job);
      var job = result.Job.ActualJob(This.Pawn);
      Expect.AreEqual(job.def, JobDefOf.Reign, "JobDef");
      Expect.AreEqual(job.targetA, throne, "targetA");
      Expect.AreEqual(job.targetC, throne, "targetC");
      
      CrossMapReachabilityCache.ClearCache();
      This.Pawn.DeSpawn(DestroyMode.WillReplace);
      Assert.IsNotNull(GenSpawn.Spawn(This.Pawn, FromRUCorner(This.Map, 12), This.Map));
      result = meditate.TryIssueJobPackage(This.Pawn, default);
      Assert.IsNotNull(result.Job);
      job = result.Job.ActualJob(This.Pawn);
      Expect.AreEqual(job.def, JobDefOf.Reign, "JobDef");
      Expect.AreEqual(job.targetA, throne, "targetA");
      Expect.AreEqual(job.targetC, throne, "targetC");
    }
  }
  
  [Test]
  public void MeditationSpot()
  {
    var meditationSpot = ThingMaker.MakeThing(ThingDefOf.MeditationSpot);
    meditationSpot.SetFaction(This.Pawn.Faction);
    var cellRect = This.Vehicle.ValidMapRect;
    using (new RoomScope(cellRect, This.VehicleMap))
    {
      Assert.IsNotNull(GenSpawn.Spawn(This.Pawn, FromRUCorner(This.Map, 3), This.Map));
      Assert.IsNotNull(GenSpawn.Spawn(meditationSpot, cellRect.CenterCell, This.VehicleMap));
      var result = meditate.TryIssueJobPackage(This.Pawn, default);
      Assert.IsNotNull(result.Job);
      var job = result.Job.ActualJob(This.Pawn);
      Expect.AreEqual(job.def, JobDefOf.Meditate, "JobDef");
      Expect.AreNotEqual(job.targetA, LocalTargetInfo.Invalid, "targetA");
    }

    meditationSpot.DeSpawn(DestroyMode.WillReplace);
    This.Pawn.DeSpawn(DestroyMode.WillReplace);
    var cellRect2 = CellRect.FromLimits(FromRUCorner(This.Map, 9), FromRUCorner(This.Map, 3));
    CrossMapReachabilityCache.ClearCache();
    using (new RoomScope(cellRect2, This.Map))
    {
      Assert.IsNotNull(GenSpawn.Spawn(This.Pawn, This.VehicleMap.Center, This.VehicleMap));
      Assert.IsNotNull(GenSpawn.Spawn(meditationSpot, cellRect2.CenterCell, This.Map));
      var result = meditate.TryIssueJobPackage(This.Pawn, default);
      Assert.IsNotNull(result.Job);
      var job = result.Job.ActualJob(This.Pawn);
      Expect.AreEqual(job.def, JobDefOf.Meditate, "JobDef");
      Expect.AreNotEqual(job.targetA, LocalTargetInfo.Invalid, "targetA");
      
      CrossMapReachabilityCache.ClearCache();
      This.Pawn.DeSpawn(DestroyMode.WillReplace);
      Assert.IsNotNull(GenSpawn.Spawn(This.Pawn, FromRUCorner(This.Map, 12), This.Map));
      result = meditate.TryIssueJobPackage(This.Pawn, default);
      Assert.IsNotNull(result.Job);
      job = result.Job.ActualJob(This.Pawn);
      Expect.AreEqual(job.def, JobDefOf.Meditate, "JobDef");
      Expect.AreNotEqual(job.targetA, LocalTargetInfo.Invalid, "targetA");
    }
  }

  private struct RoomScope : IDisposable
  {
    private List<Thing> things;
    public Room Room { get; private set; }

    [Obsolete("The constructor with no arguments is prohibited.", error: true)]
    public RoomScope()
    {
    }
    
    public RoomScope(CellRect cellRect, Map map)
    {
      things = SimplePool<List<Thing>>.Get();
      things.Clear();

      var faction = Faction.OfPlayer;
      var doorCell = cellRect.GetCenterCellOnEdge(Rot4.South);
      var door = ThingMaker.MakeThing(ThingDefOf.Door, ThingDefOf.WoodLog);
      door.SetFactionDirect(faction);
      Assert.IsNotNull(GenSpawn.Spawn(door, doorCell, map), "door");
      things.Add(door);
      foreach (var c in cellRect.EdgeCells)
      {
        if (c == doorCell) continue;
        var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
        wall.SetFactionDirect(faction);
        Assert.IsNotNull(GenSpawn.Spawn(wall, c, map), $"wall({c})");
        things.Add(wall);
      }

      foreach (var c in cellRect)
      {
        map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
      }

      map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
      Room = cellRect.CenterCell.GetRoom(map);
      Assert.IsNotNull(Room, "Room");
      Room.Temperature = 21f;
    }

    public void Dispose()
    {
      var room = Room;
      var map = room.Map;
      var center = room.ExtentsClose.CenterCell;
      foreach (var c in room.ExtentsClose.ExpandedBy(1))
      {
        map.roofGrid.SetRoof(c, null);
      }
      
      foreach (var thing in things)
      {
        if (thing is { Destroyed: false })
          thing.Destroy();
      }
      things.Clear();
      things = null;
      Room = null;
      map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
      Assert.AreNotEqual(center.GetRoom(map), room);
    }
  }
}