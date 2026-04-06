using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Targeter), "ConfirmStillValid")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Targeter_ConfirmStillValid
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMapOrCaravan_Map);
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
            .Repeat(c =>
            {
                c.InsertAndAdvance(code);
                c.InsertAfterAndAdvance(code).Advance();
            }).InstructionEnumeration();
    }
}

[HarmonyPatch(typeof(Targeter), "OrderVerbForceTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Targeter_OrderVerbForceTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Patch_Targeter_ConfirmStillValid.Transpiler(instructions);
    }
}

[HarmonyPatch(typeof(Targeter), "CurrentTargetUnderMouse")]
[PatchLevel(Level.Cautious)]
public static class Patch_Targeter_CurrentTargetUnderMouse
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_GenUI_TargetsAtMouse, CachedMethodInfo.m_GenUIOnVehicle_TargetsAtMouse);
    }
}