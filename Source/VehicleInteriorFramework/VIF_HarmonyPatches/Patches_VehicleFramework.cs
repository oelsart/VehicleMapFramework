using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI.Group;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.DisembarkPawn))]
    public static class Patch_VehiclePawn_DisembarkPawn
    {
        public static bool Prefix(Pawn pawn, VehiclePawn __instance)
        {
            var handler = __instance.handlers.First(h => h.handlers.Contains(pawn));
            if (handler.role is VehicleRoleBuildable buildable && __instance is VehiclePawnWithMap vehicle)
            {
                var parent = buildable.upgradeSingle.parent.parent;
                if (!pawn.Spawned)
                {
                    CellRect cellRect = parent.OccupiedRect().ExpandedBy(1);
                    IntVec3 intVec = parent.Position;
                    if (cellRect.EdgeCells.Where(delegate (IntVec3 c)
                    {
                        if (c.InBounds(vehicle.interiorMap) && c.Standable(vehicle.interiorMap))
                        {
                            return !c.GetThingList(vehicle.interiorMap).NotNullAndAny((Thing t) => t is Pawn);
                        }
                        return false;
                    }).TryRandomElement(out IntVec3 intVec2))
                    {
                        intVec = intVec2;
                    }
                    GenSpawn.Spawn(pawn, intVec, vehicle.interiorMap, WipeMode.Vanish);
                    if (!intVec.Standable(vehicle.interiorMap))
                    {
                        pawn.pather.TryRecoverFromUnwalkablePosition(false);
                    }
                    Lord lord = __instance.GetLord();
                    if (lord != null)
                    {
                        Lord lord2 = pawn.GetLord();
                        lord2?.Notify_PawnLost(pawn, PawnLostCondition.ForcedToJoinOtherLord, null);
                        lord.AddPawn(pawn);
                    }
                }
                __instance.RemovePawn(pawn);
                __instance.EventRegistry[VehicleEventDefOf.PawnExited].ExecuteEvents();
                if (!__instance.AllPawnsAboard.NotNullAndAny(null) && outOfFoodNotified(__instance))
                {
                    outOfFoodNotified(__instance) = false;
                }
                return false;
            }
            return true;
        }

        private static readonly AccessTools.FieldRef<VehiclePawn, bool> outOfFoodNotified = AccessTools.FieldRefAccess<VehiclePawn, bool>("outOfFoodNotified");
    }

    [HarmonyPatch("Vehicles.Rendering", "DrawSelectionBracketsVehicles")]
    public static class Patch_Rendering_DrawSelectionBracketsVehicles
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindLastIndex(c => c.opcode == OpCodes.Stloc_3);
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
            var rot = generator.DeclareLocal(typeof(Rot8));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(0),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Thing_Spawned),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_FullRotation),
                new CodeInstruction(OpCodes.Stloc_S, rot),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_AsAngleRot8),
                new CodeInstruction(OpCodes.Add)
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(ShaderTypeDef), nameof(ShaderTypeDef.Shader), MethodType.Getter)]
    [HarmonyPatchCategory("VehicleInteriors.EarlyPatches")]
    public static class Patch_ShaderTypeDef_Shader
    {
        public static void Prefix(ShaderTypeDef __instance, ref Shader ___shaderInt)
        {

            if (___shaderInt == null && __instance is RGBOpacityShaderTypeDef && VehicleMod.settings.debug.debugLoadAssetBundles)
            {
                ___shaderInt = VIF_Shaders.LoadShader(__instance.shaderPath);
                if (___shaderInt == null)
                {
                    SmashLog.Error("Failed to load Shader from path <text>\"" + __instance.shaderPath + "\"</text>", "");
                }
            }
        }
    }

    [HarmonyPatch(typeof(VehicleHarmonyOnMod), nameof(VehicleHarmonyOnMod.ShaderFromAssetBundle))]
    [HarmonyPatchCategory("VehicleInteriors.EarlyPatches")]
    public static class Patch_VehicleHarmonyOnMod_ShaderFromAssetBundle
    {
        //元メソッドが__instanceを引数として取っているのでこれを取得しようとすると元メソッドのインスタンス（存在しない）と混同してしまう
        public static bool Prefix(object[] __args)
        {
            return !(__args[0] is RGBOpacityShaderTypeDef);
        }
    }

    [HarmonyPatch(typeof(AssetBundleDatabase), nameof(AssetBundleDatabase.SupportsRGBMaskTex))]
    public static class Patch_AssetBundleDatabase_SupportsRGBMaskTex
    {
        public static void Postfix(ref bool __result, Shader shader, bool ignoreSettings)
        {
            __result = __result || ((VehicleMod.settings.main.useCustomShaders || ignoreSettings) &&
                (shader == VIF_DefOf.VIF_CutoutComplexRGBOpacity.Shader ||
                shader == VIF_DefOf.VIF_CutoutComplexPatternOpacity.Shader ||
                shader == VIF_DefOf.VIF_CutoutComplexSkinOpacity.Shader));
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.DrawAt))]
    public static class Patch_VehicleTurret_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var g_Rot8_North = AccessTools.PropertyGetter(typeof(Rot8), nameof(Rot8.North));
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Call && instruction.OperandIs(g_Rot8_North))
                {
                    yield return CodeInstruction.LoadArgument(2);
                }
                else yield return instruction;
            }
        }
    }

    //Graphic_RGBがEdgeDetectシェーダーに対応してないみたいなのでGraphic_Multiで代替。単にGetTypeの場所へのパッチだとgraphic2へ入れてるGraphicのゲッターでエラーを吐くっぽい？
    //graphicOverlayをextraの方に移したので不要になった
    //[HarmonyPatch]
    //public static class Patch_VehicleGhostUtility_GhostGraphicOverlaysFor
    //{
    //    private static MethodInfo TargetMethod()
    //    {
    //        return AccessTools.Method(targetType, "MoveNext");
    //    }

    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    //    {
    //        var codes = instructions.ToList();
    //        var c_GraphicData = AccessTools.Constructor(typeof(GraphicData));
    //        var pos = codes.FindIndex(c => c.opcode == OpCodes.Newobj && c.OperandIs(c_GraphicData));
    //        var f_cachedGhostGraphics = AccessTools.Field(typeof(VehicleGhostUtility), nameof(VehicleGhostUtility.cachedGhostGraphics));
    //        var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Ldsfld && c.OperandIs(f_cachedGhostGraphics));
    //        var label = generator.DefineLabel();
    //        var label2 = generator.DefineLabel();

    //        codes[pos].labels.Add(label);
    //        codes[pos2].labels.Add(label2);
    //        codes.InsertRange(pos, new[]
    //        {
    //            CodeInstruction.LoadLocal(4),
    //            new CodeInstruction(OpCodes.Isinst, typeof(Graphic_RGB)),
    //            new CodeInstruction(OpCodes.Brfalse_S, label),
    //            CodeInstruction.LoadLocal(4, true),
    //            CodeInstruction.LoadLocal(2),
    //            CodeInstruction.LoadArgument(0),
    //            CodeInstruction.LoadField(targetType, "ghostColor"),
    //            CodeInstruction.Call(typeof(Patch_VehicleGhostUtility_GhostGraphicOverlaysFor), nameof(Patch_VehicleGhostUtility_GhostGraphicOverlaysFor.GetGraphicRGB)),
    //            new CodeInstruction(OpCodes.Br_S, label2)
    //        });
    //        return codes;
    //    }

    //    private static void GetGraphicRGB(ref Graphic graphic, GraphicOverlay overlay, Color ghostColor)
    //    {
    //        var graphicData = new GraphicData();
    //        graphicData.CopyFrom(graphic.data);
    //        graphicData.drawOffsetWest = graphic.data.drawOffsetWest;
    //        graphicData.shadowData = null;
    //        graphic = GraphicDatabase.Get(typeof(Graphic_Multi), graphic.path, ShaderTypeDefOf.EdgeDetect.Shader, graphic.drawSize, ghostColor, Color.white, graphicData, null, null);
    //    }

    //    private static readonly Type targetType = AccessTools.Inner(typeof(VehicleGhostUtility), "<GhostGraphicOverlaysFor>d__5");
    //}
}