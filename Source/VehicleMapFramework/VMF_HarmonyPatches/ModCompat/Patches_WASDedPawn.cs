using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_WASDedPawn
{
    static Patches_WASDedPawn()
    {
        if (ModCompat.WASDedPawn)
        {
            VMF_Harmony.PatchCategory(PatchCategories.WASDedPawn);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.WASDedPawn)]
[HarmonyPatch("wasdedPawn.WASDGameComponent", "TryMovePawn")]
[PatchLevel(Level.Sensitive)]
public static class Patch_WASDGameComponent_TryMovePawn
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var f_lasPos3 = AccessTools.Field("wasdedPawn.WASDGameComponent:lasPos3");
        var m_ToIntVec3 = AccessTools.Method(typeof(IntVec3Utility), nameof(IntVec3Utility.ToIntVec3));
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.Calls(m_ToIntVec3))
            .Repeat(c =>
                c.MatchStartBackwards(CodeMatch.LoadsField(f_lasPos3))
                    .InsertAfterAndAdvance(
                        CodeInstruction.LoadLocal(3),
                        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToNonFocusedThingMapCoord))
                    .MatchStartForward(CodeMatch.Calls(m_ToIntVec3)).Advance()
                )
            .InstructionEnumeration();
    }
}

[HarmonyPatchCategory(PatchCategories.WASDedPawn)]
[HarmonyPatch("wasdedPawn.WASDGameComponent", "RenderPawn")]
[PatchLevel(Level.Sensitive)]
public static class Patch_WASDGameComponent_RenderPawn
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var f_lasPos3 = AccessTools.Field("wasdedPawn.WASDGameComponent:lasPos3");
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.LoadsField(f_lasPos3))
            .Repeat(c => c.InsertAfterAndAdvance(
                CodeInstruction.LoadArgument(1),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToNonFocusedThingMapCoord)))
            .InstructionEnumeration();
    }
}

[HarmonyPatchCategory(PatchCategories.WASDedPawn)]
[HarmonyPatch("wasdedPawn.WASDGameComponent", "HandleAiming")]
[PatchLevel(Level.Cautious)]
public static class Patch_WASDGameComponent_HandleAiming
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps);
    }
}

[HarmonyPatchCategory(PatchCategories.WASDedPawn)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_WASDGameComponent_GetImportantThing_Delegate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(
            GenTypes.GetTypeInAnyAssembly("wasdedPawn.WASDGameComponent", "wasdedPawn"),
            t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<GetImportantThing>")));
    }

    public static void Postfix(Thing t, ref int __result)
    {
        if (t is VehiclePawnWithMap) __result = 2;
    }
}