using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_AvoidFriendlyFire
{
  static Patches_AvoidFriendlyFire()
  {
    if (AvoidFriendlyFire.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.AvoidFriendlyFire);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AvoidFriendlyFire)]
[HarmonyPatch("AvoidFriendlyFire.FireProperties", "CasterMap", MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_FireProperties_CasterMap
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.AvoidFriendlyFire)]
[HarmonyPatch("AvoidFriendlyFire.FireProperties", ".ctor", MethodType.Constructor)]
[HarmonyPatch([typeof(Thing), typeof(Verb), typeof(IntVec3)])]
[PatchLevel(Level.Cautious)]
public static class Patch_FireProperties_Constructor
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.AvoidFriendlyFire)]
[HarmonyPatch("AvoidFriendlyFire.FireProperties", "GetAimOnTargetChance")]
[PatchLevel(Level.Cautious)]
public static class Patch_FireProperties_GetAimOnTargetChance
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap),
      (((Delegate)GridsUtility.Roofed).Method, ((Func<IntVec3, Map, bool>)VehicleMapUtility.RoofedAcrossMaps).Method),
      (((Func<IntVec3, Map, bool>)GenGrid.CanBeSeenOver).Method, ((Func<IntVec3, Map, bool>)GenSightOnVehicle.CanBeSeenOverOnVehicle).Method));
  }
}

[HarmonyPatchCategory(PatchCategories.AvoidFriendlyFire)]
[HarmonyPatch("AvoidFriendlyFire.FireProperties", "AdjustForLeaning")]
[PatchLevel(Level.Sensitive)]
public static class Patch_FireProperties_AdjustForLeaning
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var f_Origin = AccessTools.Field("AvoidFriendlyFire.FireProperties:Origin");
    var g_Caster = AccessTools.PropertyGetter("AvoidFriendlyFire.FireProperties:Caster");
    var g_CasterMap = AccessTools.PropertyGetter("AvoidFriendlyFire.FireProperties:CasterMap");
    var match_LeanShootingSourcesFromTo = CodeMatch.Calls(((Delegate)ShootLeanUtility.LeanShootingSourcesFromTo).Method);
    return new CodeMatcher(instructions)
      .MatchStartForward(match_LeanShootingSourcesFromTo)
      .MatchStartBackwards(CodeMatch.LoadsField(f_Origin))
      .SetAndAdvance(OpCodes.Callvirt, g_Caster)
      .Insert(CachedMethodInfo.g_Thing_Position.CallvirtInstruction)
      .MatchStartForward(CodeMatch.Calls(g_CasterMap))
      .SetAndAdvance(OpCodes.Callvirt, g_Caster)
      .Insert(CachedMethodInfo.g_Thing_Map.CallvirtInstruction)
      .MatchStartForward(match_LeanShootingSourcesFromTo)
      .Set(OpCodes.Call, ((Action<IntVec3, IntVec3, Map, List<IntVec3>>)ShootLeanUtilityOnVehicle.LeanShootingSourcesFromTo).Method)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.AvoidFriendlyFire)]
[HarmonyPatch("AvoidFriendlyFire.AttackTargetFinder_BestAttackTarget_Patch", "Prefix")]
[PatchLevel(Level.Cautious)]
public static class Patch_AttackTargetFinder_BestAttackTarget_Patch_Prefix
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return GenTypes.GetTypeInAnyAssembly(
        "AvoidFriendlyFire.AttackTargetFinder_BestAttackTarget_Patch",
        "AvoidFriendlyFire")
      .FindIncludingInnerTypes(t =>
        t.GetDeclaredMethods().Where(m => m.CallsMethod(CachedMethodInfo.g_Thing_Position)));
  }
  
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}


[HarmonyPatchCategory(PatchCategories.AvoidFriendlyFire)]
[HarmonyPatch("AvoidFriendlyFire.Building_TurretGun_OrderAttack_Patch", "Prefix")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGun_OrderAttack_Patch_Prefix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.AvoidFriendlyFire)]
[HarmonyPatch("AvoidFriendlyFire.Building_TurretGun_TryFindNewTarget_Patch", "Postfix")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_TurretGun_TryFindNewTarget_Patch_Postfix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap));
  }
}

[HarmonyPatchCategory(PatchCategories.AvoidFriendlyFire)]
[HarmonyPatch("AvoidFriendlyFire.Verb_CanHitTargetFrom_Patch", "Postfix")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CanHitTargetFrom_Patch_Postfix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
  }
}

[HarmonyPatchCategory(PatchCategories.AvoidFriendlyFire)]
[HarmonyPatch("AvoidFriendlyFire.FireConeOverlay", "BuildFireCone")]
[PatchLevel(Level.Cautious)]
public static class Patch_FireConeOverlay_BuildFireCone
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}