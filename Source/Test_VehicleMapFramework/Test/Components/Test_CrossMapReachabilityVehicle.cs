using System.Collections;
using DevTools.Testing;
using JetBrains.Annotations;
using RimWorld;
using SmashTools;
using UnityEngine.Assertions;
using Vehicles;
using Vehicles.Tests;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.Test;

/// <summary>
/// CrossMapReachabilityUtility の <c>VehiclePawn.CanReachVehicle</c> のテスト。
/// 移動する車両は通常の VehiclePawn(モック)、乗り込み先は VehiclePawnWithMap(モック)を使う。
/// モックのマップ車両は ramp を持たないので、テスト内で車両マップに ramp を置く。
/// </summary>
[TestFixture(TestType.Playing)]
[TestCategory("CrossMapReachability")]
internal sealed class Test_CrossMapReachabilityVehicle
{
  // ramp の幅(def.size.x)以下であること。CanReachVehicle は ramp 幅 >= 車両幅 を要求する。
  private static readonly IntVec2 PlainVehicleSize = new(1, 2);

  // ramp を置いても車両が動ける広さがあること。
  private static readonly IntVec2 MapVehicleSize = new(10, 12);

  // 地上マップで、車両マップを持つ車両から離れた位置に置く距離
  private const int StartMargin = 6;

  // マップ車両を2台置くときの、地上マップ中心からの距離(フットプリントが重ならないように)
  private static readonly int MapVehicleOffset = MapVehicleSize.x * 2;

  private ThreadDisabler threadDisabler;
  private VehicleGroup group;
  private readonly VehicleGroup[] mapGroups = new VehicleGroup[2];

  private static Map Map => Find.CurrentMap;

  private VehiclePawn Vehicle => group.vehicle;

  [UsedImplicitly]
  public static IEnumerable AllTraverseModes()
  {
    return Enum.GetValues<TraverseMode>().Cast<object>();
  }

  [OneTimeSetUp]
  private void OneTimeSetUp()
  {
    threadDisabler = new ThreadDisabler();
  }

  [OneTimeTearDown]
  private void OneTimeTearDown()
  {
    threadDisabler?.Dispose();
    threadDisabler = null;
  }

  [SetUp]
  private void SetUp()
  {
    group = VehicleGroup.CreateBasicVehicleGroup(new VehicleGroup.MockSettings { size = PlainVehicleSize });
    EnsurePathGrids(Vehicle.VehicleDef);
  }

  [TearDown]
  private void TearDown()
  {
    // 通常車両は車両マップ上にいることがあるので、先に破棄してから車両マップ側を破棄する
    group?.Dispose();
    group = null;
    for (var i = mapGroups.Length - 1; i >= 0; i--)
    {
      mapGroups[i]?.Dispose();
      mapGroups[i] = null;
    }
  }

  // ---------------------------------------------------------------- 正常系

  [Test]
  private void SameMap_ReachableCell()
  {
    var start = Map.Center;
    SpawnVehicle(start, Map);
    var dest = start + IntVec3.East * 8;
    Assert.IsTrue(Vehicle.Drivable(dest, Map), $"Precondition: dest {dest} must be drivable.");

    var result = CanReach(dest, Map, out var exitSpot, out var enterSpot);

    Expect.IsTrue(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: false);
  }

  [Test]
  private void SameMap_SameCellAsVehicle()
  {
    SpawnVehicle(Map.Center, Map);

    var result = CanReach(Vehicle.Position, Map, out var exitSpot, out var enterSpot);

    Expect.IsTrue(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: false);
  }

