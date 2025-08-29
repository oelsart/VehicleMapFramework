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

[HarmonyPatchCategory(Patches_Royalty.Category)]
[HarmonyPatch(typeof(MeditationUtility), nameof(MeditationUtility.DrawMeditationSpotOverlay))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MeditationUtility_DrawMeditationSpotOverlay
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_GenThing_TrueCenter1)) - 1;
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FocusedOrSelectedDrawPosOffset)
        ]);
        return codes;
    }
}