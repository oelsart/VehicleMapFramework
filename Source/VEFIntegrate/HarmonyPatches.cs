using HarmonyLib;
using PipeSystem;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
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
            if (DefDatabase<PipeNetDef>.AllDefsListForReading.Count < 2)
            {
                DefDatabase<ThingDef>.GetNamed("VMF_PipeConnector").designationCategory = null;
                DefDatabase<DesignationCategoryDef>.GetNamed("VF_Vehicles").ResolveReferences();
            }
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
    [HarmonyPatch(typeof(PipeNetManager), nameof(PipeNetManager.UnregisterConnector))]
    public static class Patch_PipeNetManager_UnregisterConnector
    {
        public static void Prefix(PipeNetManager __instance, CompResource comp)
        {
            var pipeNetMap = comp.PipeNet.map;
            if (__instance.map != pipeNetMap)
            {
                var component = MapComponentCache<PipeNetManager>.GetComponent(pipeNetMap);
                var connectors = comp.PipeNet.connectors.Where(c => c.parent.Map == pipeNetMap);
                var newNet = PipeNetMaker.MakePipeNet(connectors, pipeNetMap, comp.PipeNet.def);
                component.pipeNets.Add(newNet);
                CompPipeConnector.pipeNetCount(MapComponentCache<PipeNetManager>.GetComponent(__instance.map))++;
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VEF")]
    [HarmonyPatch(typeof(PipeNet), nameof(PipeNet.Merge))]
    public static class Patch_PipeNet_Merge
    {
        public static bool Prefix(ref PipeNet __instance, ref PipeNet otherNet)
        {
            if (__instance.map.IsVehicleMapOf(out _) && otherNet.map != __instance.map)
            {
                otherNet.Merge(__instance);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VEF")]
    [HarmonyPatch(typeof(Graphic_LinkedPipe), nameof(Graphic_LinkedPipe.ShouldLinkWith))]
    public static class Patch_Graphic_LinkedPipeVEF_ShouldLinkWith
    {
        public static void Prefix(ref IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, AccessTools.Method(typeof(Patch_Graphic_LinkedPipeVEF_ShouldLinkWith), nameof(MapModified)));
        }

        private static Map MapModified(Thing thing)
        {
            if (thing.TryGetComp<CompResource>(out var comp) && thing.Map != comp.PipeNet.map)
            {
                return comp.PipeNet.map;
            }
            return thing.Map;
        }
    }


    [HarmonyPatchCategory("VMF_Patches_VEF")]
    [HarmonyPatch(typeof(CompResourceStorage), nameof(CompResourceStorage.PostDraw))]
    public static class Patch_CompResourceStorage_PostDraw
    {
        public static void Prefix(CompResourceStorage __instance, ref GenDraw.FillableBarRequest ___request)
        {
            var fullRot = __instance.parent.BaseFullRotationAsRot4();
            var offset = (__instance.Props.centerOffset + Vector3.up * 0.1f).RotatedBy(new Rot8(fullRot.AsInt).AsAngle);
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