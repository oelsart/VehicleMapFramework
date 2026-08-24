using DevTools.Benchmarking;
using LudeonTK;
using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework.Test_Logics;

[BenchmarkClass("CrossMapReachability", AllowedGameStates = AllowedGameStates.PlayingOnMap)]
internal sealed class CrossMapReachability_AStar
{
  [Prepare]
  private static void Prepare(ref ReachabilityContext context)
  {
    MakePawnPerfect(context.normalPawn);
    MakePawnPerfect(context.abilityPawn);
    context.ability.pawn = context.abilityPawn;
    context.ability.Initialize();
    context.abilityPawn.abilities.abilities.Add(context.ability);
    
    var offset = Crawler.Size.x;
    var center = Find.CurrentMap.Center;
    if (!context.crawlers[0].Spawned) GenSpawn.Spawn(context.crawlers[0], center + IntVec3.West * offset, Find.CurrentMap);
    if (!context.crawlers[1].Spawned) GenSpawn.Spawn(context.crawlers[1], center + IntVec3.East * offset, Find.CurrentMap);
    InitializeVehicle(context.crawlers[0]);
    InitializeVehicle(context.crawlers[1]);
    
    if (!context.normalPawn.Spawned) GenSpawn.Spawn(context.normalPawn, center, Find.CurrentMap);
    if (!context.abilityPawn.Spawned) GenSpawn.Spawn(context.abilityPawn, center + IntVec3.South, Find.CurrentMap);
    return;
    
    static void InitializeVehicle(VehiclePawnWithMap crawler)
    {
      for (var i = 0; i < crawler.CachedMapEdgeCells.Count; i++)
      {
        _ = crawler.GetCachedEnterPosition(i);
      }
    }
  }

  [Benchmark]
  private static void AStar_VehicleMapToGround_NormalPawn(ref ReachabilityContext context)
  {
    VehicleMapToGround(CrossMapReachabilityUtility.aStar, TraverseParms.For(context.normalPawn), null, ref context);
  }

  [Benchmark]
  private static void AStarNew_VehicleMapToGround_NormalPawn(ref ReachabilityContext context)
  {
    VehicleMapToGround(CrossMapReachabilityUtility.aStar_new, TraverseParms.For(context.normalPawn), null, ref context);
  }

  [Benchmark]
  private static void AStar_VehicleMapToGround_AbilityPawn(ref ReachabilityContext context)
  {
    VehicleMapToGround(CrossMapReachabilityUtility.aStar, TraverseParms.For(context.abilityPawn), context.ability, ref context);
  }

  [Benchmark]
  private static void AStarNew_VehicleMapToGround_AbilityPawn(ref ReachabilityContext context)
  {
    VehicleMapToGround(CrossMapReachabilityUtility.aStar_new, TraverseParms.For(context.abilityPawn), context.ability, ref context);
  }

  [Benchmark]
  private static void AStar_GroundToVehicleMap_NormalPawn(ref ReachabilityContext context)
  {
    GroundToVehicleMap(CrossMapReachabilityUtility.aStar, TraverseParms.For(context.normalPawn), null, ref context);
  }

  [Benchmark]
  private static void AStarNew_GroundToVehicleMap_NormalPawn(ref ReachabilityContext context)
  {
    GroundToVehicleMap(CrossMapReachabilityUtility.aStar_new, TraverseParms.For(context.normalPawn), null, ref context);
  }

  [Benchmark]
  private static void AStar_GroundToVehicleMap_AbilityPawn(ref ReachabilityContext context)
  {
    GroundToVehicleMap(CrossMapReachabilityUtility.aStar, TraverseParms.For(context.abilityPawn), context.ability, ref context);
  }

  [Benchmark]
  private static void AStarNew_GroundToVehicleMap_AbilityPawn(ref ReachabilityContext context)
  {
    GroundToVehicleMap(CrossMapReachabilityUtility.aStar_new, TraverseParms.For(context.abilityPawn), context.ability, ref context);
  }

