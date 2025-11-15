using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_Biotech
{
    static Patches_Biotech()
    {
        if (ModsConfig.BiotechActive)
        {
            VMF_Harmony.PatchCategory(PatchCategories.Biotech);
        }
    }
}

[HarmonyPatch(typeof(ThoughtWorker_PsychicBondProximity), nameof(ThoughtWorker_PsychicBondProximity.NearPsychicBondedPerson))]
[PatchLevel(Level.Cautious)]
public static class Patch_ThoughtWorker_PsychicBondProximity_NearPsychicBondedPerson
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
    }
}