using DevTools.Benchmarking;
using LudeonTK;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework.Test_DevTools;

[BenchmarkClass("MapPawns", AllowedGameStates = AllowedGameStates.PlayingOnMap, RunAsync = false)]
internal sealed class Benchmark_MapPawns
{
  [Benchmark]
  private static IReadOnlyList<Pawn> AllPawnsSpawned_Vanilla()
  {
    return Patch_MapPawns_AllPawnsSpawned.AllPawnsSpawned(Find.CurrentMap.mapPawns);
  }
  
  [Benchmark]
  private static IReadOnlyList<Pawn> AllPawnsSpawned()
  {
    var map = Find.CurrentMap;
    var result = Patch_MapPawns_AllPawnsSpawned.AllPawnsSpawned(map.mapPawns);
    Patch_MapPawns_AllPawnsSpawned.Postfix(ref result, map);
    return result;
  }
}