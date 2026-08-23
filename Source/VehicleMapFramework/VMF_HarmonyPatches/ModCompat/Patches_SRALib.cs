using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_SRALib
{
  static Patches_SRALib()
  {
    if (SRALib.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.SRALib);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_TurretGunHasSpeed_OrderAttack
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return SRALib.Building_TurretGunHasSpeed.Select(t => AccessTools.DeclaredMethod(t, "OrderAttack")).NonNull;
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGunHasSpeed_IsValidTarget
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return SRALib.Building_TurretGunHasSpeed.Select(t => AccessTools.DeclaredMethod(t, "IsValidTarget")).NonNull;
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_Roofed, CachedMethodInfo.m_RoofedAcrossMaps);
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGunHasSpeed_TryFindNewTarget
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return SRALib.Building_TurretGunHasSpeed.Select(t => AccessTools.DeclaredMethod(t, "TryFindNewTarget")).NonNull;
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.AddAllBuildingsColonistForThingInstance();
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_TurretGunHasSpeed_TryFindNewTarget_Delegate
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return SRALib.Building_TurretGunHasSpeed.Select(t =>
    {
      return AccessTools.FindIncludingInnerTypes(t,
        t2 => { return t2.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryFindNewTarget>")); });
    }).NonNull;
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Projectile_BulletWithEffect_Impact
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    yield return AccessTools.Method("SRA.Projectile_BulletWithEffect:Impact");
    yield return AccessTools.Method("SRA.Projectile_BeamWithEffect:Impact");
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Verb_KT_Tachyon_Lances", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_KT_Tachyon_Lances_TryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Verb_KT_Tachyon_Lances", "AffectedCells")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_KT_Tachyon_Lances_AffectedCells
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Verb_KT_Tachyon_Lances", "TargetPosition")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_KT_Tachyon_Lances_TargetPosition
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Verb_KT_Tachyon_Lances", "CanUseCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_KT_Tachyon_Lances_CanUseCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Verb_ShootWithOffset", "BaseTryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootWithOffsetSRA_BaseTryCastShot
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.Gizmo_LaserController", "GizmoOnGUI")]
[PatchLevel(Level.Cautious)]
public static class Patch_Gizmo_LaserController_GizmoOnGUI
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.m_Roofed, CachedMethodInfo.m_RoofedAcrossMaps));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Gizmo_LaserController_GizmoOnGUI_Delegate
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes(GenTypes.GetTypeInAnyAssembly("SRA.Gizmo_LaserController", "SRA"), t =>
    {
      return t.GetDeclaredMethods().FirstOrDefault(m =>
        m.Name.Contains("<GizmoOnGUI>") && m.CallsMethod(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_Roofed));
    });
  }
  
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
      .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMapSpawned)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Map))
      .Set(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing)
      .MatchStartForward(CodeMatch.Calls(((Delegate)GridsUtility.Roofed).Method))
      .Set(OpCodes.Call, ((Func<IntVec3, Map, bool>)VehicleMapUtility.RoofedAcrossMaps).Method)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
      .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMap)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
      .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnBaseMap)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.CompLaserADS", "TryFindTarget_AntiAir")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompLaserADS_TryFindTarget_AntiAir
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.CompLaserADS", "TryFindTarget_AntiGround")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompLaserADS_TryFindTarget_AntiGround
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.m_Roofed, CachedMethodInfo.m_RoofedAcrossMaps));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.CompLaserADS", "ShootGroundTarget")]
[PatchLevel(Level.Safe)]
public static class Patch_CompLaserADS_ShootGroundTarget
{
  public static void Prefix(ThingWithComps ___parent, Thing target, ref VirtualTeleporter? __state)
  {
    var targetMap = target.Map;
    if (targetMap is not null && targetMap != ___parent.Map)
    {
      __state = new VirtualTeleporter(___parent, targetMap);
    }
  }
  
  public static void Finalizer(VirtualTeleporter? __state) => __state?.Dispose();
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.CompLaserADS", "CompTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompLaserADS_CompTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.m_Roofed, CachedMethodInfo.m_RoofedAcrossMaps));
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.CompLaserADS", "DrawLaserOffscreen")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompLaserADS_DrawLaserOffscreen
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
      .InsertAndAdvance(CachedMethodInfo.m_BaseMap_Map.CallInstruction)
      .InsertAfter(CachedMethodInfo.m_BaseMap_Map.CallInstruction)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.SRALib)]
[HarmonyPatch("SRA.MapComponent_LaserADSManager", "GetTargetingCount")]
[PatchLevel(Level.Safe)]
public static class Patch_MapComponent_LaserADSManager_GetTargetingCount
{
  public static void Postfix(MapComponent __instance, Thing proj, ref int __result)
  {
    var type = __instance.GetType();
    foreach (var vehicle in VehiclePawnWithMapCache.AllVehiclesOnAsReadOnlySpan(__instance.map))
    {
      var component = vehicle.VehicleMap.GetComponent(type);
      if (component is not null)
      {
        __result += (int)SRALib.GetTargetingCount(component, SingleParam.Get(proj));
      }
    }
  }
}