using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VehicleInteriors.VMF_HarmonyPatches;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    public static class Patches_VEF
    {
        static Patches_VEF()
        {
            if (ModsConfig.IsActive("adaptive.storage.framework"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_AdaptiveStorage");
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
}