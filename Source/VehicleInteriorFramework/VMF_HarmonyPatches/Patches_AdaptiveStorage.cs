using HarmonyLib;
using System;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    public static class Patches_AdaptiveStorage
    {
        static Patches_AdaptiveStorage()
        {
            if (ModsConfig.IsActive("adaptive.storage.framework"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_AdaptiveStorage");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_AdaptiveStorage")]
    [HarmonyPatch("AdaptiveStorage.PrintUtility", "PrintAt")]
    [HarmonyPatch(new Type[] { typeof(Graphic), typeof(SectionLayer), typeof(Thing), typeof(Vector3), typeof(Vector2), typeof(float) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal })]
    public static class Patch_PrintUtility_PrintAt
    {
        public static void Prefix(Thing thing, ref float extraRotation)
        {
            extraRotation += VehicleMapUtility.PrintExtraRotation(thing);
        }
    }
}