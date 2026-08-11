using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Vehicles.World;
using Verse;
using Verse.AI.Group;

namespace VehicleMapFramework;

[PublicAPI]
public static class VehicleMapUtility
{
  public const float YCompress = 1.5f / Altitudes.AltInc - 1f;

  private static readonly VfVersionalPatchAttribute VfVersional = new (VfVersionalPatchAttribute.LatestRelease, ComparisonType.LessThanOrEqual);

  private static readonly List<Thing> tmpThingList = [];

  private static readonly List<Building> tmpBuildingList = [];

  private static readonly AccessTools.FieldRef<RoofGrid, Map> roofGrid_map = AccessTools.FieldRefAccess<RoofGrid, Map>("map");

  private static readonly SimpleCurve PointsPerWealthCurve =
    AccessTools.StaticFieldRefAccess<SimpleCurve>(typeof(StorytellerUtility), "PointsPerWealthCurve");

  private static readonly SimpleCurve PointsPerColonistByWealthCurve =
    AccessTools.StaticFieldRefAccess<SimpleCurve>(typeof(StorytellerUtility), "PointsPerColonistByWealthCurve");

  private static readonly SimpleCurve PointsFactorForColonyMechsCurve =
    AccessTools.StaticFieldRefAccess<SimpleCurve>(typeof(StorytellerUtility), "PointsFactorForColonyMechsCurve");

  private static readonly SimpleCurve PointsFactorForColonySubhumanCurve =
    AccessTools.StaticFieldRefAccess<SimpleCurve>(typeof(StorytellerUtility), "PointsFactorForColonySubhumanCurve");

  private static readonly SimpleCurve PointsFactorForPawnAgeYearsCurve =
    AccessTools.StaticFieldRefAccess<SimpleCurve>(typeof(StorytellerUtility), "PointsFactorForPawnAgeYearsCurve");

  public static Map CurrentMap => Command_FocusVehicleMap.FocusedVehicle != null
    ? Command_FocusVehicleMap.FocusedVehicle.CurrentLevel
    : Find.CurrentMap;

  public static bool FocusedOnVehicleMap(out VehiclePawnWithMap vehicle)
  {
    if (Command_FocusVehicleMap.FocusedVehicle is null)
      return Find.CurrentMap.IsNonFocusedVehicleMapOf(out vehicle);
    vehicle = Command_FocusVehicleMap.FocusedVehicle;
    return true;
  }

  private static Vector3 MapPivot(Map map)
  {
    return AsAboveSoBelow.Active
      ? AsAboveSoBelow.RectOfBand(map, AsAboveSoBelow.CurrentBand(map)).CenterVector3
      : CellRect.WholeMap(map).CenterVector3;
  }

  private static Vector3 MapPivot(Map map, IntVec3 bandSource)
  {
    return AsAboveSoBelow.Active && AsAboveSoBelow.TryBandRectOf(map, bandSource, out var rect)
      ? rect.CenterVector3
      : CellRect.WholeMap(map).CenterVector3;
  }

  public static CellRect ClipInsideVehicleMap(ref this CellRect cellRect, Map map)
  {
    if (map.IsVehicleMapOf(out var vehicle))
    {
      //if (vehicle.Spawned)
      //{
      //    var vehicleRect = vehicle.VehicleRect(true);
      //    cellRect = cellRect.MovedBy(-vehicleRect.Min);
      //    return cellRect.ClipInsideMap(vehicle.VehicleMap);
      //}
      return cellRect = CellRect.WholeMap(vehicle.VehicleMap);
    }
    return cellRect.ClipInsideMap(map);
  }

  public static Matrix4x4 ToBaseMapCoord(this Matrix4x4 matrix, VehiclePawnWithMap vehicle)
  {
    var rootPos = matrix.Position();
    matrix.SetColumn(3, rootPos.ToBaseMapCoord(vehicle).WithY(rootPos.y));
    return matrix;
  }

  public static Vector3 OffsetFor(VehiclePawnWithMap vehicle)
  {
    return OffsetFor(vehicle, vehicle.FullRotation).RotatedBy(vehicle.Transform.rotation);
  }

  public static Vector3 OffsetFor(VehiclePawnWithMap vehicle, Rot8 rot)
  {
    var offset = Vector3.zero;
    var vehicleMap = vehicle.VehicleMapProps;
    if (vehicleMap == null) return offset;

    offset = rot.AsByte switch
    {
      Rot8.NorthInt => OffsetNorth(),
      Rot8.EastInt => vehicleMap.offsetEast ?? (vehicleMap.offsetWest == null
        ? vehicleMap.offsetEast = vehicleMap.offsetWest = vehicleMap.offset
        : vehicleMap.offsetEast = vehicleMap.offsetWest.Value.MirrorHorizontal()).Value,
      Rot8.SouthInt => OffsetSouth(),
      Rot8.WestInt => vehicleMap.offsetWest ?? (vehicleMap.offsetEast == null
        ? vehicleMap.offsetWest = vehicleMap.offsetEast = vehicleMap.offset
        : vehicleMap.offsetWest = vehicleMap.offsetEast.Value.MirrorHorizontal()).Value,
      Rot8.NorthEastInt => vehicleMap.offsetNorthEast ??=
        (vehicleMap.offsetNorthWest ??= OffsetNorth().RotatedBy(-45f)).MirrorHorizontal(),
      Rot8.SouthEastInt => vehicleMap.offsetSouthEast ??=
        (vehicleMap.offsetSouthWest ??= OffsetSouth().RotatedBy(45f)).MirrorHorizontal(),
      Rot8.SouthWestInt => vehicleMap.offsetSouthWest ??=
        (vehicleMap.offsetSouthEast ??= OffsetSouth().RotatedBy(-45f)).MirrorHorizontal(),
      Rot8.NorthWestInt => vehicleMap.offsetNorthWest ??=
        (vehicleMap.offsetNorthEast ??= OffsetNorth().RotatedBy(45f)).MirrorHorizontal(),
      _ => offset
    };
    return offset;

    Vector3 OffsetNorth() => vehicleMap.offsetNorth ?? (vehicleMap.offsetSouth == null
      ? vehicleMap.offsetNorth = vehicleMap.offsetSouth = vehicleMap.offset
      : vehicleMap.offsetNorth = vehicleMap.offsetSouth.Value.MirrorVertical()).Value;

    Vector3 OffsetSouth() => vehicleMap.offsetSouth ?? (vehicleMap.offsetNorth == null
      ? vehicleMap.offsetSouth = vehicleMap.offsetNorth = vehicleMap.offset
      : vehicleMap.offsetNorth = vehicleMap.offsetNorth.Value.MirrorVertical()).Value;
  }

