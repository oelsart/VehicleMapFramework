using System.Collections;
using DevTools.Testing;
using JetBrains.Annotations;
using RimWorld;
using SmashTools;
using UnityEngine.Assertions;
using Vehicles;
using Vehicles.Testing;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.Test_Logics;

[TestFixture(TestType.Playing)]
internal sealed class Test_CrossMapReachability(
  [ParametersSource("TraverseParmsSource")] NamedLazy<TraverseParms> traverseParms)
{
  public VehicleGroup Group { get; set; }

  public VehiclePawnWithMap[] Crawlers { get; set; }

  private VehiclePawnWithMap[] Pantodons { get; set; }

  private static Map Map => Find.CurrentMap;

  private ThreadDisabler threadDisabler;

  [UsedImplicitly]
  public static IEnumerable TraverseParmsSource()
  {
    foreach (var mode in Enum.GetValues(typeof(TraverseMode)).OfType<TraverseMode>())
    {
      var forMode = TraverseParms.For(mode);
      
      yield return new NamedLazy<TraverseParms>($"{mode}, normal pawn", () =>
      {
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        MakePawnPerfect(pawn);
        return forMode with { pawn = pawn };
      });
      
      yield return new NamedLazy<TraverseParms>($"{mode}, ability pawn", () =>
      {
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        MakePawnPerfect(pawn);
        pawn.abilities.abilities.Add(new Ability_GrapplingHook(pawn, new AbilityDef
        {
          defName = "VMF_Mock_GrapplingHookAbility",
          label = "grappling hook",
          abilityClass = typeof(Ability_GrapplingHook),
          casterMustBeCapableOfViolence = false,
          verbProperties = new VerbProperties
          {
            verbClass = typeof(Verb_LaunchZipline),
            defaultProjectile = DefDatabase<ThingDef>.GetNamed("VMF_Bullet_ZiplineTurret"),
            range = 4.9f,
            warmupTime = 0.5f,
            defaultCooldownTime = 1f,
            targetParams = TargetingParameters.ForCell()
          }
        }));
        return forMode with { pawn = pawn };
      });
      
      yield return new NamedLazy<TraverseParms>($"{mode}, no pawn", () => forMode);
    }
  }

  [OneTimeSetUp]
  public void OneTimeSetUp()
  {
    threadDisabler = new ThreadDisabler();
    var mapping = Map.GetCachedMapComponent<VehiclePathingSystem>();
    mapping.RequestGridsFor(Crawler, DeferredGridGeneration.Urgency.Urgent);
    Assert.IsFalse(mapping[Crawler].Suspended);
    Assert.IsTrue(mapping[Crawler].VehiclePathGrid.Enabled);
    if (!mapping.GridOwners.IsOwner(Crawler))
    {
      Assert.IsTrue(mapping[mapping.GridOwners.GetOwner(Crawler)].VehiclePathGrid.Enabled);
    }
    mapping.RequestGridsFor(Pantodon, DeferredGridGeneration.Urgency.Urgent);
    Assert.IsFalse(mapping[Pantodon].Suspended);
    Assert.IsTrue(mapping[Pantodon].VehiclePathGrid.Enabled);
    if (!mapping.GridOwners.IsOwner(Pantodon))
    {
      Assert.IsTrue(mapping[mapping.GridOwners.GetOwner(Pantodon)].VehiclePathGrid.Enabled);
    }
    
    var faction = Faction.OfPlayer;
    Crawlers =
    [
      (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(Crawler, faction),
      (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(Crawler, faction)
    ];
    Pantodons =
    [
      (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(Pantodon, faction),
      (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(Pantodon, faction),
      (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(Pantodon, faction)
    ];
  }

  [OneTimeTearDown]
  public void OneTimeTearDown()
  {
    threadDisabler.Dispose();
    threadDisabler = null;
    foreach (var crawler in Crawlers)
    {
      if (!crawler.Destroyed)
        crawler.Destroy();
    }

    foreach (var pantodon in Pantodons)
    {
      if (!pantodon.Destroyed)
        pantodon.Destroy();
    }
    
    if (traverseParms.Value.pawn is { Destroyed: false } pawn)
      pawn.Destroy();

    Crawlers = null;
    Pantodons = null;
    traverseParms.Reset();
  }
  
  [TearDown]
  public void TearDown()
  {
    foreach (var crawler in Crawlers)
    {
      if (crawler.Spawned)
        crawler.DeSpawn();
    }

    foreach (var pantodon in Pantodons)
    {
      if (pantodon.Spawned)
        pantodon.DeSpawn();
    }

    if (traverseParms.Value.pawn is { Spawned: true } pawn)
      pawn.DeSpawn();
    CrossMapReachabilityCache.ClearCache();
  }

  // 正常系
  [Test]
  public void GroundToVehicleMap()
  {
    Assert.IsNotNull(GenSpawn.Spawn(Crawlers[0], Map.Center, Map), "Crawler");

    var root = FromRUCorner(Map, 3);
    if (traverseParms.Value.pawn is { } pawn)
    {
      Assert.IsNotNull(GenSpawn.Spawn(pawn, root, Map), "Pawn");
    }

    var vehicleMap = Crawlers[0].VehicleMap;
    var result = CrossMapReachabilityUtility.CanReach(Map, root, vehicleMap.Center, PathEndMode.OnCell,
      traverseParms.Value, vehicleMap, out var exitSpot, out var enterSpot, out var spotsQueue);
    Expect.IsTrue(result, "result");
    Expect.AreEqual(exitSpot, TargetInfo.Invalid, $"exitSpot: {exitSpot}");
    Expect.AreNotEqual(enterSpot, TargetInfo.Invalid, $"enterSpot: {enterSpot}");
    Expect.IsNull(spotsQueue, $"spotsQueue: {string.Join(", ", spotsQueue ?? [])}");
  }

  [Test]
  public void VehicleMapToGround()
  {
    Assert.IsNotNull(GenSpawn.Spawn(Crawlers[0], Map.Center, Map), "Crawler");
    
    var dest = FromRUCorner(Map, 3);
    var vehicleMap = Crawlers[0].VehicleMap;
    if (traverseParms.Value.pawn is { } pawn)
    {
      Assert.IsNotNull(GenSpawn.Spawn(pawn, vehicleMap.Center, vehicleMap), "Pawn");
    }

    var result = CrossMapReachabilityUtility.CanReach(vehicleMap, vehicleMap.Center, dest, PathEndMode.OnCell,
      traverseParms.Value, Map, out var exitSpot, out var enterSpot, out var spotsQueue);
    Expect.IsTrue(result, "result");
    Expect.AreNotEqual(exitSpot, TargetInfo.Invalid, $"exitSpot: {exitSpot}");
    if (VehicleMapFramework.settings.legacyCanReach)
      Expect.AreEqual(enterSpot, TargetInfo.Invalid, $"enterSpot: {enterSpot}");
    else
      Expect.AreNotEqual(enterSpot, TargetInfo.Invalid, $"enterSpot: {enterSpot}");
    Expect.IsNull(spotsQueue, $"spotsQueue: {string.Join(", ", spotsQueue ?? [])}");
  }

  [Test]
  public void VehicleMapToVehicleMap()
  {
    var offset = Crawler.Size.x;
    Assert.IsNotNull(GenSpawn.Spawn(Crawlers[0], Map.Center + IntVec3.West * offset, Map), "Crawlers[0]");
    Assert.IsNotNull(GenSpawn.Spawn(Crawlers[1], Map.Center + IntVec3.East * offset, Map), "Crawlers[1]");
    
    if (traverseParms.Value.pawn is { } pawn)
    {
      Assert.IsNotNull(GenSpawn.Spawn(pawn, Crawlers[0].VehicleMap.Center, Crawlers[0].VehicleMap), "Pawn");
    }

    var result = CrossMapReachabilityUtility.CanReach(Crawlers[0].VehicleMap, Crawlers[0].VehicleMap.Center,
      Crawlers[1].VehicleMap.Center, PathEndMode.OnCell, traverseParms.Value, Crawlers[1].VehicleMap,
      out var exitSpot, out var enterSpot, out var spotsQueue);
    Expect.IsTrue(result, "result");
    Expect.AreNotEqual(exitSpot, TargetInfo.Invalid, $"exitSpot: {exitSpot}");
    Expect.AreNotEqual(enterSpot, TargetInfo.Invalid, $"enterSpot: {enterSpot}");
    Expect.IsNull(spotsQueue, $"spotsQueue: {string.Join(", ", spotsQueue ?? [])}");
  }

  [Test]
  public void VehicleMapToVehicleMapToVehicleMap()
  {
    var offset = Pantodon.Size.x + 1;
    var scope = new DeepOceanCellRectScope(offset * 3 + 6);
    
    Assert.IsNotNull(GenSpawn.Spawn(Pantodons[0], Map.Center + IntVec3.West * offset, Map), "Pantodons[0]");
    Assert.IsNotNull(GenSpawn.Spawn(Pantodons[1], Map.Center, Map), "Pantodons[1]");
    Assert.IsNotNull(GenSpawn.Spawn(Pantodons[2], Map.Center + IntVec3.East * offset, Map), "Pantodons[2]");
    
    if (traverseParms.Value.pawn is { } pawn)
    {
      Assert.IsNotNull(GenSpawn.Spawn(pawn, Pantodons[0].VehicleMap.Center, Pantodons[0].VehicleMap), "Pawn");
    }

    var result = CrossMapReachabilityUtility.CanReach(Pantodons[0].VehicleMap, Pantodons[0].VehicleMap.Center,
      Pantodons[2].VehicleMap.Center, PathEndMode.OnCell, traverseParms.Value, Pantodons[2].VehicleMap,
      out var exitSpot, out var enterSpot, out var spotsQueue);
    var canUseAbility = traverseParms.Name.ToLower().Contains("ability");
    Expect.IsTrue(result == canUseAbility, "result");
    Expect.AreEqual(exitSpot, TargetInfo.Invalid, $"exitSpot: {exitSpot}");
    Expect.AreEqual(enterSpot, TargetInfo.Invalid, $"enterSpot: {enterSpot}");
    Expect.IsTrue(!canUseAbility || spotsQueue is { Count: 2 }, $"spotsQueue: {string.Join(", ", spotsQueue ?? [])}");
    scope.Dispose();
  }

  private struct DeepOceanCellRectScope : IDisposable
  {
    private TerrainDef[] underTerrains;
    private TerrainDef[] topTerrains;
    private CellRect cellRect;

    public DeepOceanCellRectScope() : this(Map.Size.x)
    {
    }
    
    public DeepOceanCellRectScope(int size)
    {
      cellRect = CellRect.CenteredOn(Map.Center, size, size).ClipInsideMap(Map);
      underTerrains = new TerrainDef[cellRect.Area];
      topTerrains = new TerrainDef[cellRect.Area];
      var terrainGrid = Map.terrainGrid;
      var i = 0;
      foreach (var cell in cellRect)
      {
        underTerrains[i] = terrainGrid.UnderTerrainAt(i);
        topTerrains[i] = terrainGrid.TopTerrainAt(i);
        i++;
        terrainGrid.SetTerrain(cell, TerrainDefOf.WaterOceanDeep);
      }
    }

    public void Dispose()
    {
      var terrainGrid = Map.terrainGrid;
      var i = 0;
      foreach (var cell in cellRect)
      {
        if (underTerrains[i] is { } underTerrain)
          terrainGrid.SetUnderTerrain(cell, underTerrain);

        if (topTerrains[i] is { } topTerrain)
          terrainGrid.SetTerrain(cell, topTerrain);
        i++;
      }

      underTerrains = null;
      topTerrains = null;
    }
  }
}