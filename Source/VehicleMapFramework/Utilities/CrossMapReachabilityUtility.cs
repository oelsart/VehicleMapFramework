using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

    public static Map DestMapGlobal;
    
    private static ConditionalWeakTable<Pawn, Map> DepartMaps { get; } = [];

    public static Map DepartMapGlobal;
    
    private static readonly Stack<(TargetInfo, TargetInfo)> tmpTargets = new(32);
    
    private static readonly HashSet<Map> visitedMaps = new(32);
    
    private static readonly List<Map> candidateMaps = new(32);

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
        
        public bool CanReach(LocalTargetInfo dest3, PathEndMode peMode, Danger maxDanger, bool canBashDoors, bool canBashFences, TraverseMode mode, Map destMap)
        {
            var traverseParms = TraverseParms.For(pawn, maxDanger: maxDanger, mode: mode, canBashDoors: canBashDoors, canBashFences: canBashFences);
            return pawn.Spawned && CanReach(pawn.DepartMap ?? pawn.Map, traverseParms.pawn.Position, dest3, peMode, traverseParms, destMap, out _, out _, out _);
        }

        public bool CanReach(LocalTargetInfo dest3, PathEndMode peMode, Danger maxDanger, bool canBashDoors, bool canBashFences, TraverseMode mode, Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot, out List<(TargetInfo, TargetInfo)> spotsQueue)
        {
            var traverseParms = TraverseParms.For(pawn, maxDanger: maxDanger, mode: mode, canBashDoors: canBashDoors, canBashFences: canBashFences);
            exitSpot = TargetInfo.Invalid;
            enterSpot = TargetInfo.Invalid;
            spotsQueue = null;
            return pawn.Spawned && CanReach(pawn.DepartMap ?? pawn.Map, traverseParms.pawn.Position, dest3, peMode, traverseParms, destMap, out exitSpot, out enterSpot, out spotsQueue);
        }
    }

    public static IntVec3 EnterVehiclePosition(TargetInfo enterSpot, VehiclePawn enterer = null)
    {
        return EnterVehiclePosition(enterSpot, out _, enterer);
    }

    public static IntVec3 EnterVehiclePosition(TargetInfo enterSpot, out int dist, VehiclePawn enterer = null)
    {
        if (!enterSpot.Map.IsVehicleMapOf(out var vehicle) || (!vehicle?.Spawned ?? true))
        {
            dist = 0;
            return IntVec3.Invalid;
        }

        var cell = enterSpot.Cell.ToBaseMapCoord(vehicle);
        var faceCell = enterSpot.HasThing ? enterSpot.Thing.BaseFullRotation().FacingCell : enterSpot.Cell.BaseFullDirectionToInsideMap(vehicle).FacingCell;

        dist = 0;
        IntVec3 cell2;
        var cellRect = vehicle.VehicleRect();
        do
        {
            dist++;
            cell2 = cell - faceCell * dist;
            if (!cell2.InBounds(vehicle.Map))
            {
                dist = 0;
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
        out List<(TargetInfo, TargetInfo)> spotsQueue, bool canUseAbility = true)
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
            return vehiclePawn.CanReachVehicle(dest, peMode, traverseParms.maxDanger, traverseParms.mode, destMap, out exitSpot, out enterSpot);
        }
        
        if (working)
        {
            Log.ErrorOnce("Called CanReach() while working. This should never happen. Suppressing further errors.", 7312233);
            return false;
        }

        var region = root.GetRegion(departMap);
        var region2 = dest.Cell.GetRegion(destMap);
        TraverseParmsExtended parmsForCache = traverseParms;
        Ability_GrapplingHook ability = null;
        if (canUseAbility)
        {
            ability = traverseParms.pawn?.abilities?.AllAbilitiesForReading.OfType<Ability_GrapplingHook>()
                .FirstOrDefault(a => a is { CanCast.Accepted: true });
            parmsForCache.ability = ability?.def;
        }
        
        if (CrossMapReachabilityCache.TryGetCache(region, region2, parmsForCache, out var result, out exitSpot, out enterSpot, out spotsQueue))
        {
            DebugLog($"Result from cache: {root}, {departMap}, {dest}, {destMap}, {traverseParms}: {result}, {exitSpot}, {enterSpot}");
            return result;
        }
        try
        {
            working = true;
            result = false;
            if (MultiFloors.Active && (MultiFloors.GetLevel(departMap) != MultiFloors.GetLevel(destMap)))
            {
                return false;
            }

            var destBaseMap = destMap.IsVehicleMapOf(out var vehicle) && vehicle.Spawned ? vehicle.Map : destMap;
            var departBaseMap = departMap.IsVehicleMapOf(out var vehicle2) && vehicle2.Spawned ? vehicle2.Map : departMap;

            if (departMap.BaseMapOrCaravan == destMap.BaseMapOrCaravan)
            {
                var flag = departMap == departBaseMap;
                var flag2 = destBaseMap == destMap;
                var traverseParms2 = traverseParms.pawn != null ?
                    TraverseParms.For(traverseParms.pawn, traverseParms.maxDanger, TraverseMode.PassDoors, traverseParms.canBashDoors, traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences, traverseParms.avoidPersistentDanger) :
                    TraverseParms.For(TraverseMode.PassDoors, traverseParms.maxDanger, traverseParms.canBashDoors, traverseParms.alwaysUseAvoidGrid, traverseParms.canBashFences, traverseParms.avoidPersistentDanger);

                bool CanReachLocal(IntVec3 cell, IntVec3 cell2)
                {
                    return departMap.reachability.CanReach(root, cell, PathEndMode.OnCell, traverseParms) &&
                        destMap.reachability.CanReach(cell2, dest, peMode, traverseParms);
                }

                bool CellCheck(IntVec3 cell, Map map)
                {
                    return cell.Walkable(map) &&
                           (cell.GetDoor(map) is not { } door || door.HoldOpen ||
                            traverseParms.pawn is { } pawn && door.PawnCanOpen(pawn) && !door.IsForbidden(pawn));
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
                            foreach (var comp in vehicle2.GetSortedEnterComps(dest.Cell))
                            {
                                IntVec3 cell;
                                if (comp is CompZipline compZipline)
                                {
                                    var pair = compZipline.Pair;
                                    if (pair == null || !pair.HasComp<CompZipline>() || pair.Map != destMap) continue;

                                    cell = pair.Position;
                                }
                                else
                                {
                                    cell = EnterVehiclePosition(comp.parent);
                                }
                                result = CellCheck(cell, destMap) && CanReachLocal(comp.parent.Position, cell);
                                DebugLog($"VehicleMap => BaseMap: {root}, {cell}, {comp}, {traverseParms} :{result} {comp.parent}");
                                if (result)
                                {
                                    exitSpot = comp.parent;
                                    return result;
                                }
                            }
                            foreach (var c in vehicle2.CachedWalkableMapEdgeCells.OrderBy(c => (c.ToBaseMapCoord(vehicle2) - dest.Cell).LengthHorizontalSquared))
                            {
                                var targetInfo = new TargetInfo(c, departMap);
                                var cell = EnterVehiclePosition(targetInfo);
                                result = CellCheck(cell, destMap) && CanReachLocal(c, cell);
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
                                IntVec3 cell;
                                if (comp is CompZipline compZipline)
                                {
                                    var pair = compZipline.Pair;
                                    if (pair == null || !pair.HasComp<CompZipline>() || pair.Map != departMap) continue;

                                    cell = pair.Position;
                                }
                                else
                                {
                                    cell = EnterVehiclePosition(comp.parent);
                                }

                                result = CellCheck(cell, departMap) && CanReachLocal(cell, comp.parent.Position);
                                DebugLog($"BaseMap => VehicleMap: {root}, {cell}, {comp}, {traverseParms} :{result}");
                                if (result)
                                {
                                    enterSpot = comp.parent;
                                    return result;
                                }
                            }
                            foreach (var c in vehicle.CachedWalkableMapEdgeCells.OrderBy(c => (root - c.ToBaseMapCoord(vehicle)).LengthHorizontalSquared))
                            {
                                var targetInfo = new TargetInfo(c, destMap);
                                var cell = EnterVehiclePosition(targetInfo);
                                result = CellCheck(cell, departMap) && CanReachLocal(cell, c);
                                DebugLog($"BaseMap => VehicleMap: {new TargetInfo(root, departMap)}, {cell}, {c}, {dest.ToTargetInfo(destMap)}, {traverseParms} :{result}");
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
                                IntVec3 cell;
                                if (comp is CompZipline compZipline)
                                {
                                    var pair = compZipline.Pair;
                                    if (pair == null || !pair.HasComp<CompZipline>() || pair.Map == departMap)
                                        continue;

                                    cell = pair.Position;

                                    //departMapからdestMapまで直通のジップラインがある場合
                                    if (pair.Map == destMap)
                                    {
                                        var c = comp.parent.Position;
                                        if (CellCheck(cell, destMap) && CanReachLocal(c, cell))
                                        {
                                            exitSpot = comp.parent;
                                            return true;
                                        }
                                    }
                                }
                                else
                                {
                                    cell = EnterVehiclePosition(comp.parent);
                                }

                                foreach (var comp2 in vehicle.GetSortedEnterComps(cell))
                                {
                                    IntVec3 cell2;
                                    if (comp2 is CompZipline compZipline2)
                                    {
                                        var pair = compZipline2.Pair;
                                        if (pair == null || pair.Isnt<ZiplineEnd>() ||
                                            pair.Map != departBaseMap) continue;
                                        cell2 = pair.Position;
                                    }
                                    else
                                    {
                                        cell2 = EnterVehiclePosition(comp2.parent);
                                    }

                                    if (CanReach2(comp.parent.Position, cell, cell2, comp2.parent.Position))
                                    {
                                        exitSpot = comp.parent;
                                        enterSpot = comp2.parent;
                                        return true;
                                    }
                                }

                                foreach (var c2 in vehicle.CachedWalkableMapEdgeCells.OrderBy(c2 =>
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

                            foreach (var c in vehicle2.CachedWalkableMapEdgeCells.OrderBy(c =>
                                         (c.ToBaseMapCoord(vehicle2) - destBaseMapCoord)
                                         .LengthHorizontalSquared))
                            {
                                var targetInfo = new TargetInfo(c, departMap);
                                var cell = EnterVehiclePosition(targetInfo);

                                foreach (var comp2 in vehicle.GetSortedEnterComps(cell))
                                {
                                    IntVec3 cell2;
                                    if (comp2 is CompZipline compZipline)
                                    {
                                        var pair = compZipline.Pair;
                                        if (pair == null || pair.Isnt<ZiplineEnd>() ||
                                            pair.Map != departBaseMap) continue;
                                        cell2 = pair.Position;
                                    }
                                    else
                                    {
                                        cell2 = EnterVehiclePosition(comp2.parent);
                                    }

                                    if (CanReach2(c, cell, cell2, comp2.parent.Position))
                                    {
                                        exitSpot = targetInfo;
                                        enterSpot = comp2.parent;
                                        return true;
                                    }
                                }

                                foreach (var c2 in vehicle.CachedWalkableMapEdgeCells.OrderBy(c2 =>
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
                                return CellCheck(cell2, departBaseMap) &&
                                       CellCheck(cell3, departBaseMap) &&
                                       departMap.reachability.CanReach(root, cell, PathEndMode.OnCell,
                                           traverseParms) &&
                                       departBaseMap.reachability.CanReach(cell2, cell3, PathEndMode.OnCell,
                                           traverseParms2) &&
                                       destMap.reachability.CanReach(cell4, dest, peMode, traverseParms2);
                            }
                        }
                        
                        bool CanReachRecursive(out List<(TargetInfo, TargetInfo)> spotsQueue)
                        {
                            spotsQueue = null;
                            var destBaseMapCoord = dest.Cell.ToBaseMapCoord(vehicle);
                            candidateMaps.AddRange(departMap.BaseMapAndVehicleMaps(false));
                            result = EnterMap(vehicle2.VehicleMap, root);
                            if (result)
                            {
                                spotsQueue = tmpTargets.Reverse().ToList();
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
                                var isVehicleMap = map.IsVehicleMapOf(out var vehicle3);
                                var comps = isVehicleMap
                                    ? vehicle3.GetSortedEnterComps(destBaseMapCoord,
                                        VehiclePawnWithMap.EnterCompKind.ZiplineOnly).AsEnumerable()
                                    : RegionTraverserAcrossMaps.ZiplineDefs.SelectMany(def =>
                                        map.listerThings.ThingsOfDef(def).Select(t => t.TryGetComp<CompZipline>()));
                                foreach (var comp in comps)
                                {
                                    if (comp is not CompZipline comp2) continue;
                                    // Pairが適正か
                                    var pair = comp2.Pair;
                                    if (pair is not { Spawned: true } || !visitedMaps.Contains(pair.Map))
                                        continue;

                                    // Pairの車両を探索
                                    var c = comp2.parent.Position;
                                    var c2 = pair.Position;
                                    var map2 = comp2.parent.Map;
                                    if (CellCheck(c, map) && CellCheck(c2, map2) &&
                                        map2.reachability.CanReach(start, c, PathEndMode.OnCell, traverseParms2))
                                    {
                                        tmpTargets.Push((comp2.parent, TargetInfo.Invalid));
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
                                            tmpTargets.Push((castSpot, targSpot));
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
            CrossMapReachabilityCache.Cache(region, region2, parmsForCache, result, exitSpot, enterSpot, spotsQueue);
            working = false;
        }
    }

    public static bool TryFindNearestStandableCell(VehiclePawn vehicle, IntVec3 cell, Map map, out IntVec3 result, float radius = -1f)
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
            if (intVec.InBounds(map) && intVec.Standable(vehicle, map) && (!VehicleMod.settings.main.fullVehiclePathing || vehicle.DrivableRectOnCell(intVec, true, map)))
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

        public bool CanReachVehicle(LocalTargetInfo dest, PathEndMode peMode, Danger maxDanger, TraverseMode mode, Map destMap, out TargetInfo exitSpot, out TargetInfo enterSpot)
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
                return MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, dest, peMode, traverseParms);
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
                        result = vehicle3.GetSortedEnterComps(dest.Cell, VehiclePawnWithMap.EnterCompKind.RampOnly).Any(e =>
                        {
                            tmpThing = e.parent;
                            if (!AvailableEnterSpot(e) || tmpThing.OccupiedRect().Any(c3 => !vehicle.Drivable(c3, departMap))) return false;

                            var cell = tmpThing.Position + (tmpThing.Rotation.FacingCell * vehicle.HalfLength());
                            var cell2 = EnterVehiclePosition(tmpThing, vehicle);
                            return MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, cell, PathEndMode.OnCell, traverseParms) &&
                                   destMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell2, dest, peMode, TraverseMode.PassDoors, traverseParms.maxDanger);
                        });
                        exitSpot = result ? tmpThing : TargetInfo.Invalid;
                        return result;
                    }
                    //vehicleがベースマップに居て目的地が車上マップ
                    case true when !flag2 && vehicle2 != null:
                    {
                        Thing tmpThing = null;
                        result = vehicle2.GetSortedEnterComps(vehicle.Position, VehiclePawnWithMap.EnterCompKind.RampOnly).Any(e =>
                        {
                            tmpThing = e.parent;
                            if (!AvailableEnterSpot(e) || tmpThing.OccupiedRect().Any(c3 => !vehicle.Drivable(c3, destMap))) return false;

                            var cell = EnterVehiclePosition(tmpThing, vehicle);
                            var cell2 = tmpThing.Position + (tmpThing.Rotation.FacingCell * vehicle.HalfLength());
                            return MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, cell, PathEndMode.OnCell, traverseParms) &&
                                   destMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell2, dest, peMode, TraverseMode.PassDoors, traverseParms.maxDanger);
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
                                result = vehicle3.GetSortedEnterComps(dest.Cell.ToBaseMapCoord(vehicle2), VehiclePawnWithMap.EnterCompKind.RampOnly).Any(e =>
                                {
                                    tmpThing = e.parent;
                                    if (!AvailableEnterSpot(e) || tmpThing.OccupiedRect().Any(c => !vehicle.Drivable(c, departMap))) return false;

                                    var cell = EnterVehiclePosition(tmpThing, vehicle);
                                    var cell2 = tmpThing.Position + (tmpThing.Rotation.FacingCell * vehicle.HalfLength());

                                    return vehicle2.GetSortedEnterComps(cell, VehiclePawnWithMap.EnterCompKind.RampOnly).Any(e2 =>
                                    {
                                        tmpThing2 = e2.parent;
                                        if (!AvailableEnterSpot(e2) || tmpThing2.OccupiedRect().Any(c => !vehicle.Drivable(c, destMap))) return false;

                                        var cell3 = EnterVehiclePosition(tmpThing2, vehicle);
                                        var cell4 = tmpThing2.Position + (tmpThing2.Rotation.FacingCell * vehicle.HalfLength());
                                        return MapComponentCache<VehiclePathingSystem>.GetComponent(departMap)[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(vehicle.Position, cell2, PathEndMode.OnCell, traverseParms) &&
                                               departBaseMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell, cell3, PathEndMode.OnCell, TraverseMode.PassDoors, traverseParms.maxDanger) &&
                                               destMapPathing[vehicle.VehicleDef].VehicleReachability.CanReachVehicle(cell4, dest, peMode, TraverseMode.PassDoors, traverseParms.maxDanger);
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
                        FlashCell(spots.Item1, false, ref i);
                        FlashCell(spots.Item2, true, ref i);
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
}