  public static IntVec3 HitboxToMapCell(VehiclePawnWithMap vehicle)
  {
    return vehicle.MapSize / 2 - OffsetFor(vehicle, Rot8.North).ToIntVec3();
  }

  public static IntVec2 MapCellToHitbox(VehiclePawnWithMap vehicle)
  {
    return (OffsetFor(vehicle, Rot8.North).ToIntVec3() - vehicle.MapSize / 2).ToIntVec2;
  }

  public static float PrintExtraRotation(Thing thing)
  {
    var result = 0f;
    if (thing.IsOnVehicleMapOf(out _))
    {
      result -= VehicleSectionLayerManager.RotForPrint.AsAngle;
    }
    return result;
  }

  public static Map BaseMap(this Zone zone)
  {
    if (zone.Map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
    {
      return vehicle.Map;
    }
    return zone.Map;
  }

  public static IntVec3 PositionOnBaseMap(this IHaulDestination dest)
  {
    return dest.Map.IsVehicleMapOf(out var vehicle) ? dest.Position.ToBaseMapCoord(vehicle) : dest.Position;
  }

  public static TargetInfo ToBaseMapTargetInfo(ref LocalTargetInfo target, Map map)
  {
    if (!target.IsValid)
    {
      return TargetInfo.Invalid;
    }
    return target.Thing != null ? new TargetInfo(target.Thing) : new TargetInfo(target.CellOnBaseMap(), map);
  }

  public static IntVec3 CellOnAnotherThingMap(this LocalTargetInfo target, Thing another)
  {
    if (target.HasThing)
    {
      return target.Thing.PositionOnAnotherThingMap(another);
    }
    return another.IsOnVehicleMapOf(out var vehicle) ? target.Cell.ToVehicleMapCoord(vehicle) : target.Cell;
  }

  public static IntVec3 CellOnAnotherMap(this IntVec3 cell, Map another)
  {
    return another.IsVehicleMapOf(out var vehicle) ? cell.ToVehicleMapCoord(vehicle) : cell;
  }

  public static int HalfLength(this VehicleDef vehicleDef)
  {
    return vehicleDef.size.z / 2;
  }

  public static Rot4 RotForVehicleDraw(this Rot8 rot)
  {
    if (rot.IsDiagonal)
    {
      return rot == Rot8.NorthEast || rot == Rot8.NorthWest ? Rot4.North : Rot4.South;
    }
    return rot;
  }

  public static IntVec2 BaseRotatedSize(Thing thing)
  {
    return !thing.BaseRotation().IsHorizontal
      ? thing.def.size
      : new IntVec2(thing.def.size.z, thing.def.size.x);
  }

  public static float VehicleMapMass(VehiclePawnWithMap vehicle)
  {
    var mass = CollectionsMassCalculator.MassUsage(vehicle.VehicleMap.listerThings.AllThings, IgnorePawnsInventoryMode.DontIgnore, true);
    if (MultiFloors.Active)
    {
      mass += MultiFloors.GetOtherLevels(vehicle.VehicleMap)
        .Sum(map => CollectionsMassCalculator.MassUsage(map.listerThings.AllThings, IgnorePawnsInventoryMode.DontIgnore, true));
    }
    return mass;
  }

  public static Vector3 RotateForPrintNegate(Vector3 vector)
  {
    return vector.RotatedBy(-VehicleSectionLayerManager.RotForPrint.AsAngle);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool TryGetVehicleMap(this IntVec3 c, Map map, out VehiclePawnWithMap vehicle)
  {
    vehicle = MapComponentCache<VehicleMapGrid>.GetComponent(map).VehicleAt(c);
    return vehicle != null;
  }

  //thingが車両マップ上にあったらthingの中心を基準として位置と回転を下の車両基準に回転するわよ
  public static void SetTRSOnVehicle(ref Matrix4x4 matrix, Vector3 pos, Quaternion q, Vector3 s, Thing thing)
  {
    if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      var rot = vehicle.FullRotation;
      var angle = rot.AsAngle + vehicle.Transform.rotation;
      matrix = Matrix4x4.TRS(Ext_Math.RotatePoint(pos, thing.TrueCenter(), -angle),
        q * vehicle.FullAngleQuat,
        s);
      return;
    }
    matrix = Matrix4x4.TRS(pos, q, s);
  }

  public static Vector3 SelectedDrawPosOffset(Vector3 original, IntVec3 center)
  {
    VehiclePawnWithMap vehicle = null;
    return Find.Selector.SelectedObjects
      .Any(o => o is Thing thing && thing.Position == center && thing.IsOnNonFocusedVehicleMapOf(out vehicle))
      ? original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor())
      : original;
  }

