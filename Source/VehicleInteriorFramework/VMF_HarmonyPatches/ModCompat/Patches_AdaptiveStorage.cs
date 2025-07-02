using HarmonyLib;
using RimWorld;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static VehicleInteriors.MethodInfoCache;
using static VehicleInteriors.ModCompat.AdaptiveStorage;

namespace VehicleInteriors.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_AdaptiveStorage
{
    static Patches_AdaptiveStorage()
    {
        if (Active)
        {
            VMF_Harmony.PatchCategory("VMF_Patches_AdaptiveStorage");
        }
    }
}

[HarmonyPatchCategory("VMF_Patches_AdaptiveStorage")]
[HarmonyPatch("AdaptiveStorage.StorageGraphicWorker", "UpdatePrintData")]
public static class Patch_StorageGraphicWorker_UpdatePrintData
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindLastIndex(c => c.opcode == OpCodes.Stloc_0) + 1;

        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(1),
            CodeInstruction.LoadLocal(0),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation),
            new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertySetter("AdaptiveStorage.PrintDatas.PrintData:ExtraRotation"))
        ]);
        return codes.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_RotationForPrint);
    }
}

[HarmonyPatchCategory("VMF_Patches_AdaptiveStorage")]
[HarmonyPatch("AdaptiveStorage.ItemGraphicWorker", "DrawOffsetForItem")]
public static class Patch_ItemGraphicWorker_DrawOffsetForItem
{
    public static void Postfix(object __instance, Building_Storage building, Thing item, ref Vector3 __result)
    {
        if (item.IsOnVehicleMapOf(out _))
        {
            //stackBehaviourがCircleの時
            if (building == null || stackBehaviour(graphic(__instance)) == 1) return;

            var angle = VehicleMapUtility.rotForPrint.AsAngle;
            Vector3 origin;
            var parentDrawLoc = building.DrawPos;
            if (VehicleMapUtility.rotForPrint == Rot4.East)
            {
                origin = (item.Position.ToVector3Shifted() - parentDrawLoc).RotatedBy(angle);
            }
            else if (VehicleMapUtility.rotForPrint == Rot4.West)
            {
                origin = item.Position.ToVector3Shifted() - parentDrawLoc;
            }
            else
            {
                origin = Vector3.zero;
            }
            if (!building.RotationForPrint().IsHorizontal)
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
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_RotationForPrint);
    }

    private static AccessTools.FieldRef<object, int> stackBehaviour = AccessTools.FieldRefAccess<int>("AdaptiveStorage.ItemGraphic:stackBehaviour");

    private static AccessTools.FieldRef<object, object> graphic = AccessTools.FieldRefAccess<object>("AdaptiveStorage.ItemGraphicWorker:<graphic>P");
}

[HarmonyPatchCategory("VMF_Patches_AdaptiveStorage")]
[HarmonyPatch("AdaptiveStorage.ItemGraphicWorker", "ItemOffsetAt")]
public static class Patch_ItemGraphicWorker_ItemOffsetAt
{
    public static void Postfix(ref float stackRotation)
    {
        stackRotation -= VehicleMapUtility.rotForPrint.AsAngle;
    }
}
