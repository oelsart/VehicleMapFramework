using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_HaulersDream
{
  static Patches_HaulersDream()
  {
    if (HaulersDream.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.HaulersDream);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.BulkHaul", "TryBuildBulkJob")]
[PatchLevel(Level.Sensitive)]
public static class Patch_BulkHaul_TryBuildBulkJob
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .InsertAndAdvance(CodeInstruction.LoadArgument(1))
      .Set(OpCodes.Call, ((Delegate)ThingMapOrPawnMap).Method)
      .InstructionEnumeration();
  }

  private static Map ThingMapOrPawnMap(Pawn pawn, Thing primary) => primary.Map ?? pawn.Map;
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.BulkHaul", "HasPotentialBulkWork")]
[PatchLevel(Level.Cautious)]
public static class Patch_BulkHaul_HasPotentialBulkWork
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
  }
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.BulkHaul", "BuildBulkJob")]
[PatchLevel(Level.Cautious)]
public static class Patch_BulkHaul_BuildBulkJob
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
  }
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.BulkHaul", "TakeNearestEligible")]
[PatchLevel(Level.Cautious)]
public static class Patch_BulkHaul_TakeNearestEligible
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
  }
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.BulkHaul", "BuildPickUpJob")]
[PatchLevel(Level.Cautious)]
public static class Patch_BulkHaul_BuildPickUpJob
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
  }
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.BulkHaul", "BuildKeepFromContainerJob")]
[PatchLevel(Level.Cautious)]
public static class Patch_BulkHaul_BuildKeepFromContainerJob
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMapOrCaravan_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.Patch_JobDriver_HaulToCell_NoCellReservation", "Prefix")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Patch_JobDriver_HaulToCell_NoCellReservation_Prefix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .InsertAndAdvance(CodeInstruction.LoadArgument(0))
      .Set(OpCodes.Call, ((Delegate)TargetMap).Method)
      .InstructionEnumeration();
  }

  private static Map TargetMap(Pawn pawn, JobDriver driver) => driver.job?.globalTarget.Map ?? pawn.TargetMapOrThingMap;
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.StorageCommitments", "FreeUnitsFor")]
[HarmonyPatch([typeof(Pawn), typeof(ISlotGroup), typeof(ThingDef), typeof(Thing), typeof(bool)],
  [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out])]
[PatchLevel(Level.Cautious)]
public static class Patch_StorageCommitments_FreeUnitsFor
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrPawnMap);
  }
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.StorageEvidence", "AddCarriedHaul")]
public static class Patch_StorageEvidence_AddCarriedHaul
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrPawnMap);
  }
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.Patch_HaulToCellStorageJob_ClampToCommitments", "Postfix")]
[PatchLevel(Level.Cautious)]
public static class Patch_Patch_HaulToCellStorageJob_ClampToCommitments_Postfix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
  }
}

[HarmonyPatchCategory(PatchCategories.HaulersDream)]
[HarmonyPatch("HaulersDream.Patch_CarryHauledThingToCell_ReRoute", "ReRouteIfDestinationFilled")]
public static class Patch_Patch_CarryHauledThingToCell_ReRoute_ReRouteIfDestinationFilled
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrPawnMap);
  }
}