using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_EnergyShield
{
  static Patches_EnergyShield()
  {
    if (EnergyShield.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.EnergyShield);

      if (EnergyShield.CECompat)
      {
        VMF_Harmony.PatchCategory(PatchCategories.EnergyShieldCECompat);
      }
    }
  }
}

[HarmonyPatchCategory(PatchCategories.EnergyShield)]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.ShieldManagerMapComp", "WillInterceptOrbitalStrike")]
[PatchLevel(Level.Sensitive)]
public static class Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var m_AllBuildingsColonistOfClass = AccessTools.Method(typeof(ListerBuildings), nameof(ListerBuildings.AllBuildingsColonistOfClass), generics: [EnergyShield.Building_Shield]);
    foreach (var instruction in instructions)
    {
      yield return instruction;

      if (instruction.Calls(m_AllBuildingsColonistOfClass))
      {
        yield return CodeInstruction.LoadArgument(0);
        yield return CodeInstruction.Call(typeof(Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike), nameof(AddBuildings));
      }
    }
  }

  private static IEnumerable<Building> AddBuildings(IEnumerable<Building> buildings, MapComponent component)
  {
    return buildings
      .Concat(VehiclePawnWithMapCache.AllVehiclesOn(component.map)
        .SelectMany(v => v.VehicleMap.listerBuildings.allBuildingsColonist
          .Where(b => b.def.thingClass.SameOrSubclassOf(EnergyShield.Building_Shield))));
  }
}

[HarmonyPatchCategory(PatchCategories.EnergyShield)]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.ShieldManagerMapComp", "WillInterceptExplosion")]
[PatchLevel(Level.Cautious)]
public static class Patch_ShieldManagerMapComp_WillInterceptExplosion
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.EnergyShield)]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.ShieldManagerMapComp", "WillInterceptExplosionAffectCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_ShieldManagerMapComp_WillInterceptExplosionAffectCell
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.EnergyShield)]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.ShieldManagerMapComp", "WillDropPodBeIntercepted")]
[PatchLevel(Level.Cautious)]
public static class Patch_ShieldManagerMapComp_WillDropPodBeIntercepted
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.EnergyShield)]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.ShieldManagerMapComp", "WillProjectileBeBlocked")]
[PatchLevel(Level.Cautious)]
public static class Patch_ShieldManagerMapComp_WillProjectileBeBlocked
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_ShieldManagerMapComp_WillInterceptOrbitalStrike.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.EnergyShield)]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.Comp_ShieldGenerator", "costShield")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_ShieldGenerator_costShield
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.EnergyShield)]
[HarmonyPatch("zhuzi.AdvancedEnergy.Shields.Shields.Comp_ShieldGenerator", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_ShieldGenerator_PostDraw
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    foreach (var instruction in instructions)
    {
      yield return instruction;

      if (instruction.Calls(CachedMethodInfo.m_IntVec3_ToVector3Shifted))
      {
        yield return CodeInstruction.LoadArgument(0);
        yield return CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent));
        yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToThingBaseMapCoord);
      }
    }
  }
}
