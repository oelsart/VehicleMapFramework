using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld.Planet;
using Verse;
using static VehicleMapFramework.ModCompat.VFESecurity;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatchCategory(PatchCategories.VFESecurity)]
[HarmonyPatch("VFESecurity.CompPointDefense", "FindTarget")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompPointDefense_FindTarget
{
  private static readonly List<Thing> tmpList = [];

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .Set(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .Advance()
      .RemoveInstruction()
      .Advance()
      .Set(OpCodes.Call, AccessTools.Method(typeof(Patch_CompPointDefense_FindTarget), nameof(ThingsInGroup)))
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
      .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMapSpawned)
      .Instructions();
  }

  private static List<Thing> ThingsInGroup(Map map, ThingRequestGroup req)
  {
    tmpList.Clear();
    tmpList.AddRangeFast(map.BaseMapAndVehicleMaps().SelectMany(m => m.listerThings.ThingsInGroup(req)));
    return tmpList;
  }
}

[HarmonyPatchCategory(PatchCategories.VFESecurity)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompPointDefense_FindTarget_Delegate
{
  private static MethodBase TargetMethod()
  {
    var type = GenTypes.GetTypeInAnyAssembly("VFESecurity.CompPointDefense", "VFESecurity");
    return AccessTools.GetDeclaredMethods(type).First(m => m.Name.Contains("<FindTarget>"));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.VFESecurity)]
[HarmonyPatch("VFESecurity.CompPointDefense", "TryIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPointDefense_TryIntercept
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
      .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.VFESecurity)]
[HarmonyPatch("VFESecurity.CompWorldArtillery", "CompTickInterval")]
[PatchLevel(Level.Safe)]
public static class Patch_CompWorldArtillery_CompTickInterval
{
  public static void Postfix(ThingComp __instance)
  {
    GlobalTargetInfo target;
    if (__instance.parent.IsOnVehicleMapOf(out var vehicle) && (target = worldTarget(__instance)).IsValid)
    {
      if (Find.WorldGrid.TraversalDistanceBetween(vehicle.Tile, target.Tile) < worldMapAttackRange(__instance.props)) return;

      worldTarget(__instance) = GlobalTargetInfo.Invalid;
    }
  }
}