  [Benchmark]
  private static void AStar_VehicleMapToVehicleMap_NormalPawn(ref ReachabilityContext context)
  {
    VehicleMapToVehicleMap(CrossMapReachabilityUtility.aStar, TraverseParms.For(context.normalPawn), null, ref context);
  }

  [Benchmark]
  private static void AStarNew_VehicleMapToVehicleMap_NormalPawn(ref ReachabilityContext context)
  {
    VehicleMapToVehicleMap(CrossMapReachabilityUtility.aStar_new, TraverseParms.For(context.normalPawn), null, ref context);
  }

  [Benchmark]
  private static void AStar_VehicleMapToVehicleMap_AbilityPawn(ref ReachabilityContext context)
  {
    VehicleMapToVehicleMap(CrossMapReachabilityUtility.aStar, TraverseParms.For(context.abilityPawn), context.ability, ref context);
  }

  [Benchmark]
  private static void AStarNew_VehicleMapToVehicleMap_AbilityPawn(ref ReachabilityContext context)
  {
    VehicleMapToVehicleMap(CrossMapReachabilityUtility.aStar_new, TraverseParms.For(context.abilityPawn), context.ability, ref context);
  }

  private static void VehicleMapToGround(
    CrossMapReachabilityUtility.AStar<CrossMapReachabilityUtility.MapTraverse> aStar,
    TraverseParms traverseParms, Ability_MapTraverse ability, ref ReachabilityContext context)
  {
    DepartMapToDestMap(context.crawlers[0].VehicleMap, Find.CurrentMap, aStar, traverseParms, ability);
  }

  private static void GroundToVehicleMap(
    CrossMapReachabilityUtility.AStar<CrossMapReachabilityUtility.MapTraverse> aStar,
    TraverseParms traverseParms, Ability_MapTraverse ability, ref ReachabilityContext context)
  {
    DepartMapToDestMap(Find.CurrentMap, context.crawlers[0].VehicleMap, aStar, traverseParms, ability);
  }

  private static void VehicleMapToVehicleMap(
    CrossMapReachabilityUtility.AStar<CrossMapReachabilityUtility.MapTraverse> aStar,
    TraverseParms traverseParms, Ability_MapTraverse ability, ref ReachabilityContext context)
  {
    DepartMapToDestMap(context.crawlers[0].VehicleMap, context.crawlers[1].VehicleMap, aStar, traverseParms, ability);
  }

  private static void DepartMapToDestMap(
    Map departMap, Map destMap,
    CrossMapReachabilityUtility.AStar<CrossMapReachabilityUtility.MapTraverse> aStar,
    TraverseParms traverseParms, Ability_MapTraverse ability)
  {
    var root = departMap.Center;
    var dest = destMap.Center;
    var start = new CrossMapReachabilityUtility.MapTraverse(TargetInfo.Invalid, new TargetInfo(root, departMap));
    var destination = new CrossMapReachabilityUtility.MapTraverse(TargetInfo.Invalid, new TargetInfo(dest, destMap));
    CrossMapReachabilityUtility.traverser.SetParameters(start.enterSpot, destination.enterSpot, traverseParms, ability);
    CrossMapReachabilityUtility.traverseList.Clear();
    aStar.Run(start, destination, CrossMapReachabilityUtility.traverseList);
  }

  [OnFinish]
  private static void OnFinish(ref ReachabilityContext context)
  {
    foreach (var crawler in context.crawlers)
    {
      if (!crawler.Destroyed) crawler.Destroy();
    }
    if (!context.normalPawn.Destroyed) context.normalPawn.Destroy();
  }
  
  private readonly struct ReachabilityContext()
  {
    public readonly VehiclePawnWithMap[] crawlers =
    [
      (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(Crawler, Faction.OfPlayer),
      (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(Crawler, Faction.OfPlayer)
    ];
    public readonly Pawn normalPawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
    public readonly Pawn abilityPawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
    public readonly Ability_GrapplingHook ability = GrapplingAbility;
  }
}