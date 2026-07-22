using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using Vehicles;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

//車上マップにそれぞれVirtualMapTransferしてColonyThingsWillingToBuyを集める
[HarmonyPatch(typeof(Pawn), nameof(Pawn.ColonyThingsWillingToBuy))]
[PatchLevel(Level.Safe)]
public static class Patch_Pawn_ColonyThingsWillingToBuy
{
  public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, Pawn playerNegotiator, Pawn __instance)
  {
    if (values != null)
    {
      foreach (var thing in values)
      {
        yield return thing;
      }
    }

    var maps = __instance.Map.BaseMapAndVehicleMaps(false);
    var departMap = __instance.Map;
    CrossMapReachabilityUtility.DepartMapGlobal = departMap;
    try
    {
      foreach (var map in maps)
      {
        __instance.VirtualMapTransfer(map);
        foreach (var thing in __instance.trader.ColonyThingsWillingToBuy(playerNegotiator))
        {
          yield return thing;
        }
      }
    }
    finally
    {
      __instance.VirtualMapTransfer(departMap);
      CrossMapReachabilityUtility.DepartMapGlobal = null;
    }
  }
}

// Experimental: AllInventoryItemsに車両マップの物を含める
[HarmonyPatch(typeof(CaravanInventoryUtility), nameof(CaravanInventoryUtility.AllInventoryItems))]
[HarmonyPriority(Priority.High)]
public static class Patch_CaravanInventoryUtility_AllInventoryItems
{
  [PatchLevel(Level.Mandatory)]
  [HarmonyReversePatch]
  [MethodImpl(MethodImplOptions.NoInlining)]
  public static List<Thing> AllInventoryItems(Caravan caravan) => throw new NotImplementedException();

  [PatchLevel(Level.Safe)]
  public static void Postfix(Caravan caravan, List<Thing> __result)
  {
    if (!VehicleMapFramework.settings.includeMapThings || caravan is not VehicleCaravan vehicleCaravan) return;
    __result.AddRange(vehicleCaravan.Vehicles.OfType<VehiclePawnWithMap>()
      .SelectMany(v => v.VehicleMap.listerThings
        .GetAllThings(t => t.def.category == ThingCategory.Item || t is Pawn { IsSlaveOfColony: true })));
  }
}

[HarmonyPatch(typeof(CaravanInventoryUtility), nameof(CaravanInventoryUtility.GetOwnerOf))]
[PatchLevel(Level.Safe)]
public static class Patch_CaravanInventoryUtility_GetOwnerOf
{
  public static bool Prefix(Thing item, ref Pawn __result)
  {
    if (item.IsOnVehicleMapOf(out var vehicle))
    {
      __result = vehicle;
      return false;
    }

    return true;
  }
}

[HarmonyPatch(typeof(Caravan_BedsTracker), "GetUsableBeds")]
[PatchLevel(Level.Cautious)]
[HarmonyPriority(Priority.Low)]
public static class Patch_Caravan_BedsTracker_GetUsableBeds
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_AllInventoryItems,
      CachedMethodInfo.m_AllInventoryItems_Original);
  }
}

[HarmonyPatch(typeof(Dialog_SplitCaravan), "TrySplitCaravan")]
[PatchLevel(Level.Safe)]
public static class Patch_Dialog_SplitCaravan_TrySplitCaravan
{
  public static void Prefix(Caravan ___caravan, List<TransferableOneWay> ___transferables)
  {
    for (var i = ___transferables.Count - 1; i >= 0; i--)
    {
      var transferable = ___transferables[i];
      if (transferable.CountToTransfer <= 0) continue;
      var count = transferable.CountToTransfer;

      foreach (var thing in transferable.things)
      {
        if (thing.IsOnVehicleMapOf(out _))
        {
          var count2 = Math.Min(count, thing.stackCount);
          count -= count2;
          var thing2 = thing.SplitOff(count2);
          ___caravan.AddPawnOrItem(thing2, false);
          ___transferables.RemoveAt(i);
        }
      }
    }
  }
}

