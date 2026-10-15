using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

// AllInventoryItemsに車両マップの物を含める
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
        if (thing.IsOnVehicleMap)
        {
          var count2 = Math.Min(count, thing.stackCount);
          count -= count2;
          var thing2 = thing.SplitOff(count2);
          ___caravan.AddPawnOrItem(thing2, false);
        }
      }
    }
  }
}

[HarmonyPatch(typeof(CaravanEnterMapUtility), "FindNearEdgeCell")]
[PatchLevel(Level.Safe)]
public static class Patch_CaravanEnterMapUtility_FindNearEdgeCell
{
  public static bool Prefix(Map map, Predicate<IntVec3> extraCellValidator, ref IntVec3 __result)
  {
    if (!map.IsVehicleMapOf(out var vehicle)) return true;

    var hostFaction = map.ParentFaction;
    if (VehicleMapCellFinder.TryFindRandomEdgeCellWith(
          x => BaseValidator(x) && (extraCellValidator is null || extraCellValidator(x)) &&
               ((hostFaction is not null && map.reachability.CanReachFactionBase(x, hostFaction)) ||
                (hostFaction is null && map.reachability.CanReachBiggestMapEdgeDistrict(x))), vehicle, out var intVec) ||
        extraCellValidator is not null && VehicleMapCellFinder.TryFindRandomEdgeCellWith(
          x => BaseValidator(x) && extraCellValidator(x), vehicle, out intVec) ||
        VehicleMapCellFinder.TryFindRandomEdgeCellWith(BaseValidator, vehicle, out intVec))
    {
      __result = CellFinder.RandomClosewalkCellNear(intVec, map, 5);
      return false;
    }

    Log.Warning("Could not find any valid edge cell.");
    __result = CellFinder.RandomCell(map);
    return false;

    bool BaseValidator(IntVec3 x) => x.Standable(map);
  }
}