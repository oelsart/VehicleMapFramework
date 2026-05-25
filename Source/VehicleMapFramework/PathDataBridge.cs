using System;
using System.Collections;
using HarmonyLib;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

internal readonly ref struct PathDataBridge(object value)
{
  public object Value => value;
}

internal readonly ref struct PathDataIndexer(VehiclePathingSystem pathing)
{
  public PathDataBridge this[VehicleDef vehicleDef] => pathing.GetPathData(vehicleDef);
}

[StaticConstructorOnStartup]
internal static class PathDataBridgeExtensions
{

  private static readonly FastInvokeHandler PathDataContainer;
  private static readonly FastInvokeHandler PathFinder;
  private static readonly FastInvokeHandler Item;
  private static readonly FastInvokeHandler VehiclePathGrid;
  private static readonly FastInvokeHandler VehicleReachability;
  private static readonly FastInvokeHandler VehicleRegionAndRoomUpdater;
  private static readonly FastInvokeHandler PathFinderManager;
  private static readonly AccessTools.FieldRef<object, IList> pathFinders;

  static PathDataBridgeExtensions()
  {
    var t_PathData = GenTypes.GetTypeInAnyAssembly("Vehicles.PathData", "Vehicles");
    Updated = t_PathData is not null;
    if (Updated)
    {
      var g_PathData = AccessTools.PropertyGetter(typeof(VehiclePathingSystem), "PathData");
      if (g_PathData is not null)
        PathDataContainer = MethodInvoker.GetHandler(g_PathData);
      var g_PathFinder = AccessTools.PropertyGetter(typeof(VehiclePathingSystem), "PathFinder");
      if (g_PathFinder is not null)
        PathFinder = MethodInvoker.GetHandler(g_PathFinder);
      var g_VehiclePathGrid = AccessTools.PropertyGetter(t_PathData, "VehiclePathGrid");
      if (g_VehiclePathGrid is not null)
        VehiclePathGrid = MethodInvoker.GetHandler(g_VehiclePathGrid);
      var g_VehicleReachability = AccessTools.PropertyGetter(t_PathData, "VehicleReachability");
      if (g_VehicleReachability is not null)
        VehicleReachability = MethodInvoker.GetHandler(g_VehicleReachability);
      var g_VehicleRegionAndRoomUpdater = AccessTools.PropertyGetter(t_PathData, "VehicleRegionAndRoomUpdater");
      if (g_VehicleRegionAndRoomUpdater is not null)
        VehicleRegionAndRoomUpdater = MethodInvoker.GetHandler(g_VehicleRegionAndRoomUpdater);
      var g_PathFinderManager = AccessTools.PropertyGetter(typeof(VehiclePathingSystem), "PathFinderManager");
      if (g_PathFinderManager is not null)
        PathFinderManager = MethodInvoker.GetHandler(g_PathFinderManager);
      var f_pathFinders = AccessTools.Field("Vehicles.PathFinderManager:pathFinders");
      if (f_pathFinders is not null)
        pathFinders = AccessTools.FieldRefAccess<object, IList>(f_pathFinders);
    }
    else
    {
      var t_VehiclePathData = GenTypes.GetTypeInAnyAssembly("Vehicles.VehiclePathData", "Vehicles");
      var g_VehiclePathGrid = AccessTools.PropertyGetter(t_VehiclePathData, "VehiclePathGrid");
      if (g_VehiclePathGrid is not null)
        VehiclePathGrid = MethodInvoker.GetHandler(g_VehiclePathGrid);
      var g_VehicleReachability = AccessTools.PropertyGetter(t_VehiclePathData, "VehicleReachability");
      if (g_VehicleReachability is not null)
        VehicleReachability = MethodInvoker.GetHandler(g_VehicleReachability);
      var g_VehicleRegionAndRoomUpdater = AccessTools.PropertyGetter(t_VehiclePathData, "VehicleRegionAndRoomUpdater");
      if (g_VehicleRegionAndRoomUpdater is not null)
        VehicleRegionAndRoomUpdater = MethodInvoker.GetHandler(g_VehicleRegionAndRoomUpdater);
    }
    var g_Item = AccessTools.PropertyGetter(typeof(VehiclePathingSystem), "Item");
    if (g_Item is not null)
      Item = MethodInvoker.GetHandler(g_Item);
  }

  private static bool Updated { get; }

  extension(VehiclePathingSystem pathing)
  {
    public PathDataIndexer BridgeIndexer => new(pathing);

    public PathDataBridge GetPathData(VehicleDef vehicleDef)
    {
      return new PathDataBridge(Item(pathing, SingleParam.Get(vehicleDef)));
    }

    public void GeneratePathData(VehicleDef vehicleDef)
    {
      if (Updated)
      {
        var t_PathGridCalculator = GenTypes.GetTypeInAnyAssembly("Vehicles.PathGridCalculator", "Vehicles");
        if (t_PathGridCalculator is null) return;

        var container = PathDataContainer?.Invoke(pathing);
        if (container is null) return;

        var calculator = Activator.CreateInstance(t_PathGridCalculator);
        var pathFinder = PathFinder?.Invoke(pathing);
        if (pathFinder is null) return;

        var @params = Params<ValueTuple<object, object, object>>.Get((calculator, vehicleDef, pathFinder));
        UniqueVehicleUtility.GeneratePathData?.Invoke(container, @params);

        var pathFinderManager = PathFinderManager?.Invoke(pathing);
        if (pathFinderManager is null) return;

        var pathFinderArray = pathFinders(pathFinderManager);
        if (pathFinderArray is null) return;

        var t_PathFindImpl = GenTypes.GetTypeInAnyAssembly("Vehicles.PathFinderManager+PathFindImpl", "Vehicles");
        if (t_PathFindImpl is null) return;

        var params2 = Params<ValueTuple<object, object>>.Get((pathFinderManager, vehicleDef));
        pathFinderArray[vehicleDef.DefIndex] = Activator.CreateInstance(t_PathFindImpl, params2);
        return;
      }
      UniqueVehicleUtility.GeneratePathData?.Invoke(pathing, SingleParam.Get(vehicleDef));
    }
  }

  extension(PathDataBridge pathData)
  {
    public VehiclePathGrid VehiclePathGrid => (VehiclePathGrid)VehiclePathGrid(pathData.Value);

    public VehicleReachability VehicleReachability => (VehicleReachability)VehicleReachability(pathData.Value);

    public VehicleRegionAndRoomUpdater VehicleRegionAndRoomUpdater => (VehicleRegionAndRoomUpdater)VehicleRegionAndRoomUpdater(pathData.Value);
  }
}
