using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_Rimatomics
{
  static Patches_Rimatomics()
  {
    if (Rimatomics.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.Rimatomics);

      Patch_Projectile_CheckForFreeInterceptBetween.Prefixes.Add(
        Patch_Patch_HarmonyPatches_H_CheckForFreeInterceptBetween_Prefix.PrefixPatch);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch("Rimatomics.Building_Radar", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_Radar_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .AddAltitudeFor(out _, 0.1f)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch("Rimatomics.Building_ShieldArray", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_ShieldArray_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    return Patch_Building_Radar_DrawAt.Transpiler(instructions, generator);
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch("Rimatomics.MissileSilo", "DrawAt")]
[PatchLevel(Level.Cautious)]
public static class Patch_MissileSilo_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch("Rimatomics.Building_EnergyWeapon", "DrawAt")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_EnergyWeapon_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_OccupiedRect, CachedMethodInfo.m_MovedOccupiedRect);
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch("Rimatomics.Building_EnergyWeapon", "OrderAttack")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_EnergyWeapon_OrderAttack
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch("Rimatomics.Building_EnergyWeapon", "InRange")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_EnergyWeapon_InRange
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_RimatomicsVerb_TryCastShot
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    var type = GenTypes.GetTypeInAnyAssembly("Rimatomics.Verb_RimatomicsVerb", "Rimatomics");
    return type.AllSubclasses().Select(t => AccessTools.DeclaredMethod(t, "TryCastShot"))
      .WhereCallsMethod(
        CachedMethodInfo.g_Thing_Map,
        CachedMethodInfo.g_Thing_Position,
        CachedMethodInfo.g_LocalTargetInfo_Cell);
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    if (UnitTestDetector.IsTestingContext) return instructions;
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing));
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch("Rimatomics.Building_PPC", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_PPC_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_Building_Battery_DrawAt.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch("Rimatomics.CompRimatomicsShield", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompRimatomicsShield_PostDraw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IntVec3_ToVector3Shifted))
      .CreateLabelWithOffsets(1, out var label)
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .InsertAfterAndAdvance(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent)),
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2))
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_Altitudes_AltitudeFor))
      .CreateLabelWithOffsets(1, out var label2)
      .InsertAfter(
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Brfalse_S, label2),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_YOffsetFull))
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch("Rimatomics.CompRimatomicsShield", "CheckIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompRimatomicsShield_CheckIntercept
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing));
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn), typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool))]
[PatchLevel(Level.Safe)]
public static class Patch_GenSpawn_Spawn_Rimatomics
{
  public static void Prefix(Thing newThing, ref Map map)
  {
    var thingClass = newThing?.def?.thingClass;
    if (thingClass.SameOrSubclassOf(Rimatomics.BaseMissile))
    {
      map = map.BaseMap();
    }
  }
}

[HarmonyPatchCategory(PatchCategories.Rimatomics)]
[HarmonyPatch("Rimatomics.HarmonyPatches+H_CheckForFreeInterceptBetween", "Prefix")]
[PatchLevel(Level.Mandatory)]
public static class Patch_Patch_HarmonyPatches_H_CheckForFreeInterceptBetween_Prefix
{
  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  public static bool PrefixPatch(Projectile __instance, Vector3 lastExactPos, Vector3 newExactPos, ref bool __result)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
  }
}

// 描画上の制約によりプールは建設不可とする
// [HarmonyPatchCategory(PatchCategories.Rimatomics)]
// [HarmonyPatch("Rimatomics.Building_storagePool", "AllSlotCells")]
// [PatchLevel(Level.Safe)]
// public static class Patch_Building_storagePool_AllSlotCells
// {
//     public static bool Prefix(Thing __instance, ref IEnumerable<IntVec3> __result)
//     {
//         if (__instance.IsOnVehicleMapOf(out _))
//         {
//             __result = AllSlotCells(__instance);
//             return false;
//         }
//
//         return true;
//     }
//     
//     private static IEnumerable<IntVec3> AllSlotCells(Thing __instance)
//     {
//         switch (__instance.Rotation.AsInt)
//         {
//             case Rot4.NorthInt:
//                 yield return __instance.Position + new IntVec3(-1, 0, -2);
//                 yield return __instance.Position + new IntVec3(0, 0, -2);
//                 yield break;
//             case Rot4.SouthInt:
//                 yield return __instance.Position + new IntVec3(1, 0, 2);
//                 yield return __instance.Position + new IntVec3(0, 0, 2);
//                 yield break;
//             case Rot4.EastInt:
//                 yield return __instance.Position + new IntVec3(-2, 0, 1);
//                 yield return __instance.Position + new IntVec3(-2, 0, 0);
//                 yield break;
//             case Rot4.WestInt:
//                 yield return __instance.Position + new IntVec3(2, 0, 0);
//                 yield return __instance.Position + new IntVec3(2, 0, -1);
//                 yield break;
//         }
//     }
// }
//
// [HarmonyPatchCategory(PatchCategories.Rimatomics)]
// [HarmonyPatch("Rimatomics.Building_storagePool", "Print")]
// [PatchLevel(Level.Cautious)]
// public static class Patch_Building_storagePool_Print
// {
//     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
//     {
//         return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_RotationForPrint);
//     }
// }
//
// [HarmonyPatchCategory(PatchCategories.Rimatomics)]
// [HarmonyPatch("Rimatomics.Building_storagePool", "DrawAt")]
// [PatchLevel(Level.Cautious)]
// public static class Patch_Building_storagePool_DrawAt
// {
//     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
//     {
//         return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
//     }
// }
//
// [HarmonyPatchCategory(PatchCategories.Rimatomics)]
// [HarmonyPatch("Rimatomics.Building_storagePool", "drawPoolBit")]
// [PatchLevel(Level.Cautious)]
// public static class Patch_Building_storagePool_drawPoolBit
// {
//     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
//     {
//         return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseFullRotation_Thing)
//             .MethodReplacer(CachedMethodInfo.g_Rot4_AsQuat, CachedMethodInfo.m_Rot8_AsQuatRef);
//     }
// }
