using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_MultiFloors
{
    static Patches_MultiFloors()
    {
        if (MultiFloors.Active)
        {
            VMF_Harmony.PatchCategory(PatchCategories.MultiFloors);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.MultiFloors)]
[HarmonyPatch(typeof(LoadedObjectDirectory), nameof(LoadedObjectDirectory.RegisterLoaded))]
[PatchLevel(Level.Mandatory)]
public static class Patch_LoadedObjectDirectory_RegisterLoaded
{
    public static bool Prefix(ILoadReferenceable reffable, Dictionary<string, ILoadReferenceable> ___allObjectsByLoadID)
    {
        if (reffable is not Pawn pawn) return true;
        var text = "[excepted]";
        try
        {
            text = reffable.GetUniqueLoadID();
        }
        catch (Exception)
        {
            // ignored
        }

        if (___allObjectsByLoadID.TryGetValue(text, out _))
        {
            var text2 = "[excepted]";
            try
            {
                text2 = reffable.ToString();
            }
            catch (Exception)
            {
                // ignored
            }

            VMF_Log.Warning($"Pawn duplication detected. Destroying the duplicated pawn: {text2}.");
            pawn.Destroy();
            return false;
        }

        return true;
    }
}

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