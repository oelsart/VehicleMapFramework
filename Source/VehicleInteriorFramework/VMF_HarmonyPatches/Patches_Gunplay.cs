﻿using HarmonyLib;
using System.Collections.Generic;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_Gunplay
    {
        static Patches_Gunplay()
        {
            if (ModCompat.Gunplay)
            {
                VMF_Harmony.PatchCategory("VMF_Patches_Gunplay");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_Gunplay")]
    [HarmonyPatch("Gunplay.Patch.PatchProjectileLaunch", "Postfix")]
    public static class Patch_PatchProjectileLaunch_Postfix
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }
}
