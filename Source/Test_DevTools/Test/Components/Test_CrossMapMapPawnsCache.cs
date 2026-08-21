using DevTools.Testing;
using RimWorld;
using VehicleMapFramework;
using VehicleMapFramework.Test_Logics;
using Vehicles.Testing;
using Verse;

namespace Test_DevTools.Test.Components;

[TestFixture(TestType.Playing)]
internal sealed class Test_CrossMapMapPawnsCache : IGenericTest
{
  public VehicleGroup Group { get; set; }

  private IGenericTest Test => this;
  
  [OneTimeSetUp]
  public void OneTimeSetUp()
  {
    Group = VehicleGroup.CreateBasicVehicleGroup(new VehicleGroup.MockSettings()
    {
      vehicleDef = Crawler, passengers = 2
    });
    TestUtils.ForceSpawn(Test.Vehicle);
    Test.Vehicle.DoTick();
  }

  [OneTimeTearDown]
  public void OneTimeTearDown()
  {
    Group.Dispose();
  }

  [SetUp]
  public void Setup()
  {
    CrossMapMapPawnsCache.ClearAll();
  }

  [TearDown]
  public void TearDown()
  {
    foreach (var pawn in Group.pawns)
    {
      pawn.DeSpawn();
      if (pawn.Faction != Faction.OfPlayer)
        pawn.SetFaction(Faction.OfPlayer);
      pawn.guest.Released = true;
    }
  }

  [Test]
  public void Test_AllPawns()
  {
    Expect.AreEqual(1, Test.Map.mapPawns.AllPawnsCount, "Before spawning pawn[0]");
    GenSpawn.Spawn(Group.pawns[0], CellFinder.RandomSpawnCellForPawnNear(Test.Map.Center, Test.Map), Test.Map);
    Expect.AreEqual(2, Test.Map.mapPawns.AllPawnsCount, "After spawning pawn[0]");
    GenSpawn.Spawn(Group.pawns[1], CellFinder.RandomSpawnCellForPawnNear(Test.VehicleMap.Center, Test.VehicleMap), Test.VehicleMap);
    Expect.AreEqual(3, Test.Map.mapPawns.AllPawnsCount, "After spawning pawn[1]");
    Expect.AreEqual(1, Test.VehicleMap.mapPawns.AllPawnsCount, "After spawning pawn[1] on vehicle map");
  }

  [Test]
  public void Test_AllPawnsSpawned()
  {
    Expect.AreEqual(1, Test.Map.mapPawns.AllPawnsSpawned.Count, "Before spawning pawn[0]");
    GenSpawn.Spawn(Group.pawns[0], CellFinder.RandomSpawnCellForPawnNear(Test.Map.Center, Test.Map), Test.Map);
    Expect.AreEqual(2, Test.Map.mapPawns.AllPawnsSpawned.Count, "After spawning pawn[0]");
    GenSpawn.Spawn(Group.pawns[1], CellFinder.RandomSpawnCellForPawnNear(Test.VehicleMap.Center, Test.VehicleMap), Test.VehicleMap);
    Expect.AreEqual(3, Test.Map.mapPawns.AllPawnsSpawned.Count, "After spawning pawn[1]");
    Expect.AreEqual(1, Test.VehicleMap.mapPawns.AllPawnsSpawned.Count, "After spawning pawn[1] on vehicle map");
  }

  [Test]
  public void Test_FreeHumanlikesSpawnedOfFaction()
  {
    Expect.AreEqual(0, Test.Map.mapPawns.FreeColonistsCount, "Before spawning pawn[0]");
    GenSpawn.Spawn(Group.pawns[0], CellFinder.RandomSpawnCellForPawnNear(Test.Map.Center, Test.Map), Test.Map);
    Expect.AreEqual(1, Test.Map.mapPawns.FreeColonistsCount, "After spawning pawn[0]");
    GenSpawn.Spawn(Group.pawns[1], CellFinder.RandomSpawnCellForPawnNear(Test.VehicleMap.Center, Test.VehicleMap), Test.VehicleMap);
    Expect.AreEqual(2, Test.Map.mapPawns.FreeColonistsCount, "After spawning pawn[1]");
    Expect.AreEqual(1, Test.VehicleMap.mapPawns.FreeColonistsCount, "After spawning pawn[1] on vehicle map");
    Group.pawns[1].SetFaction(Faction.OfPirates);
    Expect.AreEqual(1, Test.Map.mapPawns.FreeColonistsCount, "After changing pawn[1] to pirate faction");
  }

  [Test]
  public void Test_PrisonersOfColonySpawned()
  {
    Expect.AreEqual(0, Test.Map.mapPawns.PrisonersOfColonySpawnedCount, "Before spawning pawn[0]");
    GenSpawn.Spawn(Group.pawns[0], CellFinder.RandomSpawnCellForPawnNear(Test.Map.Center, Test.Map), Test.Map);
    Expect.AreEqual(0, Test.Map.mapPawns.PrisonersOfColonySpawnedCount, "After spawning pawn[0]");
    Group.pawns[0].guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
    Expect.AreEqual(1, Test.Map.mapPawns.PrisonersOfColonySpawnedCount, "After setting pawn[0] as prisoner");
    GenSpawn.Spawn(Group.pawns[1], CellFinder.RandomSpawnCellForPawnNear(Test.VehicleMap.Center, Test.VehicleMap), Test.VehicleMap);
    Expect.AreEqual(1, Test.Map.mapPawns.PrisonersOfColonySpawnedCount, "After spawning pawn[1]");
    Group.pawns[1].guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
    Expect.AreEqual(2, Test.Map.mapPawns.PrisonersOfColonySpawnedCount, "After setting pawn[1] as prisoner");
    Expect.AreEqual(1, Test.VehicleMap.mapPawns.PrisonersOfColonySpawnedCount, "After spawning pawn[1] on vehicle map");
  }
}

[TestFixture(TestType.PostGameExit)]
public class Test_CrossMapMapPawnsCacheEnsureClear
{
  [Test]
  public void Test_CacheCleared()
  {
    Expect.All(CrossMapMapPawnsCache.AllInstances, cache => cache.CacheCount == 0);
  }
}