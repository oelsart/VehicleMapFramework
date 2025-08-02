using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public class Patches_Royalty
    {
        public const string Category = "VMF_Patches_Royalty";

        static Patches_Royalty()
        {
            if (ModsConfig.RoyaltyActive)
            {
                VMF_Harmony.PatchCategory(Category);
            }
        }
    }
}

//GenDraw.DrawLineBetween(GenThing.TrueCenter(pos, Rot4.North, def.size, def.Altitude), t.TrueCenter(), SimpleColor.Red, 0.2f) ->
//GenDraw.DrawLineBetween(FocusedDrawPosOffset(GenThing.TrueCenter(pos, Rot4.North, def.size, def.Altitude), pos), t.TrueCenter(), SimpleColor.Red, 0.2f)
[HarmonyPatchCategory(Patches_Royalty.Category)]
[HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.DrawArtificialBuildingOverlay))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MeditationUtility_DrawArtificialBuildingOverlay
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => TranspilerCommon(instructions);

    public static IEnumerable<CodeInstruction> TranspilerCommon(IEnumerable<CodeInstruction> instructions, int ArgumentNum = 0)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_GenThing_TrueCenter1)) - 1;
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(ArgumentNum),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FocusedDrawPosOffset)
        ]);
        return codes;
    }
}

[HarmonyPatchCategory(Patches_Royalty.Category)]
[HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.DrawMeditationSpotOverlay))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MeditationUtility_DrawMeditationSpotOverlay
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_MeditationUtility_DrawArtificialBuildingOverlay.TranspilerCommon(instructions);
}

[HarmonyPatchCategory(Patches_Royalty.Category)]
[HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.DrawMeditationFociAffectedByBuildingOverlay))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MeditationUtility_DrawMeditationFociAffectedByBuildingOverlay
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_MeditationUtility_DrawArtificialBuildingOverlay.TranspilerCommon(instructions, 3);
}