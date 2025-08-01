﻿using HarmonyLib;
using RimWorld;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using static VehicleMapFramework.MethodInfoCache;
using static VehicleMapFramework.ModCompat.AdaptiveStorage;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_AdaptiveStorage
{
    public const string Category = "VMF_Patches_AdaptiveStorage";

    static Patches_AdaptiveStorage()
    {
        if (Active)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_AdaptiveStorage.Category)]
[HarmonyPatch("AdaptiveStorage.StorageGraphicWorker", "UpdatePrintData")]
[PatchLevel(Level.Sensitive)]
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

[HarmonyPatchCategory(Patches_AdaptiveStorage.Category)]
[HarmonyPatch("AdaptiveStorage.ItemGraphicWorker", "DrawOffsetForItem")]
public static class Patch_ItemGraphicWorker_DrawOffsetForItem
{
    [PatchLevel(Level.Safe)]
    public static void Postfix(object __instance, Building_Storage building, Thing item, ref Vector3 __result)
    {
        if (item.IsOnVehicleMapOf(out _))
        {
            //stackBehaviourがCircleの時
            if (building == null || stackBehaviour(graphic(__instance)) == 1) return;

            var angle = VehicleMapUtility.RotForPrint.AsAngle;
            Vector3 origin;
            var parentDrawLoc = building.DrawPos;
            if (VehicleMapUtility.RotForPrint == Rot4.East)
            {
                origin = (item.Position.ToVector3Shifted() - parentDrawLoc).RotatedBy(angle);
            }
            else if (VehicleMapUtility.RotForPrint == Rot4.West)
            {
                origin = item.Position.ToVector3Shifted() - parentDrawLoc;
            }
            else
            {
                origin = Vector3.zero;
            }
            if (!building.RotationForPrint().IsHorizontal)
            {
                if (VehicleMapUtility.RotForPrint == Rot4.East)
                {
                    origin = origin.RotatedBy(-angle);
                }
                if (VehicleMapUtility.RotForPrint == Rot4.West)
                {
                    origin = origin.RotatedBy(angle);
                }
            }

            __result = Ext_Math.RotatePoint(__result, origin, angle);
        }
    }

    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_RotationForPrint);
    }

    private static AccessTools.FieldRef<object, int> stackBehaviour = AccessTools.FieldRefAccess<int>("AdaptiveStorage.ItemGraphic:stackBehaviour");

    private static AccessTools.FieldRef<object, object> graphic = AccessTools.FieldRefAccess<object>("AdaptiveStorage.ItemGraphicWorker:<graphic>P");
}

[HarmonyPatchCategory(Patches_AdaptiveStorage.Category)]
[HarmonyPatch("AdaptiveStorage.ItemGraphicWorker", "ItemOffsetAt")]
[PatchLevel(Level.Safe)]
public static class Patch_ItemGraphicWorker_ItemOffsetAt
{
    public static void Postfix(ref float stackRotation)
    {
        stackRotation -= VehicleMapUtility.RotForPrint.AsAngle;
    }
}
