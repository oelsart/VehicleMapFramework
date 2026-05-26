using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SmashTools;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_GiantImperialTurret
{
  static Patches_GiantImperialTurret()
  {
    if (GiantImperialTurret)
    {
      VMF_Harmony.PatchCategory(PatchCategories.GiantImperialTurret);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.GiantImperialTurret)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_TurretGunNonSnap_TryFindNewTarget
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    var type = AccessTools.TypeByName("BreadMoProjOffset.Building_TurretGunNonSnap");
    var methods = type.GetDeclaredMethods();
    return methods.Where(m => m.Name.Contains("<TryFindNewTarget>") || m.Name.Contains("<>"));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.GiantImperialTurret)]
[HarmonyPatch("BreadMoProjOffset.Building_TurretGunNonSnap", "IsValidTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGunNonSnap_IsValidTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
      .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.GiantImperialTurret)]
[HarmonyPatch("BreadMoProjOffset.Building_TurretGunNonSnap", "TryFindNewTarget")]
[PatchLevel(Level.Safe)]
public static class Patch_Building_TurretGunNonSnap_TryFindNewTarget2
{
  public static void Postfix(Building_TurretGun __instance, ref float ___curAngle, LocalTargetInfo ___currentTargetInt, LocalTargetInfo __result)
  {
    if (!___currentTargetInt.IsValid && __result.IsValid && __instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      ___curAngle = Ext_Math.RotateAngle(___curAngle, vehicle.FullAngle);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.GiantImperialTurret)]
[HarmonyPatch("BreadMoProjOffset.Building_TurretGunNonSnap", "Tick")]
[PatchLevel(Level.Safe)]
public static class Patch_Building_TurretGunNonSnap_Tick
{
  public static void Prefix(ref bool __state, LocalTargetInfo ___currentTargetInt)
  {
    __state = ___currentTargetInt.IsValid;
  }

  public static void Postfix(Building_TurretGun __instance, ref float ___curAngle, bool __state, LocalTargetInfo ___currentTargetInt)
  {
    if (!___currentTargetInt.IsValid && __state && __instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      ___curAngle = Ext_Math.RotateAngle(___curAngle, -vehicle.FullAngle);
    }
  }
}
