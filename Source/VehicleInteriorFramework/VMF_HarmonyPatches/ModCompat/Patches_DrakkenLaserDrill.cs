using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_DrakkenLaserDrill
{
    static Patches_DrakkenLaserDrill()
    {
        if (ModCompat.DrakkenLaserDrill)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_DrakkenLaserDrill");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_DrakkenLaserDrill")]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Building_DrakkenLaserDrill", "DrawAt")]
public static class Patch_Building_DrakkenLaserDrill_DrawAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
        var isOnVehicle = generator.DeclareLocal(typeof(bool));

        codes.InsertRange(0,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnVehicleMapOf),
            new CodeInstruction(OpCodes.Stloc_S, isOnVehicle)
        ]);

        var m_AltitudeFor = AccessTools.Method(typeof(Altitudes), nameof(Altitudes.AltitudeFor), [typeof(AltitudeLayer), typeof(float)]);
        foreach (var code in codes)
        {
            if (code.Calls(m_AltitudeFor))
            {
                var label = generator.DefineLabel();
                yield return new CodeInstruction(OpCodes.Ldloc_S, isOnVehicle);
                yield return new CodeInstruction(OpCodes.Brfalse_S, label);
                yield return new CodeInstruction(OpCodes.Ldc_R4, 200f);
                yield return new CodeInstruction(OpCodes.Add);
                yield return code.WithLabels(label);
            }
            else
            {
                yield return code;
            }
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_DrakkenLaserDrill")]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_MouseAttack", "DoSomething")]
public static class Patch_Comp_DrakkenLaserDrill_MouseAttack_DoSomething
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory("VMF_Patches_DrakkenLaserDrill")]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_MouseAttack", "DoSomething_Move")]
public static class Patch_Comp_DrakkenLaserDrill_MouseAttack_DoSomething_Move
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory("VMF_Patches_DrakkenLaserDrill")]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_AutoAttack", "DoSomething_AttackAllPawn")]
public static class Patch_Comp_DrakkenLaserDrill_AutoAttack_DoSomething_AttackAllPawn
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_DrakkenLaserDrill")]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_AutoAttack", "PrepareToAttack")]
public static class Patch_Comp_DrakkenLaserDrill_AutoAttack_PrepareToAttack
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory("VMF_Patches_DrakkenLaserDrill")]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_Attack", "DoSomething_I")]
public static class Patch_Comp_DrakkenLaserDrill_Attack_DoSomething_I
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory("VMF_Patches_DrakkenLaserDrill")]
[HarmonyPatch("MYDE_DrakkenLaserDrill.Comp_DrakkenLaserDrill_Attack", "DoSomething_II")]
public static class Patch_Comp_DrakkenLaserDrill_Attack_DoSomething_II
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}
