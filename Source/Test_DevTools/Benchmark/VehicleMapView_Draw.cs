using DevTools.Benchmarking;
using LudeonTK;
using RimWorld;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Verse;

namespace VehicleMapFramework.Test_Logics;

[BenchmarkClass("VehicleMapView", AllowedGameStates = AllowedGameStates.PlayingOnMap, RunAsync = false)]
internal sealed class VehicleMapView_Draw
{
  [Prepare]
  private static void Prepare(ref VehicleMapViewContext context)
  {
    if (!context.Crawler.Spawned)
      GenSpawn.Spawn(context.Crawler, context.TestMap.Center, context.TestMap);

    Patch_Game_CurrentMap.ForceSet = true;
    Current.Game.CurrentMap = context.Crawler.VehicleMap;
    Patch_Game_CurrentMap.ForceSet = false;

    Find.Camera.enabled = false;
    
    LongEventHandler.ForceExecuteToExecuteWhenFinished();

    VehicleMapView.Available = true;
    Patch_Map_MapUpdate.Postfix(context.Crawler.VehicleMap);

    VehicleMapView.Available = false;
    Patch_Map_MapUpdate.Postfix(context.Crawler.VehicleMap);
  }
  
  [OnFinish]
  private static void OnFinish(ref VehicleMapViewContext context)
  {
    Current.Game.CurrentMap = context.TestMap;
    if (!context.Crawler.Destroyed)
      context.Crawler.Destroy();
    VehicleMapView.Available = true;
    Find.Camera.enabled = true;
  }

  [Benchmark]
  private static void RenderTextureDriven(ref VehicleMapViewContext context)
  {
    VehicleMapView.Available = false;
    Patch_Map_MapUpdate.Postfix(context.Crawler.VehicleMap);
  }
  
  [Benchmark]
  private static void VehicleMapViewDriven(ref VehicleMapViewContext context)
  {
    VehicleMapView.Available = true;
    Patch_Map_MapUpdate.Postfix(context.Crawler.VehicleMap);
  }
  
  private readonly struct VehicleMapViewContext()
  {
    public readonly Map TestMap = Find.CurrentMap;
    public readonly VehiclePawnWithMap Crawler = (VehiclePawnWithMap)VehicleSpawner.GenerateVehicle(TestUtility.Crawler, Faction.OfPlayer);
  }
}