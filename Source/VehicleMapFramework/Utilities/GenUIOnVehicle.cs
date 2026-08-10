using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using Debug = System.Diagnostics.Debug;

namespace VehicleMapFramework;

public static class GenUIOnVehicle
{
  private static readonly List<Thing> cellThings = [with(32)];

  public static VehiclePawnWithMap vehicleForSelector;

  public static List<Thing> ThingsUnderMouse(Vector3 clickPos, float pawnWideClickRadius,
    TargetingParameters clickParams, ITargetingSource source)
  {
    return ThingsUnderMouse(clickPos, pawnWideClickRadius, clickParams, source, vehicleForSelector);
  }

  public static List<Thing> ThingsUnderMouse(Vector3 clickPos, float pawnWideClickRadius,
    TargetingParameters clickParams, ITargetingSource source, VehiclePawnWithMap vehicle)
  {
    var mouseMapPosition = UI.MouseMapPosition();
    var intVec = IntVec3.FromVector3(clickPos);
    var map = vehicle is not null ? vehicle.CurrentLevel : Find.CurrentMap;
    var list = new List<Thing>();
    var allPawnsSpawned = Find.CurrentMap.mapPawns.AllPawnsSpawned;
    foreach (var pawn in allPawnsSpawned)
    {
      if (pawn == vehicle) continue;
      if (!((pawn.DrawPos - mouseMapPosition).MagnitudeHorizontal() < 0.4f) ||
          !clickParams.CanTarget(pawn, source)) continue;
      list.Add(pawn);
      list.AddRange(ContainingSelectionUtility.SelectableContainedThings(pawn));
    }

    list.Sort(CompareThingsByDistanceToMousePointer);
    cellThings.Clear();
    foreach (var thing4 in map.thingGrid.ThingsAt(intVec))
    {
      if (list.Contains(thing4) || !clickParams.CanTarget(thing4, source)) continue;
      cellThings.Add(thing4);
      cellThings.AddRange(ContainingSelectionUtility.SelectableContainedThings(thing4));
    }

    var adjacentCells = GenAdj.AdjacentCells;
    foreach (var t in adjacentCells)
    {
      var c = t + intVec;
      if (!c.InBounds(map) || c.GetItemCount(map) <= 1) continue;
      foreach (var thing2 in map.thingGrid.ThingsAt(c))
      {
        if (thing2.def.category == ThingCategory.Item &&
            (thing2.TrueCenter() - mouseMapPosition).MagnitudeHorizontalSquared() <= 0.25f && !list.Contains(thing2) &&
            clickParams.CanTarget(thing2, source))
        {
          cellThings.Add(thing2);
        }
      }
    }

    var list2 = map.listerThings.ThingsInGroup(ThingRequestGroup.WithCustomRectForSelector);
    foreach (var thing3 in list2.Where(thing3 =>
               thing3.CustomRectForSelector != null && thing3.CustomRectForSelector.Value.Contains(intVec) &&
               !list.Contains(thing3) && clickParams.CanTarget(thing3, source)))
    {
      cellThings.Add(thing3);
    }

    cellThings.Sort(CompareThingsByDrawAltitudeOrDistToItem);
    list.AddRange(cellThings);
    cellThings.Clear();
    foreach (var pawn2 in allPawnsSpawned)
    {
      if (pawn2 == vehicle) continue;
      if ((pawn2.DrawPos - mouseMapPosition).MagnitudeHorizontal() < pawnWideClickRadius &&
          clickParams.CanTarget(pawn2, source))
      {
        cellThings.Add(pawn2);
      }
    }

    cellThings.Sort(CompareThingsByDistanceToMousePointer);
    foreach (var t in cellThings.Where(t => !list.Contains(t)))
    {
      list.Add(t);
      list.AddRange(ContainingSelectionUtility.SelectableContainedThings(t));
    }

    list.RemoveAll(thing => !clickParams.CanTarget(thing, source));
    list.RemoveAll(thing => thing is Pawn pawn3 && pawn3.IsHiddenFromPlayer());
    list.Remove(vehicle);
    return list;

    int CompareThingsByDistanceToMousePointer(Thing a, Thing b)
    {
      Debug.Assert(a.DrawPosHeld != null, "a.DrawPosHeld != null");
      Debug.Assert(b.DrawPosHeld != null, "b.DrawPosHeld != null");

      var num = (a.DrawPosHeld!.Value - mouseMapPosition).MagnitudeHorizontalSquared();
      var num2 = (b.DrawPosHeld!.Value - mouseMapPosition).MagnitudeHorizontalSquared();
      if (num < num2)
      {
        return -1;
      }

      return Mathf.Approximately(num, num2) ? b.Spawned.CompareTo(a.Spawned) : 1;
    }

    int CompareThingsByDrawAltitudeOrDistToItem(Thing A, Thing B)
    {
      if (A.def.category == ThingCategory.Item && B.def.category == ThingCategory.Item)
      {
        return (A.TrueCenter() - mouseMapPosition).MagnitudeHorizontalSquared()
          .CompareTo((B.TrueCenter() - mouseMapPosition).MagnitudeHorizontalSquared());
      }

      var spawnedParentOrMe = A.SpawnedParentOrMe;
      var spawnedParentOrMe2 = B.SpawnedParentOrMe;
      return !Mathf.Approximately(spawnedParentOrMe.def.Altitude, spawnedParentOrMe2.def.Altitude)
        ? spawnedParentOrMe2.def.Altitude.CompareTo(spawnedParentOrMe.def.Altitude)
        : B.Spawned.CompareTo(A.Spawned);
    }
  }

