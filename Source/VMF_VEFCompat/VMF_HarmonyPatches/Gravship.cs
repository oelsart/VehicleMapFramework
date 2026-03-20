using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatchCategory(PatchCategories.VGE)]
[HarmonyPatch("VanillaGravshipExpanded.CompPointDefence", "FindTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPointDefence_FindTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.VGE)]
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
                return PatchHelper.ReadMethodBodyWrapper(m).Any(i =>
                    CachedMethodInfo.g_Thing_Position.Equals(i.Value));
            });
    }
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.VGE)]
[HarmonyPatch("VanillaGravshipExpanded.CompPointDefence", "TryIntercept")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompPointDefence_TryIntercept
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.VGE)]
[HarmonyPatch("VanillaGravshipExpanded.FloatMenuOptionProvider_ExtinguishAstrofires", "GetSingleOption")]
[PatchLevel(Level.Sensitive)]
public static class Patch_FloatMenuOptionProvider_ExtinguishAstrofires_GetSingleOption
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        => Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption.Transpiler(instructions, generator);
}

[HarmonyPatchCategory(PatchCategories.VGE)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_FloatMenuOptionProvider_ExtinguishAstrofires_GetSingleOption_Delegate
{
    private static MethodBase TargetMethod()
    {
        var type = GenTypes.GetTypeInAnyAssembly(
            "VanillaGravshipExpanded.FloatMenuOptionProvider_ExtinguishAstrofires", "VanillaGravshipExpanded");
        return AccessTools.FindIncludingInnerTypes(type, t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<GetSingleOption>"));
        });
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        => Patch_FloatMenuOptionProvider_ExtinguishFires_GetSingleOption_Delegate.Transpiler(instructions, generator);
}