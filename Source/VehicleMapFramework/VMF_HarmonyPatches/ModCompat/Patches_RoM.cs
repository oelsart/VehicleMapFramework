using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_RoM
{
  static Patches_RoM()
  {
    if (RimWorldOfMagic)
    {
      VMF_Harmony.PatchCategory(PatchCategories.RimWorldOfMagic);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.RimWorldOfMagic)]
[HarmonyPatch("TorannMagic.TorannMagicMod+FloatMenuMakerMap_Patch", "Postfix")]
[PatchLevel(Level.Cautious)]
public static class Patch_FloatMenuMakerMap_Patch_Postfix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    codes.MatchStartForward(CodeMatch.IsLdarg(2), CodeMatch.Calls(CachedMethodInfo.g_Thing_Map));
    codes.Repeat(c => { c.Opcode = OpCodes.Ldarg_0; });
    return codes.Instructions();
  }
}

[HarmonyPatchCategory(PatchCategories.RimWorldOfMagic)]
[HarmonyPatch("TorannMagic.TorannMagicMod+TryFindShootLineFromTo_Base_Patch", "Prefix")]
[PatchLevel(Level.Sensitive)]
public static class Patch_TryFindShootLineFromTo_Base_Patch_Prefix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    var m_CanReachImmediate =
      ((Func<IntVec3, LocalTargetInfo, Map, PathEndMode, Pawn, bool>)ReachabilityImmediate.CanReachImmediate).Method;
    codes.MatchStartForward(CodeMatch.Calls(m_CanReachImmediate));
    codes.MatchStartBackwards(CodeMatch.IsLdarg(1));
    codes.InsertAfter(
      CodeInstruction.LoadArgument(0),
      CodeInstruction.LoadField(typeof(Verb), nameof(Verb.caster)),
      new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToThingMapCoord));
    return codes.Instructions().MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.RimWorldOfMagic)]
[HarmonyPatch("TorannMagic.TorannMagicMod+TryFindCastPosition_Base_Patch", "Prefix")]
[PatchLevel(Level.Cautious)]
public static class Patch_TryFindCastPosition_Base_Patch_Prefix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.RimWorldOfMagic)]
[HarmonyPatch("AbilityUser.AbilityUserMod", "ConfirmStillValid")]
[PatchLevel(Level.Cautious)]
public static class Patch_AbilityUserMod_ConfirmStillValid
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.RimWorldOfMagic)]
[HarmonyPatch("TorannMagic.AutoCast.Phase", "Evaluate")]
[PatchLevel(Level.Cautious)]
public static class Patch_Phase_Evaluate
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing));
  }
}

[HarmonyPatchCategory(PatchCategories.RimWorldOfMagic)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_UseAbility
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    var t_Verb_UseAbility = GenTypes.GetTypeInAnyAssembly("AbilityUser.Verb_UseAbility", "AbilityUser");
    var nestedTypes = AccessTools.InnerTypes(t_Verb_UseAbility);
    return t_Verb_UseAbility.AllSubclasses().Append(t_Verb_UseAbility).Concat(nestedTypes)
      .AsParallel()
      .SelectMany(t => t.GetDeclaredMethods())
      .WhereHasMethods(
        CachedMethodInfo.g_Thing_Position,
        CachedMethodInfo.g_Thing_PositionHeld,
        CachedMethodInfo.m_GetThingList,
        CachedMethodInfo.g_LocalTargetInfo_Cell,
        CachedMethodInfo.g_Thing_Map,
        CachedMethodInfo.g_Thing_MapHeld,
        CachedMethodInfo.m_OccupiedRect,
        CachedMethodInfo.m_BreadthFirstTraverse);
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    if (UnitTestDetector.IsTestingContext) return instructions;
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_PositionHeld, CachedMethodInfo.m_PositionHeldOnBaseMapSpawned),
      (CachedMethodInfo.m_OccupiedRect, CachedMethodInfo.m_MovedOccupiedRect),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap),
      (CachedMethodInfo.m_BreadthFirstTraverse, CachedMethodInfo.m_BreadthFirstTraverseAcrossMaps),
      (CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps));
  }
}

[HarmonyPatchCategory(PatchCategories.RimWorldOfMagic)]
[HarmonyPatch("AbilityUser.Verb_UseAbility", "UpdateTargets")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_UseAbility_UpdateTargets
{
  private static readonly List<Thing> tmpList = [];

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    var g_AllThings = AccessTools.PropertyGetter(typeof(ListerThings), nameof(ListerThings.AllThings));
    codes.MatchStartForward(CodeMatch.Calls(g_AllThings));
    codes.Repeat(c =>
    {
      c.InsertAfterAndAdvance(
        CodeInstruction.LoadArgument(0),
        ((Delegate)AddThingList).Method.CallInstruction);
    });
    return codes.Instructions();
  }

  private static List<Thing> AddThingList(List<Thing> list, Verb verb)
  {
    var vehicles = VehiclePawnWithMapCache.AllVehiclesOn(verb.caster.BaseMap());
    if (vehicles.Count == 0) return list;

    tmpList.Clear();
    tmpList.AddRange(list);
    tmpList.AddRange(vehicles.SelectMany(v => v.VehicleMap.listerThings.AllThings));
    return tmpList;
  }
}

[HarmonyPatchCategory(PatchCategories.RimWorldOfMagic)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_TMJobDriver_CastAbilityVerb_MakeNewToils
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes(GenTypes.GetTypeInAnyAssembly("TorannMagic.TMJobDriver_CastAbilityVerb", "TorannMagic"), t => { return t.GetDeclaredMethods().FirstOrDefault(m => m.Name == "MoveNext"); });
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var g_TargetLocA = AccessTools.PropertyGetter(typeof(JobDriver), "TargetLocA");
    var m_TargetLocAOnBaseMap = ((Delegate)TargetLocAOnBaseMap).Method;
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (g_TargetLocA, m_TargetLocAOnBaseMap),
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing));
  }

  private static IntVec3 TargetLocAOnBaseMap(JobDriver instance)
  {
    return instance.job.targetA.CellOnBaseMapSpawned();
  }
}