  [Test]
  private void GroundToVehicleMap_ThroughRamp([ParametersSource(nameof(AllTraverseModes))] TraverseMode mode)
  {
    var mapVehicle = SpawnMapVehicle(0, Map.Center);
    var ramp = AddRamp(mapVehicle);
    SpawnVehicle(FromRUCorner(Map, StartMargin), Map);
    // ramp の内側のセル。CanReachVehicle が第2区間の起点にするセルと同じ。
    var dest = InnerRampCell(ramp);

    var result = CanReach(dest, mapVehicle.VehicleMap, out var exitSpot, out var enterSpot, mode);

    Expect.IsTrue(result, $"result (mode: {mode})");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: true);
    Expect.ReferencesAreEqual(enterSpot.Map, mapVehicle.VehicleMap, "enterSpot should be on the vehicle map");
    Expect.IsNotNull(enterSpot.Thing?.TryGetComp<CompVehicleEnterSpot>(), "enterSpot should be a ramp");
  }

  [Test]
  private void VehicleMapToGround_ThroughRamp([ParametersSource(nameof(AllTraverseModes))] TraverseMode mode)
  {
    var mapVehicle = SpawnMapVehicle(0, Map.Center);
    var ramp = AddRamp(mapVehicle);
    SpawnVehicle(InnerRampCell(ramp), mapVehicle.VehicleMap, ramp.parent.Rotation);
    // ramp の外側のセル(地上マップ座標)。
    var dest = OuterRampCell(ramp);

    var result = CanReach(dest, Map, out var exitSpot, out var enterSpot, mode);

    Expect.IsTrue(result, $"result (mode: {mode})");
    ExpectSpots(exitSpot, enterSpot, expectExit: true, expectEnter: false);
    Expect.ReferencesAreEqual(exitSpot.Map, mapVehicle.VehicleMap, "exitSpot should be on the vehicle map");
    Expect.IsNotNull(exitSpot.Thing?.TryGetComp<CompVehicleEnterSpot>(), "exitSpot should be a ramp");
  }

  [Test]
  private void VehicleMapToVehicleMap_ThroughBothRamps()
  {
    // 車両の間は ramp 同士を結ぶ経路が通れる程度に離す
    var offset = MapVehicleOffset;
    var departVehicle = SpawnMapVehicle(0, Map.Center + IntVec3.West * offset);
    var destVehicle = SpawnMapVehicle(1, Map.Center + IntVec3.East * offset);
    var departRamp = AddRamp(departVehicle);
    var destRamp = AddRamp(destVehicle);
    SpawnVehicle(InnerRampCell(departRamp), departVehicle.VehicleMap, departRamp.parent.Rotation);
    var dest = InnerRampCell(destRamp);

    var result = CanReach(dest, destVehicle.VehicleMap, out var exitSpot, out var enterSpot);

    Expect.IsTrue(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: true, expectEnter: true);
    Expect.ReferencesAreEqual(exitSpot.Map, departVehicle.VehicleMap, "exitSpot should be on the depart vehicle map");
    Expect.ReferencesAreEqual(enterSpot.Map, destVehicle.VehicleMap, "enterSpot should be on the dest vehicle map");
  }

  // ---------------------------------------------------------------- 状態・副作用

  [Test]
  private void GroundToVehicleMap_RepeatedCallsAndCacheClear()
  {
    var mapVehicle = SpawnMapVehicle(0, Map.Center);
    SpawnVehicle(FromRUCorner(Map, StartMargin), Map);
    var dest = InnerRampCell(AddRamp(mapVehicle));
    var destMap = mapVehicle.VehicleMap;

    var first = CanReach(dest, destMap, out _, out var enterSpot1);
    var second = CanReach(dest, destMap, out _, out var enterSpot2);
    CrossMapReachabilityCache.ClearCache();
    var afterClear = CanReach(dest, destMap, out _, out var enterSpot3);

    Expect.IsTrue(first, "first");
    Expect.AreEqual(second, first, "second call");
    Expect.AreEqual(afterClear, first, "after ClearCache");
    Expect.AreEqual(enterSpot2, enterSpot1, "enterSpot (second call)");
    Expect.AreEqual(enterSpot3, enterSpot1, "enterSpot (after ClearCache)");
  }

  [Test]
  private void GroundToVehicleMap_DoesNotMoveVehicle()
  {
    var mapVehicle = SpawnMapVehicle(0, Map.Center);
    SpawnVehicle(FromRUCorner(Map, StartMargin), Map);
    var dest = InnerRampCell(AddRamp(mapVehicle));
    var position = Vehicle.Position;
    var rotation = Vehicle.Rotation;
    var map = Vehicle.Map;

    CanReach(dest, mapVehicle.VehicleMap, out _, out _);

    Expect.IsTrue(Vehicle.Spawned, "Spawned");
    Expect.AreEqual(Vehicle.Position, position, "Position");
    Expect.AreEqual(Vehicle.Rotation, rotation, "Rotation");
    Expect.ReferencesAreEqual(Vehicle.Map, map, "Map");
  }

  // ---------------------------------------------------------------- 異常系

  [Test]
  private void SameMap_UnspawnedVehicle()
  {
    var result = CanReach(Map.Center, Map, out var exitSpot, out var enterSpot);

    Expect.IsFalse(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: false);
  }

  [Test]
  private void SpawnedVehicle_NullDestMap()
  {
    SpawnVehicle(Map.Center, Map);

    var result = CanReach(Map.Center + IntVec3.East * 8, null, out var exitSpot, out var enterSpot);

    Expect.IsFalse(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: false);
  }

  [Test]
  private void GroundToVehicleMap_DestinationEnclosedByWalls()
  {
    var mapVehicle = SpawnMapVehicle(0, Map.Center);
    var ramp = AddRamp(mapVehicle);
    SpawnVehicle(FromRUCorner(Map, StartMargin), Map);
    var destMap = mapVehicle.VehicleMap;
    // ramp から離れたセルを目的地にし、その周囲だけを壁で囲む(ramp 自体は残す)。
    var dest = InnerRampCell(ramp) + ramp.parent.Rotation.FacingCell * 3;
    var ring = CellRect.CenteredOn(dest, 3, 3);
    Assert.IsTrue(ring.Cells.All(c => c.InBounds(destMap)), $"Precondition: ring around {dest} must fit in the vehicle map.");
    Assert.IsTrue(Vehicle.Drivable(dest, destMap), $"Precondition: dest {dest} must be drivable.");
    Assert.IsTrue(CanReach(dest, destMap, out _, out _), "Precondition: dest must be reachable before enclosing.");

    using var walls = new WalledRectScope(ring, destMap);
    var result = CanReach(dest, destMap, out var exitSpot, out var enterSpot);

    Expect.IsFalse(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: false);
  }

  [Test]
  private void VehicleMapToGround_DestinationEnclosedByWalls()
  {
    var mapVehicle = SpawnMapVehicle(0, Map.Center);
    var ramp = AddRamp(mapVehicle);
    SpawnVehicle(InnerRampCell(ramp), mapVehicle.VehicleMap, ramp.parent.Rotation);
    // 地上側は車両の外側(ramp の向きと逆方向)へ離れた位置を囲む。車両の足元には壁を置かない。
    var dest = OuterRampCell(ramp) - ramp.parent.Rotation.FacingCell * 6;
    var ring = CellRect.CenteredOn(dest, 3, 3);
    Assert.IsTrue(ring.Cells.All(c => c.InBounds(Map)), $"Precondition: ring around {dest} must fit in the map.");
    Assert.IsTrue(CanReach(dest, Map, out _, out _), "Precondition: dest must be reachable before enclosing.");

    using var walls = new WalledRectScope(ring, Map);
    var result = CanReach(dest, Map, out var exitSpot, out var enterSpot);

    Expect.IsFalse(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: false);
  }

  [Test]
  private void GroundToVehicleMap_VehicleMapEdgeWalled()
  {
    var mapVehicle = SpawnMapVehicle(0, Map.Center);
    SpawnVehicle(FromRUCorner(Map, StartMargin), Map);
    // 壁で ramp が壊れる前に目的地を決める
    var dest = InnerRampCell(AddRamp(mapVehicle));

    using var walls = new WalledScope(mapVehicle);
    CrossMapReachabilityCache.ClearCache();
    var result = CanReach(dest, mapVehicle.VehicleMap, out var exitSpot, out var enterSpot);

    Expect.IsFalse(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: false);
  }

  [Test]
  private void VehicleMapToGround_VehicleMapEdgeWalled()
  {
    var mapVehicle = SpawnMapVehicle(0, Map.Center);
    var ramp = AddRamp(mapVehicle);
    SpawnVehicle(InnerRampCell(ramp), mapVehicle.VehicleMap, ramp.parent.Rotation);
    var dest = OuterRampCell(ramp);

    using var walls = new WalledScope(mapVehicle);
    CrossMapReachabilityCache.ClearCache();
    var result = CanReach(dest, Map, out var exitSpot, out var enterSpot);

    Expect.IsFalse(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: false);
  }

  [Test]
  [TestDescription("VehiclePawnWithMap cannot enter another VehiclePawnWithMap's vehicle map (framework spec).")]
  private void MapVehicle_ToOtherVehicleMap()
  {
    var offset = MapVehicleSize.x;
    var traveler = SpawnMapVehicle(0, Map.Center + IntVec3.West * offset);
    var other = SpawnMapVehicle(1, Map.Center + IntVec3.East * offset);

    var result = traveler.CanReachVehicle(other.VehicleMap.Center, PathEndMode.OnCell, Danger.Deadly,
      TraverseMode.ByPawn, other.VehicleMap, out var exitSpot, out var enterSpot);

    Expect.IsFalse(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: false);
  }

  // ---------------------------------------------------------------- 回帰

  [Test]
  private void UnspawnedVehicle_InvalidDestAndNullDestMap()
  {
    Assert.IsFalse(Vehicle.Spawned, "Precondition: vehicle must be unspawned.");

    var result = Vehicle.CanReachVehicle(LocalTargetInfo.Invalid, PathEndMode.OnCell, Danger.Deadly,
      TraverseMode.ByPawn, null, out var exitSpot, out var enterSpot);

    Expect.IsFalse(result, "result");
    ExpectSpots(exitSpot, enterSpot, expectExit: false, expectEnter: false);
  }

  // ---------------------------------------------------------------- ヘルパー

  private static void EnsurePathGrids(VehicleDef def)
  {
    var mapping = Map.GetCachedMapComponent<VehiclePathingSystem>();
    mapping.RequestGridsFor(def, DeferredGridGeneration.Urgency.Urgent);
    Assert.IsFalse(mapping[def].Suspended, $"{def.defName} path grid is suspended.");
    Assert.IsTrue(mapping[def].VehiclePathGrid.Enabled, $"{def.defName} path grid is not enabled.");
  }

  private bool CanReach(LocalTargetInfo dest, Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot,
    TraverseMode mode = TraverseMode.ByPawn, Danger danger = Danger.Deadly)
  {
    // ByPawn は VehicleCanGoto / PawnGotoAction(実運用)と同じ既定値
    return Vehicle.CanReachVehicle(dest, PathEndMode.OnCell, danger, mode, destMap, out exitSpot, out enterSpot);
  }

  private void SpawnVehicle(IntVec3 cell, Map map, Rot4? rot = null)
  {
    Assert.IsNotNull(GenSpawn.Spawn(Vehicle, cell, map, rot ?? Rot4.North), $"Vehicle at {cell}");
  }

  private VehiclePawnWithMap SpawnMapVehicle(int index, IntVec3 cell)
  {
    // 必要になったときに生成する。TearDown で Dispose される。
    mapGroups[index] ??= CreateBasicMapVehicleGroup(new VehicleGroup.MockSettings { size = MapVehicleSize });
    var mapVehicle = mapGroups[index].vehicle as VehiclePawnWithMap;
    Assert.IsNotNull(mapVehicle, $"mapGroups[{index}].vehicle must be a VehiclePawnWithMap.");
    Assert.IsNotNull(GenSpawn.Spawn(mapVehicle, cell, Map, Rot4.North), $"mapVehicles[{index}] at {cell}");
    Assert.IsNotNull(mapVehicle!.VehicleMap, $"mapVehicles[{index}] must have a vehicle map.");
    // 通常車両が車両マップ上を走るので、車両マップ側にも経路グリッドを作っておく
    var mapping = mapVehicle.VehicleMap.GetCachedMapComponent<VehiclePathingSystem>();
    mapping.RequestGridsFor(Vehicle.VehicleDef, DeferredGridGeneration.Urgency.Urgent);
    return mapVehicle;
  }

  private static CompVehicleEnterSpot AddRamp(VehiclePawnWithMap mapVehicle)
  {
    var rampDef = DefDatabase<ThingDef>.GetNamed("VMF_RampMedium");
    var ramp = ThingMaker.MakeThing(rampDef);
    ramp.SetFactionDirect(Faction.OfPlayer);
    var pos = new IntVec3(2, 0, 1);
    Assert.IsNotNull(GenSpawn.Spawn(ramp, pos, mapVehicle.VehicleMap), $"ramp at {pos}");
    return ramp.TryGetComp<CompVehicleEnterSpot>();
  }

  private CompVehicleEnterSpot FindRamp(VehiclePawnWithMap mapVehicle)
  {
    var ramps = mapVehicle.GetSortedEnterComps(mapVehicle.VehicleMap.Center, CompVehicleEnterSpot.Kind.RampOnly);
    var ramp = ramps.FirstOrDefault(c => c.parent.def.size.x >= Vehicle.VehicleDef.size.x);
    Assert.IsNotNull(ramp,
      $"Precondition: {mapVehicle.def.defName} needs a ramp at least {Vehicle.VehicleDef.size.x} cells wide " +
      $"(ramps found: {ramps.Count}).");
    return ramp;
  }

  // CanReachVehicle の第2区間の起点と同じ計算(車両マップ座標)
  private IntVec3 InnerRampCell(CompVehicleEnterSpot ramp)
  {
    return ramp.parent.Position + ramp.parent.Rotation.FacingCell * Vehicle.HalfLength();
  }

  // ramp の外側のセル(ramp が置かれたマップの親マップ座標)
  private IntVec3 OuterRampCell(CompVehicleEnterSpot ramp)
  {
    return CrossMapReachabilityUtility.EnterVehiclePosition(ramp.parent, Vehicle);
  }

  private static void ExpectSpots(TargetInfo exitSpot, TargetInfo enterSpot, bool expectExit, bool expectEnter)
  {
    if (expectExit)
      Expect.AreNotEqual(exitSpot, TargetInfo.Invalid, "exitSpot should be set");
    else
      Expect.AreEqual(exitSpot, TargetInfo.Invalid, $"exitSpot should be Invalid: {exitSpot}");

    if (expectEnter)
      Expect.AreNotEqual(enterSpot, TargetInfo.Invalid, "enterSpot should be set");
    else
      Expect.AreEqual(enterSpot, TargetInfo.Invalid, $"enterSpot should be Invalid: {enterSpot}");
  }

  /// <summary>矩形の外周を壁で囲む。Dispose で壁を破棄する。</summary>
  private sealed class WalledRectScope : IDisposable
  {
    private readonly List<Thing> walls = [];

    public WalledRectScope(CellRect rect, Map map)
    {
      try
      {
        foreach (var cell in rect.EdgeCells)
        {
          var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
          wall.SetFactionDirect(Faction.OfPlayer);
          walls.Add(wall);
          Assert.IsNotNull(GenSpawn.Spawn(wall, cell, map), $"wall at {cell}");
        }
      }
      catch
      {
        Dispose();
        throw;
      }
    }

    public void Dispose()
    {
      foreach (var wall in walls)
      {
        if (wall is { Destroyed: false })
          wall.Destroy();
      }

      walls.Clear();
    }
  }
}