  public static Vector3 FocusedDrawPosOffset(Vector3 original)
  {
    return FocusedOnVehicleMap(out var vehicle)
      ? original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor())
      : original;
  }

  public static Vector3 FocusedOrSelectedDrawPosOffset(Vector3 original, IntVec3 center)
  {
    Thing thing;
    if ((thing = Find.Selector.SelectedObjects.OfType<Thing>().FirstOrDefault(t => t.Position == center)) != null)
    {
      if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
      {
        return original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor());
      }
    }
    else if (FocusedOnVehicleMap(out var vehicle))
    {
      return original.ToBaseMapCoord(vehicle).WithY(AltitudeLayer.MetaOverlays.AltitudeFor());
    }
    return original;
  }

  public static IEnumerable<Thing> ColonyThingsWillingToBuyOnVehicle(this VehiclePawnWithMap vehicle, ITrader trader)
  {
    var map = vehicle.VehicleMap;
    var enumerable = map.listerThings.AllThings.Where(x => x.def.category == ThingCategory.Item && TradeUtility.PlayerSellableNow(x, trader) && !x.Position.Fogged(map) && (map.areaManager.Home[x.Position] || x.IsInAnyStorage()));
    foreach (var item in enumerable)
    {
      yield return item;
    }

    if (ModsConfig.BiotechActive)
    {
      var list = map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.GeneBank);
      foreach (var item3 in list
                 .Select(item2 => item2.TryGetComp<CompGenepackContainer>())
                 .Where(compGenepackContainer => compGenepackContainer != null)
                 .Select(compGenepackContainer => compGenepackContainer.ContainedGenepacks)
                 .SelectMany(containedGenepacks => containedGenepacks))
      {
        yield return item3;
      }
    }

    var enumerable2 = map.listerBuildings.AllColonistBuildingsOfType<IHaulSource>();
    foreach (var item4 in enumerable2)
    {
      foreach (var item5 in item4.GetDirectlyHeldThings())
      {
        yield return item5;
      }
    }

    if (trader is Pawn pawn && pawn.GetLord() == null)
    {
      yield break;
    }

    if (vehicle.Spawned) yield break;

    foreach (var item6 in from x in TradeUtility.AllSellableColonyPawns(map)
             where !x.Downed
             select x)
    {
      yield return item6;
    }
  }

  public static bool ShouldRotatedOnVehicle(this ThingDef tDef)
  {
    return tDef.fillPercent > 0.25f ||
           tDef.Size != IntVec2.One ||
           tDef.graphic is not Graphic_Single && tDef.graphic is not Graphic_Collection ||
           tDef.hasInteractionCell ||
           tDef.drawerType == DrawerType.MapMeshOnly ||
           tDef.drawerType == DrawerType.MapMeshAndRealTime ||
           tDef.size.x != tDef.size.z;
  }

  public static List<Thing> GetThingListAcrossMaps(this IntVec3 c, Map map)
  {
    tmpThingList.Clear();
    var thingList = c.InBounds(map) ? map.thingGrid.ThingsListAtFast(c) : tmpThingList;
    if (map.IsVehicleMapOf(out var vehicle))
    {
      tmpThingList.AddRange(thingList);
      var root = c.ToBaseMapCoord(vehicle);

      if (vehicle.Spawned)
      {
        var baseMap = vehicle.Map;
        tmpThingList.AddRange(root.GetThingList(baseMap));
        if (root.TryGetVehicleMap(baseMap, out var vehicle2) && vehicle != vehicle2)
        {
          var c2 = root.ToVehicleMapCoord(vehicle2);
          if (c2.InBounds(vehicle2.VehicleMap))
            tmpThingList.AddRange(vehicle2.VehicleMap.thingGrid.ThingsListAtFast(c2));
        }
        return tmpThingList;
      }

      foreach (var m in map.BaseMapAndVehicleMaps(false))
      {
        if (m.IsVehicleMapOf(out var vehicle3))
        {
          var c2 = root.ToVehicleMapCoord(vehicle3);
          if (c2.InBounds(m))
            tmpThingList.AddRange(m.thingGrid.ThingsListAtFast(c2));
        }
        else
        {
          if (root.InBounds(m))
            tmpThingList.AddRange(m.thingGrid.ThingsListAtFast(root));
        }
      }
      return tmpThingList;
    }

    if (c.TryGetVehicleMap(map, out var vehicle4))
    {
      var c2 = c.ToVehicleMapCoord(vehicle4);
      var map2 = vehicle4.VehicleMap;
      if (c2.InBounds(map2))
      {
        tmpThingList.AddRange(thingList);
        tmpThingList.AddRange(map2.thingGrid.ThingsListAtFast(c2));
        return tmpThingList;
      }
    }
    return thingList;
  }

  public static List<Building> AddColonistBuildingList(List<Building> allBuildingsColonist, Thing instance)
  {
    var maps = instance.Map.BaseMapAndVehicleMaps(false);
    if (maps.NullOrEmpty()) return allBuildingsColonist;

    tmpBuildingList.Clear();
    tmpBuildingList.AddRange(allBuildingsColonist);
    foreach (var map in maps)
    {
      tmpBuildingList.AddRange(map.listerBuildings.allBuildingsColonist);
    }
    return tmpBuildingList;
  }

  public static bool RoofedAcrossMaps(RoofGrid roofGrid, IntVec3 c)
  {
    return c.RoofedAcrossMaps(roofGrid_map(roofGrid));
  }

  public static float DefaultThreatPointsNowForMapVehicles(IIncidentTarget target)
  {
    List<Pawn> pawns;
    if (target is Map map && map.IsVehicleMapOf(out var vehicle))
    {
      var vehicleCaravanOrStashedVehicle = vehicle.VehicleCaravanOrStashedVehicle;
      switch (vehicleCaravanOrStashedVehicle)
      {
        case VehicleCaravan caravan:
          target = caravan;
          pawns = [.. caravan.PlayerPawnsForStoryteller];
          break;
        case StashedVehicle stashedVehicle:
          pawns = [.. stashedVehicle.Vehicles];
          break;
        default:
        {
          if (vehicle.Spawned)
          {
            target = vehicle.Map;
            pawns = [.. vehicle.Map.PlayerPawnsForStoryteller];
          }
          else
          {
            return 0f;
          }
          break;
        }
      }
    }
    else
    {
      pawns = [.. target.PlayerPawnsForStoryteller];
    }

    var wealthForStoryteller = target.PlayerWealthForStoryteller;
    wealthForStoryteller += pawns.OfType<VehiclePawnWithMap>().Sum(v => v.VehicleMap.PlayerWealthForStoryteller);
    var num1 = PointsPerWealthCurve.Evaluate(wealthForStoryteller);
    var num2 = 0f;
    PawnsFactor(pawns);

    return Mathf.Clamp(
      (num1 + num2) * target.IncidentPointsRandomFactorRange.RandomInRange *
      Mathf.Lerp(1f,
        Find.StoryWatcher.watcherAdaptation.TotalThreatPointsFactor,
        Find.Storyteller.difficulty.adaptationEffectFactor) * Find.Storyteller.difficulty.threatScale *
      Find.Storyteller.def.pointsFactorFromDaysPassed.Evaluate(GenDate.DaysPassedSinceSettle),
      StorytellerUtility.GlobalPointsMin(),
      10000f);

    void PawnsFactor(IEnumerable<Pawn> pawnsEnumerable)
    {
      foreach (var p in pawnsEnumerable)
      {
        if (!p.IsQuestLodger())
        {
          var a = 0.0f;
          if (p.IsFreeColonist)
          {
            a = PointsPerColonistByWealthCurve.Evaluate(wealthForStoryteller);
          }
          else if (p.IsAnimal && p.Faction == Faction.OfPlayer && !p.Downed &&
                   p.training.CanAssignToTrain(TrainableDefOf.Release).Accepted)
          {
            a = 0.08f * p.kindDef.combatPower;
            if (target is Caravan)
              a *= 0.7f;
          }
          else if (p.IsColonyMech && !p.Downed)
          {
            a = p.kindDef.combatPower * PointsFactorForColonyMechsCurve.Evaluate(wealthForStoryteller);
          }
          else if (p.IsSubhuman)
          {
            a = p.kindDef.combatPower * PointsFactorForColonySubhumanCurve.Evaluate(wealthForStoryteller);
          }

          if (p is VehiclePawnWithMap vehicle2)
          {
            a += PointsPerWealthCurve.Evaluate(vehicle2.VehicleMap.PlayerWealthForStoryteller);
            PawnsFactor(vehicle2.VehicleMap.PlayerPawnsForStoryteller);
          }

          if (p is VehiclePawn vehicle3)
          {
            PawnsFactor(vehicle3.AllPawnsAboard);
          }
          if (a > 0f)
          {
            if (p.ParentHolder is Building_CryptosleepCasket)
              a *= 0.3f;
            var num3 = Mathf.Lerp(a, a * p.health.summaryHealth.SummaryHealthPercent, 0.65f);
            if (p.IsSlaveOfColony)
              num3 *= 0.75f;
            if (ModsConfig.BiotechActive && p.RaceProps.Humanlike)
              num3 *= PointsFactorForPawnAgeYearsCurve.Evaluate(p.ageTracker.AgeBiologicalYearsFloat);
            num2 += num3;
          }
        }
      }
    }
  }

  extension(Map map)
  {

    public bool IsVehicleMap => map.IsVehicleMapOf(out _);

    public bool IsNonFocusedVehicleMap => map.IsNonFocusedVehicleMapOf(out _);

    public bool CrossMapContext => map is not null &&
                                   (map.IsVehicleMap || VehiclePawnWithMapCache.AllVehiclesOn(map).Count != 0);

    public Map GroundMap => map.BaseMap();

    public object BaseMapOrCaravan =>
      map.IsVehicleMapOf(out var vehicle)
        ? vehicle.Spawned ? vehicle.Map : vehicle.VehicleCaravanOrStashedVehicle
        : map;

    [UsedImplicitly] // Reflection access by Faction Territories and Vassalage
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsVehicleMapOf(out VehiclePawnWithMap vehicle)
    {
      var mapParentVehicle = VehicleMapParentsComponent.GetCachedVehicle(map);
      if (mapParentVehicle is not null)
      {
        vehicle = mapParentVehicle.vehicle;
        return vehicle is not null;
      }

      vehicle = null;
      return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsNonFocusedVehicleMapOf(out VehiclePawnWithMap vehicle)
    {
      if (map.IsVehicleMapOf(out vehicle) && (VehicleMapFramework.settings.drawPlanet || Find.CurrentMap != vehicle.VehicleMap))
      {
        return true;
      }
      vehicle = null;
      return false;
    }

    [UsedImplicitly] // Reflection access by Portable Blueprints
    public IEnumerable<Map> BaseMapAndVehicleMaps()
    {
      return map.BaseMapAndVehicleMaps(true);
    }

    public HashSet<Map> BaseMapAndVehicleMaps(bool includeItself)
    {
      if (map?.GetCachedMapComponent<VehiclePawnWithMapCache>() is not { } component)
        return [];
      ref var cache = ref component.cachedBaseMapAndVehicleMaps;
      if (cache.lastCachedTick == GenTicks.TicksGame)
      {
        return includeItself ? cache.includeItself : cache.excludeItself;
      }

      cache.lastCachedTick = GenTicks.TicksGame;
      cache.includeItself.Clear();
      cache.excludeItself.Clear();
      var baseMap = map.BaseMap();
      if (baseMap is null)
        return cache.includeItself;

      cache.includeItself.Add(map);
      if (MultiFloors.Active && MultiFloors.GroundMap(map) != map)
      {
        return includeItself ? cache.includeItself : cache.excludeItself;
      }
      
      if (baseMap != map)
        cache.excludeItself.Add(baseMap);

      if (baseMap.IsVehicleMapOf(out var vehicle) && vehicle.VehicleCaravanOrStashedVehicle is { } vehicleCaravanOrStashedVehicle)
      {
        foreach (var vehicle2 in vehicleCaravanOrStashedVehicle.Vehicles)
        {
          if (vehicle != vehicle2 && vehicle2 is VehiclePawnWithMap vehiclePawnWithMap)
            cache.excludeItself.Add(vehiclePawnWithMap.VehicleMap);
        }
      }
      else
      {
        foreach (var vehicle2 in VehiclePawnWithMapCache.AllVehiclesOn(baseMap))
        {
          if (vehicle2.VehicleMap != map)
            cache.excludeItself.Add(vehicle2.VehicleMap);
        }
      }
      
      cache.includeItself.AddRange(cache.excludeItself);
      return includeItself ? cache.includeItself : cache.excludeItself;
    }

    public IEnumerable<Map> VehicleMapsOnMap()
    {
      if (map.IsVehicleMapOf(out var vehicle))
      {
        if (vehicle.VehicleCaravanOrStashedVehicle is { } vehicleCaravanOrStashedVehicle)
        {
          foreach (var vehicle2 in vehicleCaravanOrStashedVehicle.Vehicles)
          {
            if (vehicle != vehicle2 && vehicle2 is VehiclePawnWithMap vehiclePawnWithMap)
              yield return vehiclePawnWithMap.VehicleMap;
          }
        }
      }
      else
      {
        foreach (var vehicle2 in VehiclePawnWithMapCache.AllVehiclesOn(map))
        {
          yield return vehicle2.VehicleMap;
        }
      }
    }

    public void VehicleMapsOnMap(List<Map> list)
    {
      if (map.IsVehicleMapOf(out var vehicle))
      {
        if (vehicle.VehicleCaravanOrStashedVehicle is { } vehicleCaravanOrStashedVehicle)
        {
          foreach (var vehicle2 in vehicleCaravanOrStashedVehicle.Vehicles)
          {
            if (vehicle != vehicle2 && vehicle2 is VehiclePawnWithMap vehiclePawnWithMap)
              list.Add(vehiclePawnWithMap.VehicleMap);
          }
        }
      }
      else
      {
        foreach (var vehicle2 in VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(map))
        {
          list.Add(vehicle2.VehicleMap);
        }
      }
    }

    [UsedImplicitly] // Reflection access by Portable Blueprints
    public Map BaseMap()
    {
      if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
      {
        return vehicle.Map;
      }
      return map;
    }

    public CellRect BoundsRect(int contractedBy = 0)
    {
      if (!AsAboveSoBelow.Active || !map.IsVehicleMapOf(out var vehicle))
        return GenGrid.BoundsRect(map, contractedBy);

      var size = vehicle.MapSize;
      return [with(contractedBy, contractedBy, size.x - contractedBy * 2, size.z - contractedBy * 2)];
    }
  }

  extension(Thing thing)
  {
    public bool IsOnVehicleMap => thing.IsOnVehicleMapOf(out _);

    public bool IsOnNonFocusedVehicleMap => thing.IsOnNonFocusedVehicleMapOf(out _);

    public Map GroundMap => thing.BaseMap();

    public object BaseMapOrCaravan =>
      thing.IsOnVehicleMapOf(out var vehicle)
        ? vehicle.Spawned ? vehicle.Map : vehicle.VehicleCaravanOrStashedVehicle
        : thing.Map;

    public object MapHeldBaseMapOrCaravan
    {
      get
      {
        var mapHeld = thing.MapHeld;
        return mapHeld.IsVehicleMapOf(out var vehicle)
          ? vehicle.Spawned ? vehicle.Map : vehicle.VehicleCaravanOrStashedVehicle
          : mapHeld;
      }
    }

    public bool IsOnVehicleMapOf(out VehiclePawnWithMap vehicle)
    {
      if (thing != null) return thing.Map.IsVehicleMapOf(out vehicle);
      vehicle = null;
      return false;
    }

    public bool IsOnNonFocusedVehicleMapOf(out VehiclePawnWithMap vehicle)
    {
      if (thing != null) return thing.Map.IsNonFocusedVehicleMapOf(out vehicle);
      vehicle = null;
      return false;
    }

    public Map BaseMap()
    {
      if (thing.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned)
      {
        return vehicle.Map;
      }
      return thing.Map;
    }

    public Map MapHeldBaseMap()
    {
      return thing.MapHeld.BaseMap();
    }
    
    public IntVec3 PositionOnBaseMap
    {
      get
      {
        if (!thing.IsOnVehicleMapOf(out var vehicle)) return thing.Position;
        var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(thing.Map);
        if (component.cachedPosOnBaseMap.TryGetValue(thing, out var pos))
        {
          return pos;
        }
        pos = thing.Position.ToBaseMapCoord(vehicle);
        component.cachedPosOnBaseMap[thing] = pos;
        return pos;
      }
    }

    public IntVec3 PositionOnBaseMapSpawned
    {
      get
      {
        if (!thing.IsOnVehicleMapOf(out var vehicle) || !vehicle.Spawned) return thing.Position;
        var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(thing.Map);
        if (component.cachedPosOnBaseMap.TryGetValue(thing, out var pos))
        {
          return pos;
        }
        pos = thing.Position.ToBaseMapCoord(vehicle);
        component.cachedPosOnBaseMap[thing] = pos;
        return pos;
      }
    }

    public IntVec3 PositionHeldOnBaseMap
    {
      get
      {
        if (thing.Spawned)
        {
          return thing.PositionOnBaseMap;
        }
        var rootPosition = IntVec3.Invalid;
        var holder = thing.ParentHolder;
        while (holder != null)
        {
          rootPosition = holder switch
          {
            Thing { PositionOnBaseMap.IsValid: true } thing2 => thing2.PositionOnBaseMap,
            ThingComp thingComp when thingComp.parent.PositionOnBaseMap.IsValid => thingComp.parent
              .PositionOnBaseMap,
            _ => rootPosition
          };

          holder = holder.ParentHolder;
        }
        return rootPosition.IsValid ? rootPosition : thing.PositionOnBaseMap;
      }
    }

    public IntVec3 PositionHeldOnBaseMapSpawned
    {
      get
      {
        if (thing.Spawned)
        {
          return thing.PositionOnBaseMapSpawned;
        }
        var rootPosition = IntVec3.Invalid;
        var holder = thing.ParentHolder;
        while (holder != null)
        {
          rootPosition = holder switch
          {
            Thing { PositionOnBaseMapSpawned.IsValid: true } thing2 => thing2.PositionOnBaseMapSpawned,
            ThingComp thingComp when thingComp.parent.PositionOnBaseMapSpawned.IsValid => thingComp.parent
              .PositionOnBaseMapSpawned,
            _ => rootPosition
          };

          holder = holder.ParentHolder;
        }
        return rootPosition.IsValid ? rootPosition : thing.PositionOnBaseMapSpawned;
      }
    }
    
    public IntVec3 PositionOnAnotherMap(Map map)
    {
      return map.IsVehicleMapOf(out var vehicle) ? thing.PositionOnBaseMap.ToVehicleMapCoord(vehicle) : thing.PositionOnBaseMap;
    }
    
    public IntVec3 PositionOnAnotherThingMap(Thing another)
    {
      return another.IsOnVehicleMapOf(out var vehicle) ? thing.PositionOnBaseMap.ToVehicleMapCoord(vehicle) : thing.PositionOnBaseMap;
    }
    
    public Rot4 BaseRotation()
    {
      return thing.IsOnNonFocusedVehicleMapOf(out var vehicle) ? new Rot4(thing.Rotation.AsInt + vehicle.Rotation.AsInt) : thing.Rotation;
    }

    public Rot4 BaseRotationSpawned()
    {
      return thing.IsOnNonFocusedVehicleMapOf(out var vehicle) && vehicle.Spawned ? new Rot4(thing.Rotation.AsInt + vehicle.Rotation.AsInt) : thing.Rotation;
    }

    public Rot4 BaseRotationVehicleDraw()
    {
      return thing.IsOnNonFocusedVehicleMapOf(out var vehicle) ? new Rot4(thing.Rotation.AsInt + vehicle.FullRotation.RotForVehicleDraw().AsInt) : thing.Rotation;
    }

    public Rot8 BaseFullRotation()
    {
      if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
      {
        return new Rot8(thing.Rotation).Rotated(vehicle.FullRotation);
      }
      return thing.Rotation;
    }

    public Rot8 BaseFullRotationSpawned()
    {
      if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle) && vehicle.Spawned)
      {
        return new Rot8(thing.Rotation).Rotated(vehicle.FullRotation);
      }
      return thing.Rotation;
    }

    public Rot4 BaseFullRotationAsRot4()
    {
      return thing.BaseFullRotation().AsRot4Force();
    }

    public Rot8 BaseFullRotationDoor()
    {
      if (!thing.IsOnNonFocusedVehicleMapOf(out var vehicle)) return thing.Rotation;
      var rot = new Rot8(thing.Rotation).Rotated(vehicle.FullRotation);
      return rot.FacingCell.z < 0 ? rot.Opposite : rot;
    }
    
    public bool TryGetDrawPos(ref Vector3 result)
    {
      if (VehicleSectionLayerManager.CacheMode)
      {
        if (thing.def.category == ThingCategory.Item &&
            thing.GetSlotGroup()?.parent is Building_Hatch)
        {
          result = Vector3.negativeInfinity;
          return true;
        }

        if (thing is Building_GravshipWheel { CacheMode: false })
        {
          result = thing.DrawPos;
          return true;
        }

        return false;
      }

      var map = thing.Map;
      if (map.IsNonFocusedVehicleMapOf(out var vehicle))
      {
        if (!VehiclePawnWithMapCache.CacheMode)
        {
          var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(map);
          if (!component.cachedDrawPos.TryGetValue(thing, out result))
          {
            try
            {
              VehiclePawnWithMapCache.CacheMode = true;
              result = thing.DrawPos.ToBaseMapCoord(vehicle);
              if (AsAboveSoBelow.Active &&
                  AsAboveSoBelow.CompOf(map) is { } comp &&
                  AsAboveSoBelow.Banded(comp) &&
                  AsAboveSoBelow.CurrentBand(map) > AsAboveSoBelow.BandOf(comp, thing.Position))
              {
                result.y = AltitudeLayer.Terrain.AltitudeFor().YOffsetFull(vehicle);
              }
              component.cachedDrawPos[thing] = result;
            }
            finally
            {
              VehiclePawnWithMapCache.CacheMode = false;
            }
          }
          return true;
        }
      }
      return false;
    }
    
    public void VirtualMapTransfer(Map map)
    {
      if (thing is not null && map is not null)
        VirtualTeleporter.mapIndexOrState(thing) = (sbyte)map.Index;
    }

    public void VirtualMapTransfer(Map map, IntVec3 c)
    {
      if (thing is not null)
      {
        if (map is not null)
          VirtualTeleporter.mapIndexOrState(thing) = (sbyte)map.Index;
        thing.SetPositionDirect(c);
      }
    }

    public CellRect MovedOccupiedDrawRect()
    {
      var drawSize = thing.DrawSize;
      return GenAdj.OccupiedRect(thing.PositionOnBaseMap, thing.BaseRotation(), new IntVec2(Mathf.CeilToInt(drawSize.x), Mathf.CeilToInt(drawSize.y)));
    }

    public Rot4 RotationForPrint()
    {
      var rot = thing.Rotation;

      if (VehicleSectionLayerManager.RotForPrint != Rot4.North && (thing.def.size.x != thing.def.size.z || thing.def.rotatable || (thing.def.graphicData?.drawRotated ?? false) && thing.Graphic is Graphic_Multi && !SameMaterialByRot()))
      {
        rot.AsInt += VehicleSectionLayerManager.RotForPrint.AsInt;
      }
      return rot;

      bool SameMaterialByRot()
      {
        var graphic = thing.Graphic;
        var rotation = new Rot4(rot.AsInt + VehicleSectionLayerManager.RotForPrint.AsInt);
        return graphic != null && graphic.MatAt(rot, thing) == graphic.MatAt(rotation, thing) && graphic.DrawOffset(rot) == graphic.DrawOffset(rotation);
      }
    }

    public CellRect MovedOccupiedRect()
    {
      var size = thing.def.size;
      return GenAdj.OccupiedRect(thing.PositionOnBaseMap, thing.BaseRotation(), new IntVec2(Mathf.CeilToInt(size.x), Mathf.CeilToInt(size.z)));
    }
  }

  extension(Pawn pawn)
  {
    public Map LordMapOrMapHeld => pawn.GetLord()?.Map ?? pawn.MapHeld;
  }

  extension(float original)
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float YOffsetFull(VehiclePawnWithMap vehicle)
    {
      return original / YCompress + vehicle.cachedDrawPos.y;
    }
    
    public float FlipAngle(VehiclePawn vehicle)
    {
      return VfVersional.Available && vehicle.VehicleGraphic.WestFlipped && vehicle.BaseRotation() == Rot4.West ? -original : original;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float YOffset()
    {
      return original / YCompress;
    }
  }

  extension(Vector3 original)
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 YOffset()
    {
      return original.WithY(original.y.YOffset());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector3 YOffsetFull(VehiclePawnWithMap vehicle)
    {
      return original.WithY(original.y.YOffsetFull(vehicle));
    }

    public Vector3 ToVehicleMapCoord()
    {
      if (Command_FocusVehicleMap.FocusedVehicle != null)
      {
        return original.ToVehicleMapCoord(Command_FocusVehicleMap.FocusedVehicle);
      }
      if (VehicleMapFramework.settings.drawPlanet && Find.CurrentMap.IsVehicleMapOf(out _) &&
          UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
      {
        return original.ToVehicleMapCoord(vehicle);
      }
      return original;
    }

    public Vector3 ToVehicleMapCoord(VehiclePawnWithMap vehicle)
    {
      var vehicleMapPos = vehicle.cachedDrawPos + OffsetFor(vehicle);
      var pivot = MapPivot(vehicle.VehicleMap);
      var drawPos = (original - vehicleMapPos).RotatedBy(-vehicle.FullAngle) + pivot;
      return drawPos;
    }

    public Vector3 ToNonFocusedThingMapCoord(Thing thing)
    {
      return thing.IsOnNonFocusedVehicleMapOf(out var vehicle) ? original.ToVehicleMapCoord(vehicle) : original;
    }

    public Vector3 ToBaseMapCoord()
    {
      if (Command_FocusVehicleMap.FocusedVehicle != null)
      {
        return original.ToBaseMapCoord(Command_FocusVehicleMap.FocusedVehicle).WithY(original.y);
      }
      if (VehicleMapFramework.settings.drawPlanet && Find.CurrentMap.IsVehicleMapOf(out _) &&
          UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
      {
        return original.ToBaseMapCoord(vehicle).WithY(original.y);
      }
      return original;
    }

    public Vector3 ToBaseMapCoord(Map map)
    {
      return map.IsNonFocusedVehicleMapOf(out var vehicle) ? original.ToBaseMapCoord(vehicle) : original;
    }

    public Vector3 ToBaseMapCoord(VehiclePawnWithMap vehicle)
    {
      var vehiclePos = vehicle.cachedDrawPos;
      var pivot = MapPivot(vehicle.VehicleMap, original.ToIntVec3());
      var drawPos = (original.YOffset() - pivot).RotatedBy(vehicle.FullAngle) + vehiclePos;
      drawPos += OffsetFor(vehicle);
      return drawPos;
    }

    public Vector3 ToBaseMapCoord(VehiclePawnWithMap vehicle, Rot8 rot)
    {
      var vehiclePos = vehicle.cachedDrawPos;
      var pivot = MapPivot(vehicle.VehicleMap, original.ToIntVec3());
      var drawPos = (original.YOffset() - pivot).RotatedBy(rot.AsAngle) + vehiclePos;
      drawPos += OffsetFor(vehicle, rot);
      return drawPos;
    }

    public bool TryGetVehicleMap(Map map, out VehiclePawnWithMap vehicle, VehicleMapFlag flag = VehicleMapFlag.StructureCells)
    {
      vehicle = null;
      if (map == null)
      {
        return false;
      }

      var isVehicleMap = map.IsVehicleMapOf(out var vehicle2);
      var vehicleCaravanOrStashedVehicle = vehicle2?.VehicleCaravanOrStashedVehicle;
      if (isVehicleMap && vehicleCaravanOrStashedVehicle is null && VehicleMapFramework.settings.drawPlanet &&
          original.TryGetVehicleMap(vehicle2, flag))
      {
        vehicle = vehicle2;
        return true;
      }

      var vehicles =
        isVehicleMap
          ? vehicleCaravanOrStashedVehicle.Vehicles.OfType<VehiclePawnWithMap>()
          : VehiclePawnWithMapCache.AllVehiclesOn(map);

      var distanceSquared = float.MaxValue;
      foreach (var vehicle3 in vehicles)
      {
        if (original.TryGetVehicleMap(vehicle3, flag))
        {
          var distanceSquared2 = (vehicle3.cachedDrawPos - original).MagnitudeHorizontalSquared();
          if (distanceSquared2 < distanceSquared)
          {
            distanceSquared = distanceSquared2;
            vehicle = vehicle3;
          }
        }
      }
      return vehicle is not null;
    }

    public bool TryGetVehicleMap(VehiclePawnWithMap vehicle, VehicleMapFlag flag = VehicleMapFlag.StructureCells)
    {
      var rect = new Rect(0f, 0f, vehicle.MapSize.x, vehicle.MapSize.z);
      var vector = ToVehicleMapCoordLocal(original, vehicle);
      if (!rect.Contains(new Vector2(vector.x, vector.z)))
      {
        return false;
      }

      var intVec = vector.ToIntVec3();
      if (!intVec.InBounds(vehicle.VehicleMap))
        return false;
      if (!vehicle.ImpassableCellGrid[intVec])
        return true;
      var isEmptyStructureCell = vehicle.EmptyStructureGrid[intVec];
      var isExpandableCell = vehicle.ExpandableGrid[intVec];
      var isOutOfBoundsCell = vehicle.OutOfBoundsGrid[intVec];
      if ((flag & VehicleMapFlag.StructureCells) > 0 && !isEmptyStructureCell &&
          !isExpandableCell && !isOutOfBoundsCell)
        return true;
      if ((flag & VehicleMapFlag.ExpandableCells) > 0 && isExpandableCell)
        return true;
      return (flag & VehicleMapFlag.OutOfBoundsCells) > 0 && isOutOfBoundsCell;
      
      static Vector3 ToVehicleMapCoordLocal(Vector3 o, VehiclePawnWithMap v)
      {
        var vehicleMapPos = v.cachedDrawPos + OffsetFor(v);
        var mapSize = v.MapSize;
        var pivot = new Vector3(mapSize.x / 2f, 0, mapSize.z / 2f);
        var drawPos = (o - vehicleMapPos).RotatedBy(-v.FullAngle) + pivot;
        return drawPos;
      }
    }

    public Vector3 ToThingBaseMapCoord(Thing thing)
    {
      return thing.IsOnVehicleMapOf(out var vehicle) ? original.ToBaseMapCoord(vehicle) : original;
    }
  }

  extension(IntVec3 original)
  {
    public IntVec3 ToBaseMapCoord(VehiclePawnWithMap vehicle)
    {
      var vehiclePos = vehicle.cachedExactPos;
      var pivot = MapPivot(vehicle.VehicleMap, original);
      var drawPos = (original.ToVector3Shifted() - pivot).RotatedBy(vehicle.FullAngle) + vehiclePos;
      drawPos += OffsetFor(vehicle);
      return drawPos.ToIntVec3();
    }

    public IntVec3 ToBaseMapCoord(Map map)
    {
      return map.IsVehicleMapOf(out var vehicle) ? original.ToBaseMapCoord(vehicle) : original;
    }

    public IntVec3 ToVehicleMapCoord(VehiclePawnWithMap vehicle)
    {
      var vehicleMapPos = vehicle.cachedExactPos + OffsetFor(vehicle);
      var mapSize = vehicle.MapSize;
      var pivot = new Vector3(mapSize.x / 2f, 0, mapSize.z / 2f);
      var drawPos = (original.ToVector3Shifted() - vehicleMapPos).RotatedBy(-vehicle.FullAngle) + pivot;
      return drawPos.ToIntVec3();
    }

    public IntVec3 ToThingMapCoord(Thing thing)
    {
      return original.ToVehicleMapCoord(thing.Map);
    }

    public IntVec3 ToVehicleMapCoord(Map map)
    {
      return map.IsVehicleMapOf(out var vehicle) ? original.ToVehicleMapCoord(vehicle) : original;
    }

    public IntVec3 ToThingBaseMapCoord(Thing thing)
    {
      return thing.IsOnVehicleMapOf(out var vehicle) ? original.ToBaseMapCoord(vehicle) : original;
    }

    public IntVec2 ToHitCell(VehiclePawnWithMap vehicle)
    {
      return (original.ToVector3Shifted() - OffsetFor(vehicle, Rot8.North)).ToIntVec3().ToIntVec2;
    }

    public IntVec3 ClosestEdgeCell(VehiclePawnWithMap vehicle)
    {
      if (vehicle.CachedMapEdgeCells.Count == 0) return IntVec3.Invalid;

      var cellOnVehicleMap = original.ToVehicleMapCoord(vehicle);
      var mapRect = vehicle.ValidMapRect.ExpandedBy(1);
      var root = mapRect.ClosestCellTo(cellOnVehicleMap);
      var radius = (mapRect.GetCorner(Rot4.North) - mapRect.GetCorner(Rot4.South)).LengthHorizontal;

      var pattern =
        GenRadialDirectional.PatternFor(cellOnVehicleMap, vehicle.ValidMapRect, 0f, radius, out var indexRange);
      for (var i = indexRange.min; i < indexRange.max; i++)
      {
        var cell = root + pattern[i];
        if (vehicle.CachedMapEdgeCells.Contains(cell))
          return cell;
      }

      return IntVec3.Invalid;
    }

    public IntVec3 ClosestWalkableEdgeCell(VehiclePawnWithMap vehicle, int districtID = -1)
    {
      if (vehicle.CachedWalkableMapEdgeCells.Count == 0) return IntVec3.Invalid;

      var cellOnVehicleMap = original.ToVehicleMapCoord(vehicle);
      var mapRect = vehicle.ValidMapRect.ExpandedBy(1);
      var root = mapRect.ClosestCellTo(cellOnVehicleMap);
      if (cellOnVehicleMap == root || vehicle.CachedWalkableMapEdgeCells.TryGetValue(root, out var district) &&
          (districtID == -1 || district.ID == districtID)) return root;
      var radius = (mapRect.GetCorner(Rot4.North) - mapRect.GetCorner(Rot4.South)).LengthHorizontal;

      var pattern =
        GenRadialDirectional.PatternFor(cellOnVehicleMap, vehicle.ValidMapRect, 0f, radius, out var indexRange);
      for (var i = indexRange.min; i < indexRange.max; i++)
      {
        var cell = root + pattern[i];
        if (vehicle.CachedWalkableMapEdgeCells.TryGetValue(cell, out district) &&
            (districtID == -1 || district.ID == districtID))
          return cell;
      }

      return IntVec3.Invalid;
    }
  }

  extension(ref LocalTargetInfo target)
  {
    public IntVec3 CellOnBaseMap()
    {
      return target.HasThing ? target.Thing.PositionOnBaseMap : target.Cell;
    }

    public IntVec3 CellOnBaseMapSpawned()
    {
      return target.HasThing ? target.Thing.PositionOnBaseMapSpawned : target.Cell;
    }
  }

  extension(TargetInfo target)
  {
    public IntVec3 CellOnGroundMap => target.HasThing
      ? target.Thing.PositionOnBaseMap
      : target.Map.IsVehicleMapOf(out var vehicle)
        ? target.Cell.ToBaseMapCoord(vehicle)
        : target.Cell;

    public Vector3 CenterVector3OnGroundMap
    {
      get
      {
        if (target.HasThing)
        {
          if (target.Thing.Spawned)
          {
            return target.Thing.DrawPos;
          }
          return target.Thing.DrawPosHeld ?? target.Thing.Position.ToVector3Shifted().ToThingBaseMapCoord(target.Thing);
        }
        return target.Cell.IsValid ? target.Cell.ToVector3Shifted().ToBaseMapCoord(target.Map) : default;
      }
    }
  }

  extension(ref TargetInfo target)
  {
    public IntVec3 CellOnBaseMap()
    {
      return target.HasThing
        ? target.Thing.PositionOnBaseMap
        : target.Map.IsVehicleMapOf(out var vehicle)
          ? target.Cell.ToBaseMapCoord(vehicle)
          : target.Cell;
    }

    public IntVec3 CellOnBaseMapSpawned()
    {
      return target.HasThing
        ? target.Thing.PositionOnBaseMapSpawned
        : target.Map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned
          ? target.Cell.ToBaseMapCoord(vehicle)
          : target.Cell;
    }
  }

  extension(ref GlobalTargetInfo target)
  {
    public IntVec3 CellOnBaseMap()
    {
      return target.Map.IsVehicleMapOf(out var vehicle) ? target.Cell.ToBaseMapCoord(vehicle) : target.Cell;
    }

    public IntVec3 CellOnBaseMapSpawned()
    {
      return target.Map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned ? target.Cell.ToBaseMapCoord(vehicle) : target.Cell;
    }

    public Map BaseMap()
    {
      return target.Map.BaseMap();
    }
  }

  extension(IntVec3 c)
  {
    public Rot4 DirectionToInsideMap(VehiclePawnWithMap vehicle)
    {
      return vehicle.ValidMapRect.GetClosestEdge(c).Opposite;
    }

    public Rot8 BaseFullDirectionToInsideMap(VehiclePawnWithMap vehicle)
    {
      var dir = c.DirectionToInsideMap(vehicle);
      var map = vehicle.VehicleMap;
      if (Find.CurrentMap != map || VehicleMapFramework.settings.drawPlanet)
      {
        return new Rot8(dir).Rotated(vehicle.FullRotation);
      }
      return dir;
    }
  }

  extension(VehiclePawn vehicle)
  {
    public float FullAngle => Ext_Math.RotateAngle(vehicle.FullRotation.AsAngle, vehicle.Transform.rotation);

    public Quaternion FullAngleQuat => Quaternion.AngleAxis(vehicle.FullAngle, Vector3.up);

    public float ExtraAngle =>
      Mathf.Repeat(vehicle.FullAngle - vehicle.FullRotation.RotForVehicleDraw().AsAngle, 360f);

    public int HalfLength()
    {
      return vehicle.VehicleDef.HalfLength();
    }

    public bool TryGetFullRotation(ref Rot8 rot)
    {
      var map = vehicle.Map;
      if (map.IsNonFocusedVehicleMapOf(out _))
      {
        var component = MapComponentCache<VehiclePawnWithMapCache>.GetComponent(map);
        if (!component.cachedFullRot.TryGetValue(vehicle, out rot))
        {
          rot = vehicle.BaseFullRotation();
          component.cachedFullRot[vehicle] = rot;
        }
        return true;
      }
      return false;
    }

    public Rot8 BaseFullRotation()
    {
      if (!vehicle.VehicleDef.graphicData.drawRotated)
      {
        return Rot8.North;
      }
      var rot = new Rot8(vehicle.Rotation, vehicle.Angle);
      if (vehicle.IsOnNonFocusedVehicleMapOf(out var vehicle2))
      {
        rot = rot.Rotated(vehicle2.FullRotation);
      }
      return rot;
    }
  }

  extension(IntVec3 c)
  {
    public Pawn GetFirstPawnAcrossMaps(Map map)
    {
      var thingList = c.GetThingListAcrossMaps(map);
      foreach (var t in thingList)
      {
        if (t is Pawn result)
        {
          return result;
        }
      }

      return null;
    }

    public Thing GetCoverOnThingMap(Map map, Thing thing)
    {
      var thingMap = thing?.MapHeld;
      if (thingMap == null) return c.GetCover(map);
      var c2 = c.ToBaseMapCoord(thingMap);
      return c2.InBounds(thingMap) ? c2.GetCover(thingMap) : c.GetCover(map);
    }

    public bool RoofedAcrossMaps(Map map)
    {
      if (c.Roofed(map))
      {
        return true;
      }
      if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
      {
        return c.ToBaseMapCoord(vehicle).Roofed(vehicle.Map);
      }
      var vehicle2 = map.GetCachedMapComponent<VehicleMapGrid>().VehicleAt(c);
      return vehicle2 != null && c.ToVehicleMapCoord(vehicle2).Roofed(vehicle2.VehicleMap);
    }
  }
}

[Flags]
public enum VehicleMapFlag
{
  None = 0,
  StructureCells = 1 << 0,
  ExpandableCells = 1 << 1,
  OutOfBoundsCells = 1 << 2,
  All = 0b111
}
