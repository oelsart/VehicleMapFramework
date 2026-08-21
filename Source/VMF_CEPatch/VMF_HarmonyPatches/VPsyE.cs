using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CombatExtended;
using CombatExtended.Compatibility;
using HarmonyLib;
using UnityEngine;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_CE_VPsyECompat
{
  static Patches_CE_VPsyECompat()
  {
    if (VPsyE)
    {
      VMF_Harmony.PatchCategory(PatchCategories.CombatExtendedVPsyECompat);

      try
      {
        var method = AccessTools.Method(typeof(VanillaPsycastExpanded), "ShieldZones");
        var func = AccessTools.MethodDelegate<Func<Thing, IEnumerable<IEnumerable<IntVec3>>>>(method);
        Patch_BlockerRegistry_ShieldZonesCallback.Callbacks.Add(func);
      }
      catch (Exception ex)
      {
        VMF_Log.Error($"Could not register VPsyE ShieldZones callback for CE.\n{ex}");
      }
      Patch_BlockerRegistry_ImpactSomethingCallback.Callbacks.Add(
        Patch_VanillaPsycastExpanded_ImpactSomething.ImpactSomething);
      Patch_BlockerRegistry_CheckCellForCollisionCallback.Callbacks.Add(
        Patch_VanillaPsycastExpanded_Hediff_Overshield_InterceptCheck.Hediff_Overshield_InterceptCheck);
      Patch_BlockerRegistry_CheckForCollisionBetweenCallback.Callbacks.Add(
        Patch_VanillaPsycastExpanded_AOE_CheckIntercept.AOE_CheckIntercept);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedVPsyECompat)]
[HarmonyPatch(typeof(VanillaPsycastExpanded), nameof(VanillaPsycastExpanded.AOE_CheckIntercept))]
[PatchLevel(Level.Mandatory)]
public static class Patch_VanillaPsycastExpanded_AOE_CheckIntercept
{
  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  public static bool AOE_CheckIntercept(ProjectileCE projectile, Vector3 from, Vector3 newExactPos)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      return instructions.MethodReplacer(
        (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap),
        (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
    }
  }
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedVPsyECompat)]
[HarmonyPatch(typeof(VanillaPsycastExpanded), nameof(VanillaPsycastExpanded.Hediff_Overshield_InterceptCheck))]
[PatchLevel(Level.Mandatory)]
public static class Patch_VanillaPsycastExpanded_Hediff_Overshield_InterceptCheck
{
  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  [HarmonyPriority(Priority.High)]
  public static bool Hediff_Overshield_InterceptCheck(ProjectileCE projectile, IntVec3 cell, Thing launcher)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedVPsyECompat)]
[HarmonyPatch(typeof(VanillaPsycastExpanded), "ImpactSomething")]
[PatchLevel(Level.Mandatory)]
public static class Patch_VanillaPsycastExpanded_ImpactSomething
{
  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  [HarmonyPriority(Priority.High)]
  public static bool ImpactSomething(ProjectileCE projectile, Thing launcher)
  {
    _ = Transpiler(null);
    throw new NotImplementedException();

    IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
      var m_Hediff_Overshield_InterceptCheck = ((Delegate)VanillaPsycastExpanded.Hediff_Overshield_InterceptCheck).Method;
      var m_Hediff_Overshield_InterceptCheck_Reverse =
        ((Delegate)Patch_VanillaPsycastExpanded_Hediff_Overshield_InterceptCheck.Hediff_Overshield_InterceptCheck).Method;
      var g_ExactPosition = AccessTools.PropertyGetter(typeof(ProjectileCE), nameof(ProjectileCE.ExactPosition));
      var m_ExactPositionVCoord = ((Delegate)ExactPositionVehicleMapCoord).Method;
      return instructions.MethodReplacer(
        (m_Hediff_Overshield_InterceptCheck, m_Hediff_Overshield_InterceptCheck_Reverse),
        (g_ExactPosition, m_ExactPositionVCoord));
    }
  }

  private static Vector3 ExactPositionVehicleMapCoord(ProjectileCE instance)
  {
    return instance.TargetMapOrThingMap.IsVehicleMapOf(out var vehicle)
      ? instance.ExactPosition.ToVehicleMapCoord(vehicle)
      : instance.ExactPosition;
  }
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedVPsyECompat)]
[HarmonyPatch(typeof(VanillaPsycastExpanded), "OnIntercepted")]
[PatchLevel(Level.Cautious)]
public static class Patch_VanillaPsycastExpanded_OnIntercepted
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}
