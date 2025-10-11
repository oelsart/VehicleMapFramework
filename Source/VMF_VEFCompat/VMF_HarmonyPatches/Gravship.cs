using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatchCategory(Patches_VEF.CategoryVGE)]
[HarmonyPatch("VanillaGravshipExpanded.CompPointDefence", "FindTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPointDefence_FindTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryVGE)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_CompPointDefence_FindTarget_Delegate
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return GenTypes.GetTypeInAnyAssembly("VanillaGravshipExpanded.CompPointDefence", "VanillaGravshipExpanded")
            .GetDeclaredMethods()
            .Where(m =>
            {
                if (!m.Name.Contains("<FindTarget>")) return false;
                return VMF_Harmony.ReadMethodBodyWrapper(m).Any(i =>
                    CachedMethodInfo.g_Thing_Position.Equals(i.Value));
            });
    }
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryVGE)]
[HarmonyPatch("VanillaGravshipExpanded.CompPointDefence", "TryIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPointDefence_TryIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryVGE)]
[HarmonyPatch("VanillaGravshipExpanded.FloatMenuOptionProvider_ExtinguishAstrofires", "GetSingleOption")]
[PatchLevel(Level.Sensitive)]
public static class Patch_FloatMenuOptionProvider_ExtinguishAstrofires_GetSingleOption
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}

[HarmonyPatchCategory(Patches_VEF.CategoryVGE)]
[HarmonyPatch("VanillaGravshipExpanded.FloatMenuOptionProvider_ExtinguishAstrofires", "PawnCanExtinguish")]
[PatchLevel(Level.Sensitive)]
public static class Patch_FloatMenuOptionProvider_ExtinguishAstrofires_PawnCanExtinguish
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}