using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(StorytellerUtility), nameof(StorytellerUtility.DefaultThreatPointsNow))]
[PatchLevel(Level.Safe)]
public static class Patch_StorytellerUtility_DefaultThreatPointsNow
{
  public static void Prefix(ref IIncidentTarget target)
  {
    if (target is Map { ParentVehicle.ParentHolder: IIncidentTarget target2 })
      target = target2;
  }
}

[HarmonyPatch(typeof(Caravan), nameof(Caravan.PlayerWealthForStoryteller), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Caravan_PlayerWealthForStoryteller
{
  public static void Postfix(Caravan __instance, ref float __result)
  {
    if (!__instance.IsPlayerControlled)
      return;

    var pawnsList = __instance.PawnsListForReading;
    for (var i = 0; i < __instance.PawnsListForReading.Count; i++)
    {
      if (pawnsList[i] is VehiclePawnWithMap { Faction.IsPlayer: true } vehicle)
      {
        __result += vehicle.VehicleMap.PlayerWealthForStoryteller;
      }
    }
  }
}

[HarmonyPatch(typeof(Caravan), nameof(Caravan.PlayerPawnsForStoryteller), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_Caravan_PlayerPawnsForStoryteller
{
  public static IEnumerable<Pawn> Postfix(IEnumerable<Pawn> values)
  {
    foreach (var pawn in values)
    {
      if (pawn is VehiclePawnWithMap { Faction.IsPlayer: true } vehicle)
      {
        foreach (var pawn2 in vehicle.VehicleMap.PlayerPawnsForStoryteller)
        {
          yield return pawn2;
        }
      }

      yield return pawn;
    }
  }
}

// スポーンしている車両のPlayerWealthは地上マップのものに加算されており、キャラバン中の車両のものはパッチでキャラバンに加算されている
[HarmonyPatch(typeof(World), nameof(World.PlayerWealthForStoryteller), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_World_PlayerPawnsForStoryteller
{
  public static void Postfix(ref float __result)
  {
    var maps = Find.Maps;
    for (var i = 0; i < maps.Count; i++)
    {
      if (maps[i].IsVehicleMapOf(out var vehicle))
        __result -= vehicle.VehicleMap.PlayerWealthForStoryteller;
    }
  }
}