  public static IEnumerable<LocalTargetInfo> TargetsAtMouse(TargetingParameters clickParams, bool thingsOnly = false,
    ITargetingSource source = null)
  {
    var clickPos = UI.MouseMapPosition();
    source?.Caster.TargetMap = Find.CurrentMap;

    if (!clickPos.TryGetVehicleMap(Find.CurrentMap, out var vehicle, VehicleMapFlag.None) ||
        source is not (Verb_Jump or Verb_CastAbilityJump or Verb_LaunchZipline))
      return TargetsAt(clickPos, clickParams, thingsOnly, source, vehicle, false);
    source.Caster.TargetMap = vehicle.VehicleMap;
    return TargetsAt(clickPos, clickParams, thingsOnly, source, vehicle);
  }

  public static IEnumerable<LocalTargetInfo> TargetsAt(Vector3 clickPos, TargetingParameters clickParams,
    bool thingsOnly, ITargetingSource source = null, bool convToVehicleMap = true)
  {
    return TargetsAt(clickPos, clickParams, thingsOnly, source, vehicleForSelector, convToVehicleMap);
  }

  public static IEnumerable<LocalTargetInfo> TargetsAt(Vector3 clickPos, TargetingParameters clickParams,
    bool thingsOnly, ITargetingSource source, VehiclePawnWithMap vehicle, bool convToVehicleMap = true)
  {
    var clickableList = vehicle != null
      ? ThingsUnderMouse(clickPos.ToVehicleMapCoord(vehicle), 0.8f, clickParams, source, vehicle)
      : GenUI.ThingsUnderMouse(clickPos, 0.8f, clickParams, source);
    var caster = source?.Caster;
    int num;
    for (var i = 0; i < clickableList.Count; i = num + 1)
    {
      if (clickableList[i] is VehiclePawn vehicle2 && vehicle2 == FloatMenuMakerMap.makingFor)
      {
        num = i;
        continue;
      }

      if (clickableList[i] is not Pawn pawn || !pawn.IsPsychologicallyInvisible() || caster == null ||
          caster.Faction == pawn.Faction)
      {
        yield return clickableList[i];
      }

      num = i;
    }

    if (thingsOnly) yield break;
    var intVec = (convToVehicleMap && vehicle != null)
      ? clickPos.ToVehicleMapCoord(vehicle).ToIntVec3()
      : clickPos.ToIntVec3();
    var map = (convToVehicleMap && vehicle != null) ? vehicle.VehicleMap : Find.CurrentMap;
    if (intVec.InBounds(map, clickParams.mapBoundsContractedBy) &&
        clickParams.CanTarget(new TargetInfo(intVec, map), source))
    {
      yield return intVec;
    }
  }
}