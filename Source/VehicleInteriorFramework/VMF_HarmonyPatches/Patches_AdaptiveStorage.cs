using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_AdaptiveStorage
    {
        static Patches_AdaptiveStorage()
        {
            if (ModCompat.AdaptiveStorage)
            {
                //VMF_Harmony.Instance.PatchCategory("VMF_Patches_AdaptiveStorage");

                VMF_Harmony.Instance.Patch(AccessTools.Method("AdaptiveStorage.PrintUtility:PrintAt", new Type[] { typeof(Graphic), typeof(SectionLayer), typeof(Thing), typeof(Vector3).MakeByRefType(), typeof(Vector2).MakeByRefType(), typeof(float) }), prefix: AccessTools.Method(typeof(Patch_PrintUtility_PrintAt), nameof(Patch_PrintUtility_PrintAt.Prefix)), transpiler: AccessTools.Method(typeof(Patch_PrintUtility_PrintAt), nameof(Patch_PrintUtility_PrintAt.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("AdaptiveStorage.StorageRenderer:DrawOffsetForThing"), postfix: AccessTools.Method(typeof(Patch_StorageRenderer_DrawOffsetForThing), nameof(Patch_StorageRenderer_DrawOffsetForThing.Postfix)), transpiler: AccessTools.Method(typeof(Patch_StorageRenderer_DrawOffsetForThing), nameof(Patch_StorageRenderer_DrawOffsetForThing.Transpiler)));
                VMF_Harmony.Instance.Patch(AccessTools.Method("AdaptiveStorage.StorageRenderer:ItemOffsetAt"), postfix: AccessTools.Method(typeof(Patch_StorageRenderer_ItemOffsetAt), nameof(Patch_StorageRenderer_ItemOffsetAt.Postfix)));
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

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_RotationForPrint);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_AdaptiveStorage")]
    [HarmonyPatch("AdaptiveStorage.StorageRenderer", "DrawOffsetForThing")]
    public static class Patch_StorageRenderer_DrawOffsetForThing
    {
        public static void Postfix(Thing thing, Vector3 parentDrawLoc, object itemGraphic, ref Vector3 __result)
        {
            if (thing.IsOnVehicleMapOf(out _))
            {
                var parent = thing.StoringThing();
                //stackBehaviourがCircleの時
                if (parent == null || stackBehaviour(itemGraphic) == 1) return;

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
                if (!parent.RotationForPrint().IsHorizontal)
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

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_RotationForPrint);
        }

        private static AccessTools.FieldRef<object, int> stackBehaviour = AccessTools.FieldRefAccess<int>("AdaptiveStorage.ItemGraphic:stackBehaviour");
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
