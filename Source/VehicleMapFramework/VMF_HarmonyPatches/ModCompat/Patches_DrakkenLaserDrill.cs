using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_DrakkenLaserDrill
{
    public const string Category = "VMF_Patches_DrakkenLaserDrill";

    static Patches_DrakkenLaserDrill()
    {
        if (ModCompat.DrakkenLaserDrill)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_DrakkenLaserDrill.Category)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_MouseAttack", "DoSomething")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_MouseAttack_DoSomething
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_DrakkenLaserDrill.Category)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_MouseAttack", "DoSomething_Move")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_MouseAttack_DoSomething_Move
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_DrakkenLaserDrill.Category)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_AutoAttack", "DoSomething_AttackAllPawn")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_AutoAttack_DoSomething_AttackAllPawn
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_DrakkenLaserDrill.Category)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_AutoAttack", "PrepareToAttack")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_AutoAttack_PrepareToAttack
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_DrakkenLaserDrill.Category)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_Attack", "DoSomething_I")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_Attack_DoSomething_I
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_DrakkenLaserDrill.Category)]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_Attack", "DoSomething_II")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_DrakkenLaserDrill_Attack_DoSomething_II
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}
