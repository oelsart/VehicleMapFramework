using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_RoM
{
    public const string Category = "VMF_Patches_RoM";

    static Patches_RoM()
    {
        if (ModCompat.RimWorldOfMagic)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_RoM.Category)]
[HarmonyPatch("TorannMagic.TorannMagicMod+TryFindShootLineFromTo_Base_Patch", "Prefix")]
[PatchLevel(Level.Sensitive)]
public static class Patch_TryFindShootLineFromTo_Base_Patch_Prefix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        var m_CanReachImmediate = AccessTools.Method(typeof(ReachabilityImmediate), nameof(ReachabilityImmediate.CanReachImmediate), [typeof(IntVec3), typeof(LocalTargetInfo), typeof(Map), typeof(PathEndMode), typeof(Pawn)]);
        codes.MatchStartForward(CodeMatch.Calls(m_CanReachImmediate));
        codes.MatchStartBackwards(CodeMatch.IsLdarg(1));
        codes.InsertAfter(
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(Verb), nameof(Verb.caster)),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToThingMapCoord));
        return codes.Instructions().MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_RoM.Category)]
[HarmonyPatch("TorannMagic.TorannMagicMod+TryFindCastPosition_Base_Patch", "Prefix")]
[PatchLevel(Level.Cautious)]
public static class Patch_TryFindCastPosition_Base_Patch_Prefix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}