//キャラバンのメンバーにVehiclePawnWithMapが含まれる場合そのVehicleMap上の物も取引できるようにする
[HarmonyPatch(typeof(Caravan), nameof(Caravan.ColonyThingsWillingToBuy))]
[PatchLevel(Level.Safe)]
public static class Patch_Caravan_ColonyThingsWillingToBuy
{
  public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, Pawn playerNegotiator)
  {
    var vehicles = (playerNegotiator.GetCaravan()?.PawnsListForReading?.OfType<VehiclePawnWithMap>() ??
                    playerNegotiator.GetVehicleCaravan()?.Vehicles?.OfType<VehiclePawnWithMap>())?.ToList();

    if (values != null)
    {
      foreach (var thing in values)
      {
        yield return thing;
      }
    }

    if (VehicleMapFramework.settings.includeMapThings)
      yield break;

    if (!vehicles.NullOrEmpty())
    {
      foreach (var thing in
               vehicles!.SelectMany(vehicle => vehicle.ColonyThingsWillingToBuyOnVehicle(playerNegotiator)))
      {
        yield return thing;
      }
    }
    else if (playerNegotiator is VehiclePawnWithMap vehicle2)
    {
      foreach (var thing in vehicle2.ColonyThingsWillingToBuyOnVehicle(playerNegotiator))
      {
        yield return thing;
      }
    }
  }
}

[HarmonyPatch(typeof(Settlement), nameof(Settlement.ColonyThingsWillingToBuy))]
[PatchLevel(Level.Safe)]
public static class Patch_Settlement_ColonyThingsWillingToBuy
{
  public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, Pawn playerNegotiator) =>
    Patch_Caravan_ColonyThingsWillingToBuy.Postfix(values, playerNegotiator);
}

//トレードビーコンの検索時車上マップのビーコンを含める
[HarmonyPatch(typeof(Building_OrbitalTradeBeacon), nameof(Building_OrbitalTradeBeacon.AllPowered))]
[PatchLevel(Level.Safe)]
public static class Patch_Building_OrbitalTradeBeacon_AllPowered
{
  public static IEnumerable<Building_OrbitalTradeBeacon> Postfix(IEnumerable<Building_OrbitalTradeBeacon> values,
    Map map)
  {
    foreach (var b in values) yield return b;

    var maps = map.BaseMapAndVehicleMaps(false);
    var buildings = maps.SelectMany(m => m.listerBuildings.AllBuildingsColonistOfClass<Building_OrbitalTradeBeacon>()
      .Where(b =>
      {
        var comp = b.GetComp<CompPowerTrader>();
        return comp == null || comp.PowerOn;
      }));

    foreach (var b in buildings) yield return b;
  }
}

//ビーコンを含めただけでは売却可能なポーンが追加されないのでこれも追加する
[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.ColonyThingsWillingToBuy))]
[PatchLevel(Level.Safe)]
public static class Patch_TradeShip_ColonyThingsWillingToBuy
{
  public static IEnumerable<Thing> Postfix(IEnumerable<Thing> values, Pawn playerNegotiator)
  {
    var result = values.ToList();
    var maps = playerNegotiator.Map.BaseMapAndVehicleMaps(false);

    foreach (var map in maps)
    {
      result.AddRange(TradeUtility.AllSellableColonyPawns(map, false));
    }

    return result;
  }
}

