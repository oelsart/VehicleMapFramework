using HarmonyLib;
using PipeSystem;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_VEF
    {
        static Patches_VEF()
        {
            VMF_Harmony.Instance.PatchCategory("VMF_Patches_VEF");
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VEF")]
    [HarmonyPatch(typeof(CompResource), nameof(CompResource.Props), MethodType.Getter)]
    public static class Patch_CompResource_Props
    {
        public static void Postfix(CompResource __instance, ref CompProperties_Resource __result)
        {
            if (__instance is CompPipeConnector connector)
            {
                dummy.pipeNet = connector.pipeNet;
                dummy.soundAmbient = __result.soundAmbient;
                __result = dummy;
            }
        }

        private static readonly CompProperties_Resource dummy = new CompProperties_Resource();
    }

    [HarmonyPatchCategory("VMF_Patches_VEF")]
    [HarmonyPatch(typeof(Graphic_LinkedPipe), nameof(Graphic_LinkedPipe.ShouldLinkWith))]
    public static class Patch_Graphic_LinkedPipe_ShouldLinkWith
    {
        public static void Prefix(ref IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
    }


    [HarmonyPatchCategory("VMF_Patches_VEF")]
    [HarmonyPatch(typeof(CompResourceStorage), nameof(CompResourceStorage.PostDraw))]
    public static class Patch_CompResourceStorage_PostDraw
    {
        public static void Prefix(CompResourceStorage __instance, ref GenDraw.FillableBarRequest ___request)
        {
            var fullRot = __instance.parent.BaseFullRotation();
            var offset = (__instance.Props.centerOffset + Vector3.up * 0.1f).RotatedBy(fullRot.AsAngle);
            if (__instance.parent.Graphic.WestFlipped && __instance.parent.BaseRotationVehicleDraw() == Rot4.West)
            {
                offset = offset.RotatedBy(180f);
            }
            ___request.center = __instance.parent.DrawPos + offset;
            Rot8Utility.Rotate(ref fullRot, RotationDirection.Clockwise);
            rotInt(ref ___request.rotation) = fullRot.AsByte;
        }

        private static readonly AccessTools.StructFieldRef<Rot4, byte> rotInt = AccessTools.StructFieldRefAccess<Rot4, byte>("rotInt");
    }
}