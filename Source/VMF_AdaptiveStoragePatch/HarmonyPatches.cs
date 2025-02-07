using AdaptiveStorage;
using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using UnityEngine;
using VehicleInteriors;
using VehicleInteriors.VMF_HarmonyPatches;
using Vehicles;
using Verse;

namespace VMF_AdaptiveStoragePatch
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_AdaptiveStorage
    {
        static Patches_AdaptiveStorage()
        {
            VMF_Harmony.Instance.PatchCategory("VMF_Patches_AdaptiveStorage");
        }
    }

    [HarmonyPatchCategory("VMF_Patches_AdaptiveStorage")]
    [HarmonyPatch(typeof(PrintUtility), nameof(PrintUtility.PrintAt))]
    [HarmonyPatch(new Type[] { typeof(Graphic), typeof(SectionLayer), typeof(Thing), typeof(Vector3), typeof(Vector2), typeof(float) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal })]
    public static class Patch_PrintUtility_PrintAt
    {
        public static void Prefix(Thing thing, ref float extraRotation)
        {
            extraRotation += VehicleMapUtility.PrintExtraRotation(thing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_AdaptiveStorage")]
    [HarmonyPatch(typeof(StorageRenderer), nameof(StorageRenderer.PrintAt), new Type[] { typeof(SectionLayer), typeof(Vector3) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Ref })]
    public static class Patch_StorageRenderer_PrintAt
    {
        public static void Prefix(StorageRenderer __instance, SectionLayer layer)
        {
            if (__instance.Parent.IsOnVehicleMapOf(out _))
            {
                __instance.InitializeStoredThingGraphics(layer);
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_AdaptiveStorage")]
    [HarmonyPatch("AdaptiveStorage.StorageRenderer", "GetItemGraphicFor")]
    public static class Patch_StorageRenderer_GetItemGraphicFor
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_Thing_RotationOrig);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_AdaptiveStorage")]
    [HarmonyPatch("AdaptiveStorage.StorageRenderer", "DrawOffsetForThing")]
    public static class Patch_StorageRenderer_DrawOffsetForThing
    {
        public static void Postfix(Thing thing, Vector3 parentDrawLoc, ref Vector3 __result)
        {
            if (thing.IsOnVehicleMapOf(out _))
            {
                var parent = thing.StoringThing();
                if (parent == null || parent.def.size.x == parent.def.size.z) return;

                var angle = VehicleMapUtility.rotForPrint.AsAngle;
                Vector3 origin;
                if (VehicleMapUtility.rotForPrint == Rot4.East)
                {
                    origin = (thing.Position.ToVector3Shifted() - parentDrawLoc).RotatedBy(angle);
                }
                else if (VehicleMapUtility.rotForPrint == Rot4.West)
                {
                    origin = thing.Position.ToVector3Shifted() - parentDrawLoc;
                }
                else
                {
                    origin = Vector3.zero;
                }
                if (!parent.Rotation.IsHorizontal)
                {
                    if (VehicleMapUtility.rotForPrint == Rot4.East)
                    {
                        origin = origin.RotatedBy(-angle);
                    }
                    if (VehicleMapUtility.rotForPrint == Rot4.West)
                    {
                        origin = origin.RotatedBy(angle);
                    }
                }

                __result = Ext_Math.RotatePoint(__result, origin, angle);
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_AdaptiveStorage")]
    [HarmonyPatch("AdaptiveStorage.StorageRenderer", "ItemOffsetAt")]
    public static class Patch_StorageRenderer_ItemOffsetAt
    {
        public static void Postfix(ref float stackRotation)
        {
            stackRotation -= VehicleMapUtility.rotForPrint.AsAngle;
        }
    }
}