//車上マップのビーコンが含まれているのでMapは引数じゃなくそこから取る
//c.GetThingList(map) -> c.GetThingList(building_OrbitalTradeBeacon.Map)
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_TradeUtility_AllLaunchableThingsForTrade
{
  private static MethodInfo TargetMethod()
  {
    var type = AccessTools.FirstInner(typeof(TradeUtility), t => t.Name.Contains("AllLaunchableThingsForTrade"));
    return AccessTools.Method(type, "MoveNext");
  }

  //ローカル変数からビーコンを取ろうとするとforeachのMoveNextタイミングによってなんかがなんかしてたまにnullになるのでstaticフィールドでやりとりします
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var m_GetThingList = ((Delegate)GridsUtility.GetThingList).Method;
    foreach (var instruction in instructions)
    {
      if (instruction.Calls(m_GetThingList))
      {
        yield return ((Delegate)BuildingMap).Method.CallInstruction;
      }

      yield return instruction;

      if (instruction.opcode == OpCodes.Stloc_2)
      {
        yield return CodeInstruction.LoadLocal(2);
        yield return CodeInstruction.StoreField(typeof(Patch_TradeUtility_AllLaunchableThingsForTrade), nameof(beacon));
      }
    }
  }

  [UsedImplicitly] public static Building_OrbitalTradeBeacon beacon;

  private static Map BuildingMap(Map map)
  {
    return beacon?.Map ?? map;
  }
}

//posのInBoundsチェックはやってるのに範囲内のセルのInBoundsはチェックしてないのぉ？なんでよ……まあ建築限界線があるからだろうけども。チェックを追加します。
[HarmonyPatch(typeof(Building_OrbitalTradeBeacon), nameof(Building_OrbitalTradeBeacon.TradeableCellsAround))]
[PatchLevel(Level.Safe)]
public static class Patch_Building_OrbitalTradeBeacon_TradeableCellsAround
{
  public static void Postfix(Map map, List<IntVec3> __result)
  {
    __result.RemoveAll(c => !c.InBounds(map));
  }
}

//map.thingGrid.ThingsAt(c) -> building_OrbitalTradeBeacon.Map.thingGrid.ThingsAt(c)
[HarmonyPatch(typeof(TradeUtility), nameof(TradeUtility.LaunchThingsOfType))]
[PatchLevel(Level.Sensitive)]
public static class Patch_TradeUtility_LaunchThingsOfType
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var f_Map_thingGrid = AccessTools.Field(typeof(Map), nameof(Map.thingGrid));
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_Map_thingGrid)) - 1;

    codes.RemoveAt(pos);
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadLocal(2),
      new CodeInstruction(OpCodes.Callvirt,
        AccessTools.PropertyGetter(typeof(IEnumerator<Building_OrbitalTradeBeacon>), nameof(IEnumerator.Current))),
      new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_Map)
    ]);
    return codes;
  }
}

//CommsConsoleを車両マップに建てている場合でもトレーダー船が現れるようにする
[HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival), "TryExecuteWorker")]
[PatchLevel(Level.Sensitive)]
public static class Patch_IncidentWorker_OrbitalTraderArrival_TryExecuteWorker
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    foreach (var instruction in instructions)
    {
      yield return instruction;

      if (instruction.LoadsField(AccessTools.Field(typeof(ListerBuildings),
            nameof(ListerBuildings.allBuildingsColonist))))
      {
        yield return CodeInstruction.LoadArgument(1);
        yield return ((Delegate)AddBuildings).Method.CallInstruction;
      }
    }
  }

  private static readonly List<Building> buildings = [];

  private static List<Building> AddBuildings(List<Building> list, IncidentParms parms)
  {
    var allVehicles = VehiclePawnWithMapCache.AllVehiclesOn((Map)parms.target);
    if (allVehicles.NullOrEmpty()) return list;

    buildings.Clear();
    for (var i = 0; i < list.Count; i++)
    {
      buildings.Add(list[i]);
    }

    foreach (var vehicle in allVehicles)
    {
      var allBuildingsColonist = vehicle.VehicleMap.listerBuildings.allBuildingsColonist;
      for (var i = 0; i < allBuildingsColonist.Count; i++)
      {
        buildings.Add(allBuildingsColonist[i]);
      }
    }

    return buildings;
  }
}