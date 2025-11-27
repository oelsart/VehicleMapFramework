// using System.Collections.Generic;
// using HarmonyLib;
//
// namespace VehicleMapFramework.VMF_HarmonyPatches;
//
// [StaticConstructorOnStartupPriority(Priority.Low)]
// internal static class Patches_MultiFloors
// {
//     static Patches_MultiFloors()
//     {
//         if (ModCompat.MultiFloors.Active)
//         {
//             VMF_Harmony.PatchCategory(PatchCategories.MultiFloors);
//         }
//     }
// }

//[HarmonyPatchCategory(PatchCategories.MultiFloors)]
//[HarmonyPatch("MultiFloors.Maps.LevelMapGenerator", "SetupMapGenerator")]
//[PatchLevel(Level.Safe)]
//public static class Patch_LevelMapGenerator_SetupMapGenerator
//{
//    public static void Postfix(Thing entrance, ref MapGeneratorDef __result)
//    {
//        if (entrance.IsOnVehicleMapOf(out _) && __result?.defName == "MF_Basement")
//        {
//            var MF_BasementWithoutCaves = DefDatabase<MapGeneratorDef>.GetNamedSilentFail("MF_BasementWithoutCaves");
//            if (MF_BasementWithoutCaves != null)
//            {
//                __result = MF_BasementWithoutCaves;
//            }
//        }
//    }
//}