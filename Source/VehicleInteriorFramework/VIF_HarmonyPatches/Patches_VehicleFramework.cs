using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
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
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_IsOnNonFocusedVehicleMapOf),
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

    [HarmonyPatch(typeof(VehicleGraphics), nameof(VehicleGraphics.RetrieveAllOverlaySettingsGraphicsProperties), typeof(Rect), typeof(VehicleDef), typeof(Rot8), typeof(PatternData), typeof(List<GraphicOverlay>))]
    public static class Patch_VehicleGraphics_RetrieveAllOverlaySettingsGraphicsProperties
    {
        public static IEnumerable<VehicleGraphics.RenderData> Postfix(IEnumerable<VehicleGraphics.RenderData> values, Rect rect, VehicleDef vehicleDef, Rot8 rot, PatternData pattern)
        {
            foreach (var value in values)
            {
                yield return value;
            }
            foreach (CompProperties_TogglableOverlays compProperties in vehicleDef.comps.OfType<CompProperties_TogglableOverlays>())
            {
                foreach (var graphicOverlay in compProperties.overlays)
                {
                    if (graphicOverlay.data.renderUI)
                    {
                        yield return VehicleGraphics.RetrieveOverlaySettingsGraphicsProperties(rect, vehicleDef, rot, graphicOverlay, pattern);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(VehicleGUI), nameof(VehicleGUI.RetrieveAllOverlaySettingsGUIProperties), typeof(Rect), typeof(VehicleDef), typeof(Rot8), typeof(List<GraphicOverlay>))]
    public static class Patch_VehicleGUI_RetrieveAllOverlaySettingsGUIProperties
    {
        public static IEnumerable<VehicleGUI.RenderData> Postfix(IEnumerable<VehicleGUI.RenderData> values, Rect rect, VehicleDef vehicleDef, Rot8 rot)
        {
            foreach (var value in values)
            {
                yield return value;
            }
            foreach (CompProperties_TogglableOverlays compProperties in vehicleDef.comps.OfType<CompProperties_TogglableOverlays>())
            {
                foreach (var graphicOverlay in compProperties.overlays)
                {
                    if (graphicOverlay.data.renderUI)
                    {
                        yield return VehicleGUI.RetrieveOverlaySettingsGUIProperties(rect, vehicleDef, rot, graphicOverlay);
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(VehicleGhostUtility), nameof(VehicleGhostUtility.GhostGraphicOverlaysFor))]
    public static class Patch_VehicleGhostUtility_GhostGraphicOverlaysFor
    {
        public static IEnumerable<ValueTuple<Graphic, float>> Postfix(IEnumerable<ValueTuple<Graphic, float>> values, VehicleDef vehicleDef, Color ghostColor)
        {
            foreach (var value in values)
            {
                yield return value;
            }
            int num = 0;
            num = Gen.HashCombine<VehicleDef>(num, vehicleDef);
            num = Gen.HashCombineStruct<Color>(num, ghostColor);
            foreach (CompProperties_TogglableOverlays compProperties in vehicleDef.comps.OfType<CompProperties_TogglableOverlays>())
            {
                foreach (var graphicOverlay in compProperties.overlays)
                {
                    int key = Gen.HashCombine<GraphicDataRGB>(num, graphicOverlay.data.graphicData);
                    if (!VehicleGhostUtility.cachedGhostGraphics.TryGetValue(key, out Graphic graphic))
                    {
                        graphic = graphicOverlay.Graphic;
                        GraphicData graphicData = new GraphicData();
                        graphicData.CopyFrom(graphic.data);
                        graphicData.drawOffsetWest = graphic.data.drawOffsetWest;
                        graphicData.shadowData = null;
                        //Graphic graphic2 = graphicData.Graphic;
                        graphic = GraphicDatabase.Get(typeof(Graphic_Multi), graphic.path, ShaderTypeDefOf.EdgeDetect.Shader, graphic.drawSize, ghostColor, Color.white, graphicData, null, null);
                        VehicleGhostUtility.cachedGhostGraphics.Add(key, graphic);
                    }
                    yield return new ValueTuple<Graphic, float>(graphic, graphicOverlay.data.rotation);
                }
            }
        }
    }

    [HarmonyPatch(typeof(GenGridVehicles), nameof(GenGridVehicles.ImpassableForVehicles))]
    public static class Patch_GenGridVehicles_ImpassableForVehicles
    {
        public static void Postfix(Thing thing, ref bool __result)
        {
            __result = __result && !(thing is Building_VehicleSlope && thing.def.passability != Traversability.Impassable && !thing.def.IsFence);
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), "TurretAutoTick")]
    public static class Patch_VehicleTurret_TurretAutoTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_TargetingHelper_TryGetTarget = AccessTools.Method(typeof(TargetingHelper), nameof(TargetingHelper.TryGetTarget));
            var m_TargetingHelperOnVehicle_TryGetTarget = AccessTools.Method(typeof(TargetingHelperOnVehicle), nameof(TargetingHelperOnVehicle.TryGetTarget));
            return instructions.MethodReplacer(m_TargetingHelper_TryGetTarget, m_TargetingHelperOnVehicle_TryGetTarget);
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), "TurretTargeterTick")]
    public static class Patch_VehicleTurret_TurretTargeterTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_TurretTargeter_TargetMeetsRequirements = AccessTools.Method(typeof(TurretTargeter), nameof(TurretTargeter.TargetMeetsRequirements));
            var m_TargetingHelperOnVehicle_TargetMeetsRequirements = AccessTools.Method(typeof(TargetingHelperOnVehicle), nameof(TargetingHelperOnVehicle.TargetMeetsRequirements));
            return instructions.MethodReplacer(m_TurretTargeter_TargetMeetsRequirements, m_TargetingHelperOnVehicle_TargetMeetsRequirements);
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.TryFindShootLineFromTo))]
    public static class Patch_VehicleTurret_TryFindShootLineFromTo
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.FireTurret))]
    public static class Patch_VehicleTurret_FireTurret
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }
}