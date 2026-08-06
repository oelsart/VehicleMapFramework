using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class CrossMapReachabilityUtility
{
  public static bool working;

  private static ConditionalWeakTable<Pawn, Map> DestMaps { get; } = [];

  private static ConditionalWeakTable<Pawn, Map> DepartMaps { get; } = [];

  private static Dictionary<Pawn, IntVec3?> DepartPositions { get; } = [];

  public static Map DepartMapGlobal;

  [UsedImplicitly]
  public static Map DestMapGlobal;

  private static readonly Traverser traverser = new();

  private static readonly AStar<MapTraverse> aStar = new(Traverser.Cost, traverser.Neighbors, traverser.FinalCheck,
    traverser.CanEnter, traverser.Heuristic, Traverser.ProcessPath, traverser.DebugDrawEnterNode);

  private static readonly List<MapTraverse> traverseList = [with(16)];

  private static readonly Stack<TraverseSpots> tmpTargets = [with(16)];

  private static readonly HashSet<Map> visitedMaps = [with(16)];

  private static readonly List<Map> candidateMaps = [with(16)];

  private static readonly List<Region> destRegions = [];

#if DEBUG
    public static bool enableDebugLog;
#endif

  [Conditional("DEBUG")]
  internal static void DebugLog(string message)
  {
#if DEBUG
        if (!enableDebugLog) return;
#endif
    VMF_Log.DebugMessage($"[CrossMapReachability] {message}");
  }

  extension(Pawn pawn)
  {
    public Map DestMap
    {
      get
      {
        if (pawn is null) return null;
        return DestMaps.TryGetValue(pawn, out var map) ? map : null;
      }
      set
      {
        if (pawn is null) return;
        if (value is null)
        {
          pawn.RemoveDestMap();
          return;
        }

        DestMaps.AddOrUpdate(pawn, value);
      }
    }

    public void RemoveDestMap()
    {
      if (pawn is null) return;
      DestMaps.Remove(pawn);
    }

    public Map DepartMap
    {
      get
      {
        if (pawn is null) return null;
        return DepartMaps.TryGetValue(pawn, out var map) ? map : null;
      }
      set
      {
        if (pawn is null) return;
        if (value is null)
        {
          pawn.RemoveDepartMap();
          return;
        }

        DepartMaps.AddOrUpdate(pawn, value);
      }
    }

    public void RemoveDepartMap()
    {
      if (pawn is null) return;
      DepartMaps.Remove(pawn);
    }

    public Map DepartMapOrPawnMap => pawn.DepartMap ?? pawn.Map;

    public Map DepartMapOrPawnMapHeld => pawn.DepartMap ?? pawn.MapHeld;

    internal IntVec3? DepartPosition
    {
      get => pawn is null ? null : DepartPositions.GetValueOrDefault(pawn);
      set
      {
        if (pawn is null) return;
        if (value is null)
        {
          DepartPositions.Remove(pawn);
          return;
        }

        DepartPositions[pawn] = value;
      }
    }

    public bool CanReach(LocalTargetInfo dest3, PathEndMode peMode, Danger maxDanger, bool canBashDoors,
      bool canBashFences, TraverseMode mode, Map destMap)
    {
      var traverseParms = TraverseParms.For(pawn, maxDanger: maxDanger, mode: mode, canBashDoors: canBashDoors,
        canBashFences: canBashFences);
      return pawn.Spawned && CanReach(pawn.DepartMap ?? pawn.Map, pawn.DepartPosition ?? pawn.Position, dest3, peMode,
        traverseParms, destMap, out _, out _, out _);
    }

    public bool CanReach(LocalTargetInfo dest3, PathEndMode peMode, Danger maxDanger, bool canBashDoors,
      bool canBashFences, TraverseMode mode, Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot,
      out List<TraverseSpots> spotsQueue)
    {
      var traverseParms = TraverseParms.For(pawn, maxDanger: maxDanger, mode: mode, canBashDoors: canBashDoors,
        canBashFences: canBashFences);
      exitSpot = TargetInfo.Invalid;
      enterSpot = TargetInfo.Invalid;
      spotsQueue = null;
      return pawn.Spawned && CanReach(pawn.DepartMap ?? pawn.Map, pawn.DepartPosition ?? pawn.Position, dest3, peMode,
        traverseParms, destMap, out exitSpot, out enterSpot, out spotsQueue);
    }
  }

  public static IntVec3 EnterVehiclePosition(TargetInfo enterSpot, VehiclePawn enterer = null)
  {
    if (!enterSpot.Map.IsVehicleMapOf(out var vehicle) || vehicle is not { Spawned: true })
    {
      return IntVec3.Invalid;
    }

    var cell = enterSpot.Cell.ToBaseMapCoord(vehicle);
    var faceCell = enterSpot.HasThing
      ? enterSpot.Thing.BaseFullRotation().FacingCell
      : enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle).FacingCell;

    var dist = 0;
    IntVec3 cell2;
    var cellRect = vehicle.VehicleRect();
    do
    {
      dist++;
      cell2 = cell - faceCell * dist;
      if (!cell2.InBounds(vehicle.Map))
      {
        return IntVec3.Invalid;
      }
    } while (cellRect.Contains(cell2));

    if (enterSpot.Thing is Building_VehicleRamp && dist < 2) dist++;

    if (enterer != null)
      dist += enterer.HalfLength();
    var result = cell - faceCell * dist;
    return result;
  }

  public static bool CanReach(Map departMap, IntVec3 root, LocalTargetInfo dest, PathEndMode peMode,
    TraverseParms traverseParms, Map destMap, bool canUseAbility = true)
  {
    return CanReach(departMap, root, dest, peMode, traverseParms, destMap, out _, out _, out _, canUseAbility);
  }

  public static bool CanReach(Map departMap, IntVec3 root, LocalTargetInfo dest, PathEndMode peMode,
    TraverseParms traverseParms, Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot,
    out List<TraverseSpots> spotsQueue, bool canUseAbility = true)
  {
    exitSpot = TargetInfo.Invalid;
    enterSpot = TargetInfo.Invalid;
    spotsQueue = null;

    if (departMap == null || destMap == null) return false;
    if (departMap == destMap)
    {
      try
      {
        working = true;
        return destMap.reachability.CanReach(root, dest, peMode, traverseParms);
      }
      finally
      {
        working = false;
      }
    }

    if (traverseParms.pawn is VehiclePawn vehiclePawn)
    {
      return vehiclePawn.CanReachVehicle(dest, peMode, traverseParms.maxDanger, traverseParms.mode, destMap,
        out exitSpot, out enterSpot);
    }

    if (working)
    {
      Log.ErrorOnce("Called CanReach() while working. This should never happen. Suppressing further errors.", 7312233);
      return false;
    }

    var region = root.GetRegion(departMap);
    TraverseParmsExtended parmsForCache = traverseParms;
    Ability_MapTraverse ability = null;
    if (canUseAbility)
    {
      ability = traverseParms.pawn?.abilities?.AllAbilitiesForReading.OfType<Ability_MapTraverse>()
        .FirstOrDefault(a => a is { CanCast.Accepted: true });
      parmsForCache.ability = ability?.def;
    }

    dest = (LocalTargetInfo)GenPath.ResolvePathMode(traverseParms.pawn, dest.ToTargetInfo(destMap), ref peMode);
    destRegions.Clear();
    switch (peMode)
    {
      case PathEndMode.OnCell:
      {
        if (dest.Cell.GetRegion(destMap) is { } region2 && region2.Allows(traverseParms, true))
          destRegions.Add(region2);
        break;
      }
      case PathEndMode.Touch:
        TouchPathEndModeUtility.AddAllowedAdjacentRegions(dest, traverseParms, destMap, destRegions);
        break;
      case PathEndMode.None:
      case PathEndMode.ClosestTouch:
      case PathEndMode.InteractionCell:
      default: break;
    }

    destRegions.RemoveDuplicates();
    if (destRegions.Count == 0 && traverseParms.mode != TraverseMode.PassAllDestroyableThings &&
        traverseParms.mode != TraverseMode.PassAllDestroyablePlayerOwnedThings &&
        traverseParms.mode != TraverseMode.PassAllDestroyableThingsNotWater)
    {
      return false;
    }

    var result = false;
    foreach (var region2 in destRegions)
    {
      if (CrossMapReachabilityCache.TryGetCache(region, region2, parmsForCache, out result, out exitSpot, out enterSpot,
            out spotsQueue))
      {
        DebugLog(
          $"Result from cache: {root}, {departMap}, {dest}, {destMap}, {traverseParms}: {result}, {exitSpot}, {enterSpot}");
        return result;
      }
    }

    try
    {
      working = true;
      if (MultiFloors.Active && (MultiFloors.GetLevel(departMap) != MultiFloors.GetLevel(destMap)))
      {
        return false;
      }

      var destBaseMap = destMap.IsVehicleMapOf(out var vehicle) && vehicle.Spawned ? vehicle.Map : destMap;
      var departBaseMap = departMap.IsVehicleMapOf(out var vehicle2) && vehicle2.Spawned ? vehicle2.Map : departMap;

      if (departMap.BaseMapOrCaravan == destMap.BaseMapOrCaravan)
      {
        if (!VehicleMapFramework.settings.legacyCanReach)
        {
          if (!root.InBounds(departMap))
          {
            VMF_Log.Error($"Root {root} is out of bounds of departMap {departMap}. This should never happen.");
            return false;
          }
          var start = new MapTraverse(TargetInfo.Invalid, new TargetInfo(root, departMap));
          var destination = new MapTraverse(TargetInfo.Invalid, dest.ToTargetInfo(destMap));
          traverser.SetParameters(start.enterSpot, destination.enterSpot, traverseParms, ability);
          traverseList.Clear();
          aStar.Run(start, destination, traverseList);
          result = traverseList.Count > 0;
          if (traverseList.Count == 1)
          {
            exitSpot = traverseList[0].exitSpot;
            enterSpot = traverseList[0].enterSpot;
          }
          else if (result)
          {
            spotsQueue = SimplePool<List<TraverseSpots>>.Get();
            spotsQueue.Clear();
            foreach (var traverse in traverseList)
            {
              spotsQueue.Add(new TraverseSpots(traverse.exitSpot, traverse.enterSpot));
            }
          }
          
          traverseList.Clear();
          return result;
        }

        using var profiler = new DeepProfilerScope("CrossMapReachability Legacy Run", aStar.debug);

        var flag = departMap == departBaseMap;
        var flag2 = destBaseMap == destMap;
        var traverseParms2 = traverseParms.pawn is not null
          ? TraverseParms.For(traverseParms.pawn, traverseParms.maxDanger, TraverseMode.PassDoors,
            traverseParms.canBashDoors, traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences,
            traverseParms.avoidPersistentDanger)
          : TraverseParms.For(TraverseMode.PassDoors, traverseParms.maxDanger, traverseParms.canBashDoors,
            traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences, traverseParms.avoidPersistentDanger);
        traverser.destRegion = dest.Cell.GetRegion(destMap);

        bool CanReachLocal(IntVec3 cell, IntVec3 cell2)
        {
          return departMap.reachability.CanReach(root, cell, PathEndMode.OnCell, traverseParms) &&
                 destMap.reachability.CanReach(cell2, dest, peMode, traverseParms);
        }

        switch (flag)
        {
          //出発地が車上マップで目的地がベースマップ
          case false when flag2:
          {
            if (vehicle2 != null)
            {
              if (!vehicle2.AllowExitFor(traverseParms.pawn))
              {
                return false;
              }

              foreach (var comp in vehicle2.GetSortedEnterComps(dest.Cell, CompVehicleEnterSpot.Kind.GroundAccessOnly))
              {
                if (comp is not { AvailableAccessSpot: { IsValid: true } accessSpot } ||
                    accessSpot.Map != destMap)
                  continue;
                
                var cell = accessSpot.Cell;

                result = CellCheck(cell, destMap, traverseParms, true) && CanReachLocal(comp.parent.Position, cell);
                DebugLog($"VehicleMap => BaseMap: {root}, {cell}, {comp}, {traverseParms} :{result} {comp.parent}");
                if (result)
                {
                  exitSpot = comp.parent;
                  return result;
                }
              }

              foreach (var c in vehicle2.CachedWalkableMapEdgeCells.Keys.OrderBy(c =>
                         (c.ToBaseMapCoord(vehicle2) - dest.Cell).LengthHorizontalSquared))
              {
                var targetInfo = new TargetInfo(c, departMap);
                var cell = EnterVehiclePosition(targetInfo);
                result = CellCheck(cell, destMap, traverseParms, true) && CanReachLocal(c, cell);
                DebugLog($"VehicleMap => BaseMap: {root}, {cell}, {c}, {traverseParms} :{result} {targetInfo}");
                if (result)
                {
                  exitSpot = targetInfo;
                  return result;
                }
              }

              result = ability is not null &&
                       ability.TryFindCastPosition(dest.ToTargetInfo(destMap), out exitSpot, out enterSpot);
              return result;
            }

            break;
          }
          //出発地がベースマップで目的地が車上マップ
          case true when !flag2:
          {
            if (vehicle != null)
            {
              if (!vehicle.AllowEnterFor(traverseParms.pawn))
              {
                return false;
              }

              foreach (var comp in vehicle.GetSortedEnterComps(root))
              {
                if (comp is not { AvailableAccessSpot: { IsValid: true } accessSpot } ||
                    accessSpot.Map != departMap)
                  continue;
                
                var cell = accessSpot.Cell;
                result = CellCheck(cell, departMap, traverseParms) && CanReachLocal(cell, comp.parent.Position);
                DebugLog($"BaseMap => VehicleMap: {root}, {cell}, {comp}, {traverseParms} :{result}");
                if (result)
                {
                  enterSpot = comp.parent;
                  return result;
                }
              }

              foreach (var c in vehicle.CachedWalkableMapEdgeCells.Keys.OrderBy(c =>
                         (root - c.ToBaseMapCoord(vehicle)).LengthHorizontalSquared))
              {
                var targetInfo = new TargetInfo(c, destMap);
                var cell = EnterVehiclePosition(targetInfo);
                result = CellCheck(cell, departMap, traverseParms) && CanReachLocal(cell, c);
                DebugLog(
                  $"BaseMap => VehicleMap: {new TargetInfo(root, departMap)}, {cell}, {c}, {dest.ToTargetInfo(destMap)}, {traverseParms} :{result}");
                if (result)
                {
                  enterSpot = targetInfo;
                  return result;
                }
              }

              result = ability is not null &&
                       ability.TryFindCastPosition(dest.ToTargetInfo(destMap), out exitSpot, out enterSpot);
              return result;
            }

            break;
          }
          //出発地と目的地がそれぞれ別の車上マップ
          default:
          {
            if (vehicle2 is null || !vehicle2.AllowExitFor(traverseParms.pawn) ||
                vehicle is null || !vehicle.AllowEnterFor(traverseParms.pawn))
              return false;

            result = CanReachBasic(out exitSpot, out enterSpot) ||
                     CanReachRecursive(out spotsQueue);

            return result;

            bool CanReachBasic(out TargetInfo exitSpot, out TargetInfo enterSpot)
            {
              exitSpot = TargetInfo.Invalid;
              enterSpot = TargetInfo.Invalid;

              var destBaseMapCoord = dest.Cell.ToBaseMapCoord(vehicle);
              foreach (var comp in vehicle2.GetSortedEnterComps(destBaseMapCoord))
              {
                if (comp is not { AvailableAccessSpot: { IsValid: true } accessSpot } ||
                    accessSpot.Map != destMap && accessSpot.Map != departBaseMap)
                  continue;
                
                var cell = accessSpot.Cell;
                //departMapからdestMapまで直通のジップラインがある場合
                if (accessSpot.Map == destMap)
                {
                  var c = comp.parent.Position;
                  if (CellCheck(cell, destMap, traverseParms, true) && CanReachLocal(c, cell))
                  {
                    exitSpot = comp.parent;
                    return true;
                  }
                }

                foreach (var comp2 in vehicle.GetSortedEnterComps(cell))
                {
                  if (comp2 is not { AvailableAccessSpot: { IsValid: true } accessSpot2 } ||
                      accessSpot2.Map != departBaseMap)
                    continue;

                  var cell2 = accessSpot2.Cell;
                  if (CanReach2(comp.parent.Position, cell, cell2, comp2.parent.Position))
                  {
                    exitSpot = comp.parent;
                    enterSpot = comp2.parent;
                    return true;
                  }
                }

                foreach (var c2 in vehicle.CachedWalkableMapEdgeCells.Keys.OrderBy(c2 =>
                           (cell - c2.ToBaseMapCoord(vehicle)).LengthHorizontalSquared))
                {
                  var targetInfo = new TargetInfo(c2, destMap);
                  var cell2 = EnterVehiclePosition(targetInfo);
                  if (CanReach2(comp.parent.Position, cell, cell2, c2))
                  {
                    exitSpot = comp.parent;
                    enterSpot = targetInfo;
                    return true;
                  }
                }
              }

              foreach (var c in vehicle2.CachedWalkableMapEdgeCells.Keys.OrderBy(c =>
                         (c.ToBaseMapCoord(vehicle2) - destBaseMapCoord).LengthHorizontalSquared))
              {
                var targetInfo = new TargetInfo(c, departMap);
                var cell = EnterVehiclePosition(targetInfo);

                foreach (var comp2 in vehicle.GetSortedEnterComps(cell))
                {
                  if (comp2 is not { AvailableAccessSpot: { IsValid: true } accessSpot2 } ||
                      accessSpot2.Map != departBaseMap)
                    continue;
                  
                  var cell2 = accessSpot2.Cell;
                  if (CanReach2(c, cell, cell2, comp2.parent.Position))
                  {
                    exitSpot = targetInfo;
                    enterSpot = comp2.parent;
                    return true;
                  }
                }

                foreach (var c2 in vehicle.CachedWalkableMapEdgeCells.Keys.OrderBy(c2 =>
                           (cell - c2.ToBaseMapCoord(vehicle)).LengthHorizontalSquared))
                {
                  var targetInfo2 = new TargetInfo(c2, destMap);
                  var cell2 = EnterVehiclePosition(targetInfo2);
                  if (CanReach2(c, cell, cell2, c2))
                  {
                    exitSpot = targetInfo;
                    enterSpot = targetInfo2;
                    return true;
                  }
                }
              }

              return ability is not null &&
                     ability.TryFindCastPosition(dest.ToTargetInfo(destMap), out exitSpot, out enterSpot);

              bool CanReach2(IntVec3 cell, IntVec3 cell2, IntVec3 cell3, IntVec3 cell4)
              {
                return CellCheck(cell2, departBaseMap, traverseParms, true) &&
                       CellCheck(cell3, departBaseMap, traverseParms) &&
                       departMap.reachability.CanReach(root, cell, PathEndMode.OnCell,
                         traverseParms) &&
                       departBaseMap.reachability.CanReach(cell2, cell3, PathEndMode.OnCell,
                         traverseParms2) &&
                       destMap.reachability.CanReach(cell4, dest, peMode, traverseParms2);
              }
            }

            bool CanReachRecursive(out List<TraverseSpots> spotsQueue)
            {
              spotsQueue = null;
              var destBaseMapCoord = dest.Cell.ToBaseMapCoord(vehicle);
              candidateMaps.AddRange(departMap.BaseMapAndVehicleMaps(false));
              result = EnterMap(vehicle2.VehicleMap, root);
              if (result)
              {
                spotsQueue = SimplePool<List<TraverseSpots>>.Get();
                spotsQueue.Clear();
                foreach (var target in tmpTargets)
                {
                  spotsQueue.Add(target);
                }
              }

              tmpTargets.Clear();
              visitedMaps.Clear();
              candidateMaps.Clear();
              return result;

              bool EnterMap(Map map, IntVec3 start)
              {
                // 目的のマップ
                if (map == destMap &&
                    destMap.reachability.CanReach(start, dest, PathEndMode.OnCell, traverseParms2))
                {
                  return true;
                }

                visitedMaps.Add(map);
                var comps = map.IsVehicleMapOf(out var vehicle3)
                  ? vehicle3.GetSortedEnterComps(destBaseMapCoord,
                    CompVehicleEnterSpot.Kind.DirectAccessOnly).AsEnumerable()
                  : RegionTraverserAcrossMaps.EnterSpotDefs.SelectMany(def =>
                    map.listerThings.ThingsOfDef(def).Select(t => t.TryGetComp<CompVehicleEnterSpot>()));
                foreach (var comp in comps)
                {
                  if (comp is not { AvailableAccessSpot: { IsValid: true } accessSpot } ||
                      visitedMaps.Contains(accessSpot.Map)) continue;

                  var c = comp.parent.Position;
                  var c2 = accessSpot.Cell;
                  var map2 = accessSpot.Map;
                  if (CellCheck(c, map, traverseParms) && CellCheck(c2, map2, traverseParms, true) &&
                      map.reachability.CanReach(start, c, PathEndMode.OnCell, traverseParms2))
                  {
                    tmpTargets.Push(new TraverseSpots(comp.parent, TargetInfo.Invalid));
                    if (!EnterMap(map2, c2))
                    {
                      tmpTargets.Pop();
                      return false;
                    }

                    return true;
                  }
                }

                // GrapplingHookアビリティによる探索
                if (ability is not null)
                {
                  foreach (var targetMap in candidateMaps)
                  {
                    if (!visitedMaps.Contains(targetMap) &&
                        ability.TryFindCastPositionFromTo(
                          new TargetInfo(start, map), new TargetInfo(targetMap.Center, targetMap),
                          out var castSpot, out var targSpot))
                    {
                      tmpTargets.Push(new TraverseSpots(castSpot, targSpot));
                      if (!EnterMap(targetMap, targSpot.Cell))
                      {
                        tmpTargets.Pop();
                        return false;
                      }

                      return true;
                    }
                  }
                }

                return false;
              }
            }
          }
        }
      }

      result = false;
      return result;
    }
    finally
    {
      if (result)
        CrossMapReachabilityCache.Cache(region, traverser.destRegion, parmsForCache, true, exitSpot, enterSpot,
          spotsQueue);
      else
      {
        foreach (var region2 in destRegions)
        {
          CrossMapReachabilityCache.Cache(region, region2, parmsForCache, false, TargetInfo.Invalid, TargetInfo.Invalid,
            null);
        }
      }

      working = false;
    }
  }


  private static bool CellCheck(IntVec3 cell, Map map, TraverseParms parms, bool destination = false)
  {
    if (parms.pawn is { } pawn)
    {
      if (!cell.WalkableBy(map, pawn) ||
          cell.GetDoor(map) is { HoldOpen: false } door &&
           (!door.PawnCanOpen(pawn) || door.IsForbidden(pawn)))
        return false;
      
      if (!destination || !parms.avoidPersistentDanger)
        return true;

      var terrain = cell.GetTerrain(map);
      if (terrain is { dangerous: false })
        return true;

      return CompAllowDangerTerrains.AllowedTerrains.TryGetValue(pawn, out var allowedTerrains) &&
             allowedTerrains.Contains(terrain);
    }
    
    return cell.Walkable(map) &&
           (cell.GetDoor(map) is not { } door2 || door2.HoldOpen) &&
           (!destination || !parms.avoidPersistentDanger || cell.GetTerrain(map) is { dangerous: false });
  }

  public static bool CanReachToMap(IntVec3 root, Map departMap, TraverseParms parms, Map destMap, bool canUseAbility = true)
  {
    return CanReachToMap(root, departMap, parms, destMap, out _, out _, out _, canUseAbility);
  }
  
  public static bool CanReachToMap(IntVec3 root, Map departMap, TraverseParms parms, Map destMap,
    out TargetInfo exitSpot, out TargetInfo enterSpot, out List<TraverseSpots> spotsQueue, bool canUseAbility = true)
  {
    exitSpot = TargetInfo.Invalid;
    enterSpot = TargetInfo.Invalid;
    spotsQueue = null;
    
    

    if (departMap == null || destMap == null) return false;
    if (departMap == destMap) return true;
    if (departMap.BaseMapOrCaravan != destMap.BaseMapOrCaravan) return false;

    if (working)
    {
      Log.ErrorOnce("Called CanReachToMap() while working. This should never happen. Suppressing further errors.", 7312234);
      return false;
    }
    
    working = true;
    try
    {
      var region = root.GetRegion(departMap);
      TraverseParmsExtended parmsForCache = parms;
      Ability_MapTraverse ability = null;
      if (canUseAbility)
      {
        ability = parms.pawn?.abilities?.AllAbilitiesForReading.OfType<Ability_MapTraverse>()
          .FirstOrDefault(a => a is { CanCast.Accepted: true });
        parmsForCache.ability = ability?.def;
      }

      destRegions.Clear();
      foreach (var district in destMap.regionGrid.allDistricts)
      {
        if (district.Passable)
          destRegions.AddRange(district.Regions);
      }

      if (destRegions.Empty()) return false;

      bool result;
      foreach (var region2 in destRegions)
      {
        if (CrossMapReachabilityCache.TryGetCache(region, region2, parmsForCache, out result, out exitSpot,
              out enterSpot,
              out spotsQueue) && result)
          return true;
      }

      var start = new MapTraverse(TargetInfo.Invalid, new TargetInfo(root, departMap));
      var destination = new MapTraverse(TargetInfo.Invalid, new TargetInfo(destRegions[0].AnyCell, destMap));
      traverser.SetParameters(start.enterSpot, destination.enterSpot, parms, ability);
      traverseList.Clear();
      aStar.Run(start, destination, traverseList);
      result = traverseList.Count > 0;
      if (traverseList.Count == 1)
      {
        exitSpot = traverseList[0].exitSpot;
        enterSpot = traverseList[0].enterSpot;
      }
      else if (result)
      {
        spotsQueue = SimplePool<List<TraverseSpots>>.Get();
        spotsQueue.Clear();
        foreach (var traverse in traverseList)
        {
          spotsQueue.Add(new TraverseSpots(traverse.exitSpot, traverse.enterSpot));
        }
      }
      if (result)
        CrossMapReachabilityCache.Cache(region, traverser.destRegion, parmsForCache, true, exitSpot, enterSpot, spotsQueue);
      traverseList.Clear();
      return result;
    }
    finally
    {
      working = false;
    }
  }

  public static bool TryFindNearestStandableCell(VehiclePawn vehicle, IntVec3 cell, Map map, out IntVec3 result,
    float radius = -1f)
  {
    if (radius < 0f)
    {
      radius = Mathf.Min(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z) * 2;
    }

    var num = GenRadial.NumCellsInRadius(radius);
    result = IntVec3.Invalid;
    for (var i = 0; i < num; i++)
    {
      var intVec = GenRadial.RadialPattern[i] + cell;
      if (intVec.InBounds(map) && intVec.Standable(vehicle, map) && vehicle.DrivableRectOnCell(intVec, true, map))
      {
        if (map == vehicle.Map && intVec == vehicle.Position || vehicle.beached ||
            AnyVehicleBlockingPathAt(intVec, vehicle, map) == null && vehicle.CanReachVehicle(intVec,
              PathEndMode.OnCell, Danger.Deadly, TraverseMode.ByPawn, map, out _, out _))
        {
          result = intVec;
          return true;
        }
      }
    }

    return false;
  }

  public static VehiclePawn AnyVehicleBlockingPathAt(IntVec3 cell, VehiclePawn vehicle, Map map)
  {
    var thingList = cell.GetThingList(map);
    if (thingList.NullOrEmpty()) return null;

    var euclideanDistance = Ext_Map.Distance(vehicle.PositionOnBaseMap, cell.ToBaseMapCoord(map));
    foreach (var t in thingList)
    {
      if (t is VehiclePawn otherVehicle && otherVehicle != vehicle)
      {
        if (euclideanDistance < 20 || !otherVehicle.vehiclePather.Moving)
        {
          return otherVehicle;
        }
      }
    }

    return null;
  }

  extension(VehiclePawn vehicle)
  {
    public bool DrivableRectOnCell(IntVec3 cell, bool maxPossibleSize, Map map)
    {
      if (maxPossibleSize)
      {
        return vehicle.VehicleRect(cell, Rot8.North).All(rectCell => vehicle.Drivable(rectCell, map)) &&
               vehicle.VehicleRect(cell, Rot8.East).All(rectCell => vehicle.Drivable(rectCell, map));
      }

      return vehicle.MinRect(cell).Cells.All(c => vehicle.Drivable(c, map));
    }

    public bool Drivable(IntVec3 cell, Map map)
    {
      return cell.InBounds(map) && vehicle.DrivableFast(cell, map);
    }

    public bool DrivableFast(int index, Map map)
    {
      var cell = vehicle.Map.cellIndices.IndexToCell(index);
      return vehicle.DrivableFast(cell, map);
    }

    public bool DrivableFast(int x, int z, Map map)
    {
      IntVec3 cell = new(x, 0, z);
      return vehicle.DrivableFast(cell, map);
    }

    public bool DrivableFast(IntVec3 cell, Map map)
    {
      var vehiclePawn = map.GetDetachedMapComponent<VehiclePositionManager>().ClaimedBy(cell);
      if (vehiclePawn == null || vehiclePawn == vehicle)
      {
        return map.GetCachedMapComponent<VehiclePathingSystem>()[vehicle.VehicleDef].VehiclePathGrid.WalkableFast(cell);
      }

      return false;
    }

    public bool CanReachVehicle(LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, TraverseMode mode,
      Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot)
    {
      exitSpot = TargetInfo.Invalid;
      enterSpot = TargetInfo.Invalid;

      var traverseParms = TraverseParms.For(vehicle, maxDanger, mode);

      if (dest.Cell == vehicle.Position && destMap == vehicle.Map)
      {
        return true;
      }

      if (!vehicle.Spawned) return false;

      var departMap = vehicle.Map;
      if (departMap == null || destMap == null) return false;
      if (departMap == destMap)
      {
        return MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef]
          .VehicleReachability.CanReachVehicle(vehicle.Position, dest, peMode, traverseParms);
      }

      if (MultiFloors.Active && (MultiFloors.GetLevel(departMap) != MultiFloors.GetLevel(destMap)))
      {
        return false;
      }

      if (vehicle is VehiclePawnWithMap)
      {
        return false;
      }

      var destBaseMap = destMap.IsVehicleMapOf(out var vehicle2) && vehicle2.Spawned ? vehicle2.Map : destMap;
      var departBaseMap = departMap.IsVehicleMapOf(out var vehicle3) && vehicle3.Spawned ? vehicle3.Map : departMap;

      //行き先のマップでまだPathGridが作られてない場合構築をリクエストする処理を追加
      var destMapPathing = MapComponentCache<VehiclePathingSystem>.GetComponent(destMap);
      if (!destMapPathing[vehicle.VehicleDef].VehiclePathGrid.Enabled)
      {
        destMapPathing.RequestGridsFor(vehicle.VehicleDef, DeferredGridGeneration.Urgency.Urgent);
      }

      if (departBaseMap == destBaseMap)
      {
        var flag = departMap == departBaseMap;
        var flag2 = departBaseMap == destMap;

        bool AvailableEnterSpot(CompVehicleEnterSpot comp)
        {
          return comp != null && comp.parent.def.size.x >= vehicle.VehicleDef.size.x;
        }

        bool result;
        switch (flag)
        {
          //vehicleが車上マップに居て目的地がベースマップ
          case false when flag2 && vehicle3 != null:
          {
            Thing tmpThing = null;
            result = vehicle3.GetSortedEnterComps(dest.Cell, CompVehicleEnterSpot.Kind.RampOnly).Any(e =>
            {
              tmpThing = e.parent;
              if (!AvailableEnterSpot(e) || tmpThing.OccupiedRect().Any(c3 => !vehicle.Drivable(c3, departMap)))
                return false;

              var cell = tmpThing.Position + (tmpThing.Rotation.FacingCell * vehicle.HalfLength());
              var cell2 = EnterVehiclePosition(tmpThing, vehicle);
              return MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef]
                       .VehicleReachability
                       .CanReachVehicle(vehicle.Position, cell, PathEndMode.OnCell, traverseParms) &&
                     destMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell2, dest, peMode,
                       TraverseMode.PassDoors, traverseParms.maxDanger);
            });
            exitSpot = result ? tmpThing : TargetInfo.Invalid;
            return result;
          }
          //vehicleがベースマップに居て目的地が車上マップ
          case true when !flag2 && vehicle2 != null:
          {
            Thing tmpThing = null;
            result = vehicle2.GetSortedEnterComps(vehicle.Position, CompVehicleEnterSpot.Kind.RampOnly).Any(e =>
            {
              tmpThing = e.parent;
              if (!AvailableEnterSpot(e) || tmpThing.OccupiedRect().Any(c3 => !vehicle.Drivable(c3, destMap)))
                return false;

              var cell = EnterVehiclePosition(tmpThing, vehicle);
              var cell2 = tmpThing.Position + (tmpThing.Rotation.FacingCell * vehicle.HalfLength());
              return MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef]
                       .VehicleReachability
                       .CanReachVehicle(vehicle.Position, cell, PathEndMode.OnCell, traverseParms) &&
                     destMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell2, dest, peMode,
                       TraverseMode.PassDoors, traverseParms.maxDanger);
            });
            enterSpot = result ? tmpThing : TargetInfo.Invalid;
            return result;
          }
          //vehicleと目的地がそれぞれ別の車上マップ
          default:
          {
            if (vehicle3 != null)
            {
              if (vehicle2 != null)
              {
                //行き先のベースマップでまだPathGridが作られてない場合構築をリクエストする処理を追加
                var departBaseMapPathing = MapComponentCache<VehiclePathingSystem>.GetComponent(departBaseMap);
                if (!departBaseMapPathing[vehicle.VehicleDef].VehiclePathGrid.Enabled)
                {
                  departBaseMapPathing.RequestGridsFor(vehicle.VehicleDef, DeferredGridGeneration.Urgency.Urgent);
                }

                Thing tmpThing = null;
                Thing tmpThing2 = null;
                result = vehicle3.GetSortedEnterComps(dest.Cell.ToBaseMapCoord(vehicle2),
                  CompVehicleEnterSpot.Kind.RampOnly).Any(e =>
                {
                  tmpThing = e.parent;
                  if (!AvailableEnterSpot(e) || tmpThing.OccupiedRect().Any(c => !vehicle.Drivable(c, departMap)))
                    return false;

                  var cell = EnterVehiclePosition(tmpThing, vehicle);
                  var cell2 = tmpThing.Position + (tmpThing.Rotation.FacingCell * vehicle.HalfLength());

                  return vehicle2.GetSortedEnterComps(cell, CompVehicleEnterSpot.Kind.RampOnly).Any(e2 =>
                  {
                    tmpThing2 = e2.parent;
                    if (!AvailableEnterSpot(e2) || tmpThing2.OccupiedRect().Any(c => !vehicle.Drivable(c, destMap)))
                      return false;

                    var cell3 = EnterVehiclePosition(tmpThing2, vehicle);
                    var cell4 = tmpThing2.Position + (tmpThing2.Rotation.FacingCell * vehicle.HalfLength());
                    return MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef]
                             .VehicleReachability
                             .CanReachVehicle(vehicle.Position, cell2, PathEndMode.OnCell, traverseParms) &&
                           departBaseMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell, cell3,
                             PathEndMode.OnCell, TraverseMode.PassDoors, traverseParms.maxDanger) &&
                           destMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell4, dest, peMode,
                             TraverseMode.PassDoors, traverseParms.maxDanger);
                  });
                });
                exitSpot = result ? tmpThing : TargetInfo.Invalid;
                enterSpot = result ? tmpThing2 : TargetInfo.Invalid;
                return result;
              }
            }

            break;
          }
        }
      }

      return false;
    }
  }

  private struct MapTraverse(TargetInfo exitSpot, TargetInfo enterSpot, bool canMerge = true) : IEquatable<MapTraverse>
  {
    public TargetInfo exitSpot = exitSpot;
    public readonly TargetInfo enterSpot = enterSpot;
    public bool canMerge = canMerge;

    public int DistrictID { get; init; } = RegionAndRoomQuery.DistirctAtFast(enterSpot.Cell, enterSpot.Map)?.ID ?? -1;

    public bool Equals(MapTraverse other)
    {
      if (DistrictID != -1) return DistrictID == other.DistrictID;
      return enterSpot == other.enterSpot;
    }

    public override int GetHashCode()
    {
      return DistrictID != -1 ? Gen.HashCombineInt(DistrictID, 2821981) : enterSpot.GetHashCode();
    }

    public override string ToString()
    {
      return
        $"Exit: {exitSpot} {(exitSpot.Map.IsVehicleMapOf(out var vehicle) ? vehicle.VehicleMap.Parent.Label : null)}, " +
        $"Enter: {enterSpot} {(enterSpot.Map.IsVehicleMapOf(out var vehicle2) ? vehicle2.VehicleMap.Parent.Label : null)}";
    }
  }

  private class Traverser
  {
    private IntVec3 _destBaseMapCoord;
    private TraverseParms _traverseParms;
    private TraverseParms _traverseParms2;
    private Ability_MapTraverse _ability;
    private readonly List<Map> _tmpCandidates = [];
    private readonly HashSet<int> _visitedDistrictIDs = [];
    private int debugNodeNumber;
    public Region destRegion;

    public void SetParameters(TargetInfo start, TargetInfo destination, TraverseParms traverseParms,
      Ability_MapTraverse ability)
    {
      _destBaseMapCoord = destination.CellOnGroundMap;
      _traverseParms = traverseParms;
      _traverseParms2 = traverseParms.pawn != null
        ? TraverseParms.For(traverseParms.pawn, traverseParms.maxDanger, TraverseMode.PassDoors,
          traverseParms.canBashDoors, traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences,
          traverseParms.avoidPersistentDanger)
        : TraverseParms.For(TraverseMode.PassDoors, traverseParms.maxDanger, traverseParms.canBashDoors,
          traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences, traverseParms.avoidPersistentDanger);
      _ability = ability;
      _tmpCandidates.Clear();
      _tmpCandidates.AddRange(start.Map.BaseMapAndVehicleMaps());
      var destMap = destination.Map;
      _tmpCandidates.SortBy(m =>
      {
        if (m == destMap)
          return 0;
        if (m.IsVehicleMapOf(out var vehicle))
          return (m.Center.ToBaseMapCoord(vehicle) - _destBaseMapCoord).LengthManhattan;
        return m.Size.LengthManhattan / 2;
      });
      _visitedDistrictIDs.Clear();
      if (RegionAndRoomQuery.DistirctAtFast(start.Cell, start.Map) is { } district)
        _visitedDistrictIDs.Add(district.ID);
      destRegion = null;
      debugNodeNumber = 0;
    }
    
    public void SetParameters(TargetInfo start, Map destMap, TraverseParms traverseParms,
      Ability_MapTraverse ability)
    {
      _destBaseMapCoord = destMap.IsVehicleMapOf(out var vehicle)
        ? destMap.Center.ToBaseMapCoord(vehicle)
        : start.CellOnBaseMap();
      _traverseParms = traverseParms;
      _traverseParms2 = traverseParms.pawn != null
        ? TraverseParms.For(traverseParms.pawn, traverseParms.maxDanger, TraverseMode.PassDoors,
          traverseParms.canBashDoors, traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences,
          traverseParms.avoidPersistentDanger)
        : TraverseParms.For(TraverseMode.PassDoors, traverseParms.maxDanger, traverseParms.canBashDoors,
          traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences, traverseParms.avoidPersistentDanger);
      _ability = ability;
      _tmpCandidates.Clear();
      _tmpCandidates.AddRange(start.Map.BaseMapAndVehicleMaps());
      _tmpCandidates.SortBy(m =>
      {
        if (m == destMap)
          return 0;
        if (m.IsVehicleMapOf(out var vehicle2))
          return (m.Center.ToBaseMapCoord(vehicle2) - _destBaseMapCoord).LengthManhattan;
        return m.Size.LengthManhattan / 2;
      });
      _visitedDistrictIDs.Clear();
      if (RegionAndRoomQuery.DistirctAtFast(start.Cell, start.Map) is { } district)
        _visitedDistrictIDs.Add(district.ID);
      destRegion = null;
      debugNodeNumber = 0;
    }

    public IEnumerable<MapTraverse> Neighbors(MapTraverse current)
    {
      var start = current.enterSpot.Cell;
      var map = current.enterSpot.Map;
      if (map.IsVehicleMapOf(out var vehicle))
      {
        using var profiler = new DeepProfilerScope("Vehicle Neighbors");
        if (!vehicle.AllowExitFor(_traverseParms.pawn))
          yield break;
        
        var spawned = vehicle.Spawned;
        foreach (var comp in vehicle.GetSortedEnterComps(_destBaseMapCoord.ToVehicleMapCoord(vehicle)))
        {
          if (comp is { AvailableAccessSpot: { IsValid: true } accessSpot })
          {
            yield return new MapTraverse(comp.parent, accessSpot, !accessSpot.Map.IsVehicleMap);
          }
        }

        if (spawned)
        {
          // OrderByを避けて行き先に近いセルから返す
          var map2 = vehicle.Map;
          var startIndex =
            vehicle.CachedMapEdgeCells.IndexOf(_destBaseMapCoord.ClosestWalkableEdgeCell(vehicle, current.DistrictID));
          var count = vehicle.CachedMapEdgeCells.Count;
          for (var i = 0; i < count; i++)
          {
            var offset = (i % 2 == 0) ? (i / 2) : -(i / 2 + 1);
            var index = GenMath.PositiveMod(startIndex + offset, count);
            var cell = vehicle.CachedMapEdgeCells[index];
            if (vehicle.GetCachedEnterPosition(index) is { IsValid: true } cell2)
            {
              var traverse = new MapTraverse(new TargetInfo(cell, map), new TargetInfo(cell2, map2));
              yield return traverse;
              if (_visitedDistrictIDs.Contains(traverse.DistrictID))
                break; // 車両の周りにnullでない複数のDistrictがあるとは考えにくいため早期breakしていいはず
            }
          }
        }
      }
      else
      {
        using var profiler = new DeepProfilerScope("Ground Neighbors");
        for (var i = 0; i < _tmpCandidates.Count; i++)
        {
          var map2 = _tmpCandidates[i];
          if (map == map2) continue;

          if (map2.IsVehicleMapOf(out var vehicle2))
          {
            if (!vehicle2.AllowEnterFor(_traverseParms.pawn))
              continue;
            
            foreach (var comp in vehicle2.GetSortedEnterComps(start.ToVehicleMapCoord(vehicle2), CompVehicleEnterSpot.Kind.GroundAccessOnly))
            {
              if (comp is { AvailableAccessSpot: { IsValid: true } accessSpot } && accessSpot.Map == map)
              {
                yield return new MapTraverse(accessSpot, comp.parent);
              }
            }

            foreach (var district in vehicle2.CachedEdgeDistricts)
            {
              if (!ValidDistrict(district, _visitedDistrictIDs))
                continue;
              var startIndex =
                vehicle2.CachedMapEdgeCells.IndexOf(
                  current.enterSpot.Cell.ClosestWalkableEdgeCell(vehicle2, district.ID));
              var count = vehicle2.CachedMapEdgeCells.Count;
              for (var j = 0; j < count; j++)
              {
                var offset = j % 2 == 0 ? j / 2 : -j / 2 + 1;
                var index = GenMath.PositiveMod(startIndex + offset, count);
                if (vehicle2.GetCachedEnterPosition(index) is { IsValid: true } cell)
                {
                  var cell2 = vehicle2.CachedMapEdgeCells[index];
                  yield return new MapTraverse(new TargetInfo(cell, map), new TargetInfo(cell2, map2));
                  if (_visitedDistrictIDs.Contains(district.ID)) break;
                }
              }
            }
          }
        }
      }

      if (_ability is not null)
      {
        using var profiler = new DeepProfilerScope("Ability Neighbors");
        for (var i = 0; i < _tmpCandidates.Count; i++)
        {
          var map2 = _tmpCandidates[i];
          if (map == map2) continue;
          if (map2.IsVehicleMapOf(out var vehicle2))
          {
            if (!vehicle2.AllowEnterFor(_traverseParms.pawn))
              continue;
            
            foreach (var district in vehicle2.CachedEdgeDistricts)
            {
              if (!ValidDistrict(district, _visitedDistrictIDs))
                continue;
              var tmpCell = district.Regions[0].AnyCell;
              var targetInfo = new TargetInfo(tmpCell, map2);
              if (_ability.TryFindCastPositionFromTo(current.enterSpot, targetInfo, out var castSpot, out var targSpot,
                    district.ID))
              {
                yield return new MapTraverse(castSpot, targSpot, false);
              }
            }
          }
          // 地上マップのDistrict全走査は無駄が多すぎるためひとつのスポットのみの探索
          else if (_ability.TryFindCastPositionFromTo(current.enterSpot, new TargetInfo(_destBaseMapCoord, map2),
                     out var castSpot, out var targSpot))
          {
            yield return new MapTraverse(castSpot, targSpot, false);
          }
        }
      }

      yield break;

      static bool ValidDistrict(District district, HashSet<int> visited)
      {
        return district.RegionCount != 0 &&
               (district.RegionType & RegionType.Set_Passable) != RegionType.None &&
               !visited.Contains(district.ID);
      }
    }

    public bool CanEnter(MapTraverse from, MapTraverse to)
    {
      return CellCheck(to.exitSpot.Cell, to.exitSpot.Map, _traverseParms) &&
             CellCheck(to.enterSpot.Cell, to.enterSpot.Map, _traverseParms, true) &&
             from.enterSpot.Map.reachability.CanReach(from.enterSpot.Cell, to.exitSpot.Cell,
               PathEndMode.OnCell, _traverseParms2) &&
             _visitedDistrictIDs.Add(to.DistrictID);
    }

    public bool FinalCheck(MapTraverse from, MapTraverse to)
    {
      if (from.enterSpot.Map is null || from.enterSpot.Map != to.enterSpot.Map)
        return false;
      foreach (var region in destRegions)
      {
        if (from.enterSpot.Map.reachability.CanReach(from.enterSpot.Cell, region.AnyCell, PathEndMode.OnCell,
              _traverseParms2))
        {
          destRegion = region;
          return true;
        }
      }

      return false;
    }

    public static int Cost(MapTraverse from, MapTraverse to)
    {
      return (from.enterSpot.Cell - to.exitSpot.Cell).LengthManhattan + 1;
    }

    public int Heuristic(MapTraverse to)
    {
      return (to.enterSpot.CellOnGroundMap - _destBaseMapCoord).LengthManhattan;
    }

    public static void ProcessPath(List<MapTraverse> path, MapTraverse current)
    {
      if (current.canMerge)
      {
        if (path is [.., { canMerge: true } last])
        {
          last.exitSpot = current.exitSpot;
          last.canMerge = false;
          path[^1] = last;
          return;
        }

        if (!current.exitSpot.Map.IsVehicleMap)
        {
          current.exitSpot = TargetInfo.Invalid; // アビリティによる移動でない限り地上マップ側のTargetInfoは不要
        }
      }

      path.Add(current);
    }

    public void DebugDrawEnterNode(MapTraverse mapTraverse)
    {
      mapTraverse.exitSpot.Map.debugDrawer.FlashCell(mapTraverse.exitSpot.Cell, 0.25f, $"{debugNodeNumber}:exit");
      mapTraverse.enterSpot.Map.debugDrawer.FlashCell(mapTraverse.enterSpot.Cell, 0.5f, $"{debugNodeNumber}:enter");
      debugNodeNumber++;
    }
  }

  private class AStar<T>(
    Func<T, T, int> cost,
    Func<T, IEnumerable<T>> neighbors,
    Func<T, T, bool> finalCheck,
    Func<T, T, bool> canEnter = null,
    Func<T, int> heuristic = null,
    Action<List<T>, T> processPath = null,
    Action<T> debugAction = null) where T : IEquatable<T>
  {
    private readonly PriorityQueue<T, int> openQueue = new();

    private readonly Dictionary<T, Node> nodes = [];

    public bool debug;

    public void Run(T start, T destination, List<T> path)
    {
      using var profiler = new DeepProfilerScope("CrossMapReachability AStar Run", debug);
      nodes.Clear();
      openQueue.Clear();
      openQueue.Enqueue(start, 0);
      if (start.Equals(destination))
        return;

      while (openQueue.Count > 0 && openQueue.TryDequeue(out var current, out _))
      {
        using var profiler2 = new DeepProfilerScope("Neighbors");
        foreach (var neighbor in neighbors(current))
        {
          if (CreateNode(current, neighbor, out var node))
          {
            nodes[neighbor] = node;
            if (debug)
            {
              debugAction?.Invoke(neighbor);
            }

            openQueue.Enqueue(neighbor, node.cost + node.heuristic);
            if (finalCheck(neighbor, destination))
            {
              SolvePath(start, neighbor, path);
              return;
            }
          }
        }
      }

      path.Clear();
    }

    private bool CreateNode(T current, T neighbor, out Node node)
    {
      using var profiler = new DeepProfilerScope("CreateNode");
      if (nodes.TryGetValue(neighbor, out node) || canEnter?.Invoke(current, neighbor) is false)
      {
        return false;
      }

      node = new Node
      {
        parent = current,
        cost = cost(current, neighbor),
        heuristic = heuristic(neighbor)
      };
      return true;
    }

    private void SolvePath(T start, T destination, List<T> path)
    {
      var current = destination;
      while (!start.Equals(current))
      {
        (processPath ?? ((list, cur) => list.Add(cur)))(path, current);
        current = nodes[current].parent;
      }

      path.Reverse();
      if (path.Count == 0 || !path[^1].Equals(destination))
      {
        var stringBuilder = new StringBuilder($"A* failed to solve path from {start} to {destination}.");
        for (var i = 0; i < path.Count; i++)
        {
          stringBuilder.AppendLine($"  {i}: {path[i]}");
        }

        VMF_Log.Error(stringBuilder.ToString());
      }
    }

    private struct Node
    {
      public T parent;
      public int cost;
      public int heuristic;
    }
  }

  [DebugAction(VehicleMapFramework.CategoryName, "Flash Traverse Points", actionType = DebugActionType.ToolMapForPawns)]
  private static void FlashTraversePoints(Pawn p)
  {
    DebugTools.curTool = new DebugTool($"{p}: Destination...", () =>
    {
      var mousePos = UI.MouseMapPosition();
      IntVec3 dest;
      Map destMap;
      if (mousePos.TryGetVehicleMap(Find.CurrentMap, out var vehicle, VehicleMapFlag.None))
      {
        dest = UI.MouseCell().ToVehicleMapCoord(vehicle);
        destMap = vehicle.VehicleMap;
      }
      else
      {
        dest = UI.MouseCell();
        destMap = p.Map;
      }

      if (p.CanReach(dest, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn, destMap,
            out var exitSpot, out var enterSpot, out var spotsQueue))
      {
        var i = 0;
        if (!spotsQueue.NullOrEmpty())
        {
          foreach (var spots in spotsQueue)
          {
            FlashCell(spots.exitSpot, false, ref i);
            FlashCell(spots.enterSpot, true, ref i);
          }
        }

        FlashCell(exitSpot, false, ref i);
        FlashCell(enterSpot, true, ref i);
        return;
      }

      Messages.Message($"{p} can not reach to {new TargetInfo(dest, destMap)}", MessageTypeDefOf.RejectInput, false);
    });
    return;

    static void FlashCell(TargetInfo target, bool enterSpot, ref int index) =>
      target.Map?.debugDrawer.FlashCell(target.Cell, enterSpot ? 0.2f : 0.4f, $"{index++}");
  }

  [DebugAction(VehicleMapFramework.CategoryName, "Toggle A* Debug")]
  private static void ToggleDebugTraverser()
  {
    aStar.debug = !aStar.debug;
    Messages.Message($"{(aStar.debug ? "Enabled" : "Disabled")} the CrossMapReachability's A* debug draw/profile.",
      MessageTypeDefOf.TaskCompletion, false);
  }
}