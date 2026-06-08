using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CombatExtended;
using HarmonyLib;
using UnityEngine;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_CE_RimatomicsCompat
{
  static Patches_CE_RimatomicsCompat()
  {
    if (ModCompat.Rimatomics.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.CombatExtendedRimatomicsCompat);

      try
      {
        var method = AccessTools.Method("CombatExtended.Compatibility.Rimatomics:ShieldZonesCallback");
        var func = AccessTools.MethodDelegate<Func<Thing, IEnumerable<IEnumerable<IntVec3>>>>(method);
        if (func is null) throw new NullReferenceException();
        Patch_BlockerRegistry_ShieldZonesCallback.Callbacks.Add(func);
      }
      catch (Exception ex)
      {
        VMF_Log.Error($"Could not register Rimatomics ShieldZones callback for CE.\n{ex}");
      }
      Patch_BlockerRegistry_CheckForCollisionBetweenCallback.Callbacks.Add(
        Patch_Rimatomics_CheckForCollisionBetweenCallback.CheckForCollisionBetweenCallback);
      Patch_BlockerRegistry_ImpactSomethingCallback.Callbacks.Add(
        Patch_Rimatomics_ImpactSomethingCallback.ImpactSomethingCallback);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedRimatomicsCompat)]
[HarmonyPatch("CombatExtended.Compatibility.Rimatomics", "CheckForCollisionBetweenCallback")]
[PatchLevel(Level.Mandatory)]
public static class Patch_Rimatomics_CheckForCollisionBetweenCallback
{
  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  public static bool CheckForCollisionBetweenCallback(ProjectileCE projectile, Vector3 from, Vector3 to)
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

[HarmonyPatchCategory(PatchCategories.CombatExtendedRimatomicsCompat)]
[HarmonyPatch("CombatExtended.Compatibility.Rimatomics", "ImpactSomethingCallback")]
[PatchLevel(Level.Mandatory)]
public static class Patch_Rimatomics_ImpactSomethingCallback
{
  [MethodImpl(MethodImplOptions.NoInlining)]
  [HarmonyReversePatch]
  public static bool ImpactSomethingCallback(ProjectileCE projectile, Thing launcher)
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
