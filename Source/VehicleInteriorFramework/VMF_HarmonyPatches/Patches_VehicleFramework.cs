﻿using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    //VehiclePawnWithMapの場合Movementフラグを持つハンドラーが存在しない場合コントロールできないようにする
    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.CanMoveWithOperators), MethodType.Getter)]
    public static class Patch_VehiclePawn_CanMoveWithOperators
    {
        public static bool Prefix(VehiclePawn __instance, ref bool __result)
        {
            if (__instance is VehiclePawnWithMap)
            {
                if (__instance.MovementPermissions == VehiclePermissions.NoDriverNeeded)
                {
                    __result = true;
                    return false;
                }
                var matchHandlers = __instance.handlers.Where(h => h.role.HandlingTypes.HasFlag(HandlingTypeFlags.Movement)).ToList();
                if (matchHandlers.Empty())
                {
                    __result = false;
                    return false;
                }
                __result = matchHandlers.All(h => h.RoleFulfilled);
                return false;
            }
            return true;
        }
    }

    //VehiclePawnWithMapの場合タレットに対応するハンドラーが存在しない場合コントロールできないようにする
    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.RecacheMannedStatus))]
    public static class Patch_VehicleTurret_RecacheMannedStatus
    {
        public static bool Prefix(VehicleTurret __instance)
        {
            if (__instance.vehicle is VehiclePawnWithMap)
            {
                if (VehicleMod.settings.debug.debugShootAnyTurret)
                {
                    IsManned(__instance, true);
                    return false;
                }
                var matchHandlers = __instance.vehicle.handlers.FindAll(h => h.role.HandlingTypes.HasFlag(HandlingTypeFlags.Turret) && (h.role.TurretIds.Contains(__instance.key) || h.role.TurretIds.Contains(__instance.groupKey)));
                if (matchHandlers.Empty())
                {
                    IsManned(__instance, false);
                    return false;
                }
                IsManned(__instance, matchHandlers.All(h => h.RoleFulfilled));
                return false;
            }
            return true;
        }

        private static readonly FastInvokeHandler IsManned = MethodInvoker.GetHandler(AccessTools.PropertySetter(typeof(VehicleTurret), nameof(IsManned)));
    }

    //VehiclePawnWithMapの場合タレットに対応するハンドラーが存在しない場合ギズモを操作不能にする
    [HarmonyPatch(typeof(CompVehicleTurrets), nameof(CompVehicleTurrets.CompGetGizmosExtra))]
    public static class Patch_CompVehicleTurrets_CompGetGizmosExtra
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, CompVehicleTurrets __instance)
        {
            foreach (var gizmo in gizmos)
            {
                if (gizmo is Command_CooldownAction command_CooldownAction && __instance.Vehicle is VehiclePawnWithMap)
                {
                    var turret = command_CooldownAction.turret;
                    if (!VehicleMod.settings.debug.debugShootAnyTurret && !command_CooldownAction.Disabled && __instance.Vehicle.GetAllHandlersMatch(HandlingTypeFlags.Turret, !turret.groupKey.NullOrEmpty() ? turret.groupKey : turret.key).Empty())
                    {
                        command_CooldownAction.Disable("VMF_NoRoles".Translate(__instance.Vehicle.LabelShort));
                    }
                }
                yield return gizmo;
            }
        }
    }

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.DisembarkPawn))]
    public static class Patch_VehiclePawn_DisembarkPawn
    {
        public static bool Prefix(Pawn pawn, VehiclePawn __instance)
        {
            var handler = __instance.handlers.First(h => h.handlers.Contains(pawn));
            if (handler.role is VehicleRoleBuildable buildable && __instance is VehiclePawnWithMap vehicle)
            {
                var parent = buildable.upgradeComp.parent;
                if (!pawn.Spawned)
                {
                    CellRect cellRect = parent.OccupiedRect().ExpandedBy(1);
                    IntVec3 intVec = parent.Position;
                    if (cellRect.EdgeCells.Where(delegate (IntVec3 c)
                    {
                        if (c.InBounds(vehicle.VehicleMap) && c.Standable(vehicle.VehicleMap))
                        {
                            return !c.GetThingList(vehicle.VehicleMap).NotNullAndAny((Thing t) => t is Pawn);
                        }
                        return false;
                    }).TryRandomElement(out IntVec3 intVec2))
                    {
                        intVec = intVec2;
                    }
                    GenSpawn.Spawn(pawn, intVec, vehicle.VehicleMap, WipeMode.Vanish);
                    if (!intVec.Standable(vehicle.VehicleMap))
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

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.FullRotation))]
    public static class Patch_VehiclePawn_FullRotation
    {
        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Getter)]
        public static void PostGetter(VehiclePawn __instance, ref Rot8 __result)
        {
            if (__instance.VehicleDef.graphicData.drawRotated && __instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                var angle = Ext_Math.RotateAngle(__result.AsAngle, vehicle.FullRotation.AsAngle);
                __result = Rot8.FromAngle(angle);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(MethodType.Setter)]
        public static void PostSetter(VehiclePawn __instance)
        {
            if (__instance is VehiclePawnWithMap vehicle)
            {
                MapComponentCache<VehiclePawnWithMapCache>.GetComponent(vehicle.VehicleMap).ForceResetCache();
            }
        }
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
                new CodeInstruction(OpCodes.Callvirt, MethodInfoCache.g_Rot8_AsAngle),
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

            if (___shaderInt == null && __instance is RGBMaskShaderTypeDef && VehicleMod.settings.debug.debugLoadAssetBundles)
            {
                ___shaderInt = VMF_Shaders.LoadShader(__instance.shaderPath);
                if (___shaderInt == null)
                {
                    Log.Error("[VehicleMapFramework] Failed to load Shader from path ${__instance.shaderPath}");
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
            return !(__args[0] is RGBMaskShaderTypeDef);
        }
    }

    [HarmonyPatch(typeof(AssetBundleDatabase), nameof(AssetBundleDatabase.SupportsRGBMaskTex))]
    public static class Patch_AssetBundleDatabase_SupportsRGBMaskTex
    {
        public static void Postfix(ref bool __result, Shader shader, bool ignoreSettings)
        {
            __result = __result || ((VehicleMod.settings.main.useCustomShaders || ignoreSettings) &&
                (shader == VMF_DefOf.VMF_CutoutComplexRGBOpacity.Shader ||
                shader == VMF_DefOf.VMF_CutoutComplexPatternOpacity.Shader ||
                shader == VMF_DefOf.VMF_CutoutComplexSkinOpacity.Shader));
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

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.AngleBetween))]
    public static class Patch_VehicleTurret_AngleBetween
    {
        public static void Prefix(VehicleTurret __instance, ref Vector3 mousePosition)
        {
            if (__instance.vehicle.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                mousePosition = Ext_Math.RotatePoint(mousePosition, __instance.TurretLocation, vehicle.FullRotation.AsAngle);
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
                        graphic = GraphicDatabase.Get(typeof(Graphic_Vehicle), graphic.path, ShaderTypeDefOf.EdgeDetect.Shader, graphic.drawSize, ghostColor, Color.white, graphicData, null, null);
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
            __result = __result && !(thing is Building_VehicleRamp && thing.def.passability != Traversability.Impassable && !thing.def.IsFence);
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

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.InRange))]
    public static class Patch_VehicleTurret_InRange
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
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

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.TurretRotation), MethodType.Getter)]
    public static class Patch_VehicleTurret_TurretRotation
    {
        public static void Postfix(ref float __result, VehiclePawn ___vehicle)
        {
            if (___vehicle.IsOnNonFocusedVehicleMapOf(out var vehicle2))
            {
                __result = Ext_Math.RotateAngle(__result, vehicle2.FullRotation.AsAngle);
            }
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.TurretRotationTargeted), MethodType.Setter)]
    public static class Patch_VehicleTurret_TurretRotationTargeted
    {
        public static void Prefix(ref float value, VehicleTurret __instance)
        {
            if (__instance.vehicle.IsOnNonFocusedVehicleMapOf(out var vehicle2) && (__instance.TargetLocked || TurretTargeter.Turret == __instance))
            {
                value = Ext_Math.RotateAngle(value, -vehicle2.FullRotation.AsAngle);
            }
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.RotationAligned), MethodType.Getter)]
    public static class Patch_VehicleTurret_RotationAligned
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var f_rotationTargeted = AccessTools.Field(typeof(VehicleTurret), "rotationTargeted");
            var g_RotationTargeted = AccessTools.PropertyGetter(typeof(VehicleTurret), nameof(VehicleTurret.TurretRotationTargeted));
            return instructions.Manipulator(c => c.OperandIs(f_rotationTargeted), c =>
            {
                c.opcode = OpCodes.Callvirt;
                c.operand = g_RotationTargeted;
            });
        }
    }

    [HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.BeginTargeting))]
    public static class Patch_TurretTargeter_BeginTargeting
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatch(typeof(TurretTargeter), "CurrentTargetUnderMouse")]
    public static class Patch_TurretTargeter_CurrentTargetUnderMouse
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_GenUI_TargetsAtMouse, MethodInfoCache.m_GenUIOnVehicle_TargetsAtMouse);
        }
    }

    [HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.TargeterUpdate))]
    public static class Patch_TurretTargeter_TargeterUpdate
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.ProcessInputEvents))]
    public static class Patch_TurretTargeter_ProcessInputEvents
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.TargetMeetsRequirements))]
    public static class Patch_TurretTargeter_TargetMeetsRequirements
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSightToThing, MethodInfoCache.m_GenSightOnVehicle_LineOfSightToThingVehicle)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSight1, MethodInfoCache.m_GenSightOnVehicle_LineOfSight1);
        }
    }

    [HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.TargeterValid), MethodType.Getter)]
    public static class Patch_TurretTargeter_TargeterValid
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    //タレットの自動ロードがVehicleDefのCargoCapacityを参照してたので、これをVehiclePawnのインスタンスからステータスを参照させる
    [HarmonyPatch(typeof(Command_CooldownAction), "DrawBottomBar")]
    public static class Patch_Command_CooldownAction_DrawBottomBar
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var m_GetStatValueAbstract = AccessTools.Method(typeof(Ext_Vehicles), nameof(Ext_Vehicles.GetStatValueAbstract));
            var m_VehiclePawn_GetStatValue = AccessTools.Method(typeof(VehiclePawn), nameof(VehiclePawn.GetStatValue));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_GetStatValueAbstract));

            if (pos != -1)
            {
                codes[pos].opcode = OpCodes.Callvirt;
                codes[pos].operand = m_VehiclePawn_GetStatValue;

                var g_VehiclePawn_VehicleDef = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.VehicleDef));
                var pos2 = codes.FindLastIndex(pos, c => c.opcode == OpCodes.Callvirt && c.OperandIs(g_VehiclePawn_VehicleDef));
                if (pos2 != -1)
                {
                    codes.RemoveAt(pos2);
                }
            }
            return codes;
        }
    }

    //VehicleTurretがdefaultAngleRotatedのままだとセーブされないので、forceSaveさせる
    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.ExposeData))]
    public static class Patch_VehicleTurret_ExposeData
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var f_defaultAngleRotated = AccessTools.Field(typeof(VehicleTurret), nameof(VehicleTurret.defaultAngleRotated));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_defaultAngleRotated)) + 1;

            codes[pos].opcode = OpCodes.Ldc_I4_1;
            return codes;
        }
    }

    [HarmonyPatch(typeof(LaunchProtocol), nameof(LaunchProtocol.GetFloatMenuOptionsAt))]
    public static class Patch_LaunchProtocol_GetFloatMenuOptionsAt
    {
        public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> __result, int tile, LaunchProtocol __instance)
        {
            foreach (var floatMenu in __result)
            {
                yield return floatMenu;
            }

            if (__instance.Vehicle is VehiclePawnWithMap)
            {
                yield break;
            }

            IEnumerable<VehiclePawnWithMap> vehicles = null;
            MapParent mapParent;
            Caravan caravan;
            AerialVehicleInFlight aerial;
            if ((mapParent = Find.WorldObjects.MapParentAt(tile)) != null && mapParent.HasMap)
            {
                vehicles = VehiclePawnWithMapCache.AllVehiclesOn(mapParent.Map);
            }
            else if ((caravan = Find.WorldObjects.PlayerControlledCaravanAt(tile)) != null)
            {
                if (caravan is VehicleCaravan vehicleCaravan)
                {
                    vehicles = vehicleCaravan.Vehicles.OfType<VehiclePawnWithMap>();
                }
                else
                {
                    vehicles = caravan.pawns.OfType<VehiclePawnWithMap>();
                }
            }
            else if ((aerial = VehicleWorldObjectsHolder.Instance.AerialVehicles.FirstOrDefault(a => a.Tile == tile)) != null)
            {
                vehicles = aerial.Vehicles.OfType<VehiclePawnWithMap>();
            }

            if (vehicles.NullOrEmpty()) yield break;

            foreach (var vehicle in vehicles)
            {
                mapParent = vehicle.VehicleMap.Parent;

                bool CanLandInSpecificCell()
                {
                    if (mapParent != null && mapParent.HasMap)
                    {
                        if (mapParent.EnterCooldownBlocksEntering())
                        {
                            return FloatMenuAcceptanceReport.WithFailMessage("MessageEnterCooldownBlocksEntering".Translate(mapParent.EnterCooldownTicksLeft().ToStringTicksToPeriod()));
                        }

                        return true;
                    }

                    return false;
                }

                if (CanLandInSpecificCell())
                {
                    var floatMenu = (FloatMenuOption)AccessTools.Method(__instance.GetType(), "FloatMenuOption_LandInsideMap").Invoke(__instance, new object[] { mapParent, tile });
                    floatMenu.action += MapComponentCache<VehiclePawnWithMapCache>.GetComponent(vehicle.VehicleMap).ForceResetCache;
                    floatMenu.Label = "VMF_LandInSpecificMap".Translate(vehicle.VehicleMap.Parent.Label, __instance.Vehicle.Label);
                    yield return floatMenu;
                }
            }
        }
    }

    //カーソルが画面からマップの表示範囲から大きく外れた時に範囲外でGetRoofしようとしてしまう、おそらくVF本体のバグ修正
    [HarmonyPatch(typeof(Ext_Vehicles), nameof(Ext_Vehicles.IsRoofRestricted), typeof(IntVec3), typeof(Map), typeof(bool))]
    public static class Patch_Ext_Vehicles_IsRoofRestricted
    {
        public static bool Prefix(IntVec3 cell, Map map, ref bool __result)
        {
            if (!cell.InBounds(map))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    //VehicleMap上を右クリックしている時は複数ポーンのVehicle乗り込みフロートメニューをオフにする
    [HarmonyPatch(typeof(SelectionHelper), nameof(SelectionHelper.MultiSelectClicker))]
    public static class Patch_SelectionHelper_MultiSelectClicker
    {
        public static bool Prefix(ref bool __result)
        {
            if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out _, false))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    //aerialVehicleInFlight.vehicle.AllPawnsAboardのとこにnullチェックを追加
    [HarmonyPatch(typeof(Ext_Vehicles), nameof(Ext_Vehicles.GetAerialVehicle))]
    public static class Patch_Ext_Vehicles_GetAerialVehicle
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var g_AllPawnsAboard = AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.AllPawnsAboard));
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(g_AllPawnsAboard));
            var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Brfalse_S);
            var label = codes[pos2].operand;
            var label2 = generator.DefineLabel();

            codes[pos].labels.Add(label2);
            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Brtrue_S, label2),
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Br_S, label)
            });
            return codes;
        }
    }

    //主にRepairVehicleに使用される。ターゲットAをVehicleとしてターゲットB(=直す場所)に先に向かうため、StartGotoDestMapJobを挟む
    [HarmonyPatch(typeof(JobDriver_WorkVehicle), "MakeNewToils")]
    public static class Patch_JobDriver_WorkVehicle_MakeNewToils
    {
        public static IEnumerable<Toil> Postfix(IEnumerable<Toil> values, Pawn ___pawn, Job ___job, JobDriver_WorkVehicle __instance)
        {
            var thingMap = ___job.targetA.Thing?.Map;
            if (thingMap != ___pawn.Map && ___pawn.CanReach(___job.targetA.Thing, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, thingMap, out var exitSpot, out var enterSpot))
            {
                yield return Toils_General.Do(() =>
                {
                    JobAcrossMapsUtility.StartGotoDestMapJob(___pawn, exitSpot, enterSpot);
                });
            }
            foreach (var toil in values)
            {
                yield return toil;
            }
        }
    }

    //WorkGiver_RefuelVehicleTurretでVehicleが海上に居た場合Regionがnullでエラーを吐いていた問題の修正
    [HarmonyPatch(typeof(WorkGiver_RefuelVehicleTurret), nameof(WorkGiver_RefuelVehicleTurret.JobOnThing))]
    public static class Patch_WorkGiver_RefuelVehicleTurret_JobOnThing
    {
        public static bool Prefix(Thing thing)
        {
            return thing.Position.GetRegion(thing.Map) != null;
        }
    }

    [HarmonyPatch]
    public static class Patch_Dialog_FormVehicleCaravan_TryFindExitSpot
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(Dialog_FormVehicleCaravan), t => t.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("<TryFindExitSpot>b__2")));
            yield return AccessTools.Method(typeof(Dialog_FormVehicleCaravan), "TryFindExitSpot",
                new Type[] { typeof(Map), typeof(List<Pawn>), typeof(bool), typeof(Rot4), typeof(IntVec3).MakeByRefType(), typeof(bool) });
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            if (instructions.FirstOrDefault(c => c.OperandIs(MethodInfoCache.m_ReachabilityUtility_CanReach)) == null) Log.Error(method.Name);
            return instructions.MethodReplacer(MethodInfoCache.m_ReachabilityUtility_CanReach, MethodInfoCache.m_ReachabilityUtilityOnVehicle_CanReach);
        }
    }

    //[HarmonyPatch(typeof(Vehicle_PathFollower), "TryEnterNextPathCell")]
    //public static class Patch_Vehicle_PathFollower_TryEnterNextPathCell
    //{
    //    public static bool Prefix(Vehicle_PathFollower __instance, VehiclePawn ___vehicle, ref int ___waitTicks, ref IntVec3 ___lastCell, IntVec3 ___nextCell, HashSet<IntVec3> ___hitboxUpdateCells)
    //    {
    //        var size = ___vehicle.VehicleDef.Size;
    //        if (size.x * size.z > 100)
    //        {
    //            TryEnterNextPathCell(__instance, ___vehicle, ref ___waitTicks, ref ___lastCell, ___nextCell, ___hitboxUpdateCells);
    //            return false;
    //        }
    //        return true;
    //    }

    //    private static void TryEnterNextPathCell(Vehicle_PathFollower __instance, VehiclePawn ___vehicle, ref int ___waitTicks, ref IntVec3 ___lastCell, IntVec3 ___nextCell, HashSet<IntVec3> ___hitboxUpdateCells)
    //    {
    //        if (___waitTicks > 0)
    //        {
    //            ___waitTicks--;
    //        }
    //        else
    //        {
    //            if (__instance.CalculatingPath)
    //            {
    //                return;
    //            }

    //            CellRect cellRect = ___vehicle.OccupiedRect();
    //            ___lastCell = ___vehicle.Position;
    //            ___vehicle.Position = ___nextCell;
    //            ___hitboxUpdateCells.Clear();
    //            ___hitboxUpdateCells.AddRange(cellRect);
    //            ___hitboxUpdateCells.AddRange(___vehicle.OccupiedRect());
    //            Parallel.ForEach(___hitboxUpdateCells, c =>
    //            {
    //                ___vehicle.Map.pathing.RecalculatePerceivedPathCostAt(c);
    //            });
    //            ___hitboxUpdateCells.Clear();

    //            if (VehicleMod.settings.main.runOverPawns)
    //            {
    //                float num = Mathf.Max(1f, __instance.nextCellCostTotal / 450f);
    //                float num2 = 1f / (__instance.nextCellCostTotal / 60f / num);
    //                if (___vehicle.FullRotation.IsDiagonal)
    //                {
    //                    num2 *= Ext_Math.Sqrt2;
    //                }

    //                WarnPawnsImpendingCollision(__instance);
    //                ___vehicle.CheckForCollisions(num2);
    //            }

    //            if (___vehicle.beached)
    //            {
    //                ___vehicle.BeachShip();
    //                ___vehicle.Position = ___nextCell;
    //                __instance.PatherFailed();
    //                return;
    //            }

    //            if (___vehicle.BodySize > 0.9f)
    //            {
    //                ___vehicle.Map.snowGrid.AddDepth(___vehicle.Position, -0.001f);
    //            }

    //            switch (NeedNewPath(__instance))
    //            {
    //                case PathRequest.Fail:
    //                    __instance.PatherFailed();
    //                    return;
    //                case PathRequest.Wait:
    //                    ___waitTicks = 10;
    //                    return;
    //                case PathRequest.NeedNew:
    //                    TrySetNewPath_Threaded(__instance);
    //                    break;
    //                default:
    //                    throw new NotImplementedException("TryEnterNextPathCell.PathRequest");
    //                case PathRequest.None:
    //                    break;
    //            }

    //            if (__instance.curPath != null)
    //            {
    //                if ((bool)AtDestinationPosition(__instance))
    //                {
    //                    PatherArrived(__instance);
    //                    return;
    //                }

    //                SetupMoveIntoNextCell(__instance);
    //                Ext_Map.GetCachedMapComponent<VehiclePositionManager>(___vehicle.Map).ClaimPosition(___vehicle);
    //            }
    //        }
    //    }

    //    private static FastInvokeHandler WarnPawnsImpendingCollision = MethodInvoker.GetHandler(AccessTools.Method(typeof(Vehicle_PathFollower), "WarnPawnsImpendingCollision"));

    //    private static FastInvokeHandler NeedNewPath = MethodInvoker.GetHandler(AccessTools.Method(typeof(Vehicle_PathFollower), "NeedNewPath"));

    //    private static FastInvokeHandler TrySetNewPath_Threaded = MethodInvoker.GetHandler(AccessTools.Method(typeof(Vehicle_PathFollower), "TrySetNewPath_Threaded"));

    //    private static FastInvokeHandler AtDestinationPosition = MethodInvoker.GetHandler(AccessTools.Method(typeof(Vehicle_PathFollower), "AtDestinationPosition"));

    //    private static FastInvokeHandler PatherArrived = MethodInvoker.GetHandler(AccessTools.Method(typeof(Vehicle_PathFollower), "PatherArrived"));

    //    private static FastInvokeHandler SetupMoveIntoNextCell = MethodInvoker.GetHandler(AccessTools.Method(typeof(Vehicle_PathFollower), "SetupMoveIntoNextCell"));
    //}

    //サイズの大きなVehicleの場合はVF本体のスレッド管理を回避しつつ独自にマルチスレッド化
    [HarmonyPatch]
    public static class Patch_VehiclePathing_SetRotationAndUpdateVehicleRegionsClipping
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method("Vehicles.VehiclePathing:SetRotationAndUpdateVehicleRegionsClipping");
        }

        public static bool Prefix(ref bool __result, object[] __args, HashSet<IntVec3> ___hitboxUpdateCells)
        {
            if (VehicleInteriors.settings.threadingPathCost && __args[0] is VehiclePawnWithMap vehicle && vehicle.Spawned && vehicle.def.size.x * vehicle.def.size.z > VehicleInteriors.settings.minAreaForThreading)
            {
                __result = SetRotationAndUpdateVehicleRegionsClipping(vehicle, (Rot4)__args[1], ___hitboxUpdateCells);
                return false;
            }
            return true;
        }

        private static bool SetRotationAndUpdateVehicleRegionsClipping(VehiclePawn vehicle, Rot4 value, HashSet<IntVec3> hitboxUpdateCells)
        {
            var map = vehicle.Map;
            if (!vehicle.OccupiedRectShifted(IntVec2.Zero, new Rot4?(value)).InBounds(map))
            {
                return false;
            }
            hitboxUpdateCells.Clear();
            hitboxUpdateCells.AddRange(vehicle.OccupiedRectShifted(IntVec2.Zero, new Rot4?(Rot4.East)));
            hitboxUpdateCells.AddRange(vehicle.OccupiedRectShifted(IntVec2.Zero, new Rot4?(Rot4.North)));
            var mapping = MapComponentCache<VehicleMapping>.GetComponent(map);
            var allMoveableVehicleDefs = (List<VehicleDef>)AllMoveableVehicleDefs(null);
            Parallel.ForEach(hitboxUpdateCells, c =>
            {
                foreach (VehicleDef vehicleDef in allMoveableVehicleDefs)
                {
                    if (c.InBounds(map))
                    {
                        mapping[vehicleDef].VehiclePathGrid.RecalculatePerceivedPathCostAt(c, map.thingGrid.ThingsListAtFast(c));
                    }
                }
            });
            hitboxUpdateCells.Clear();
            return true;
        }

        private static FastInvokeHandler AllMoveableVehicleDefs = MethodInvoker.GetHandler(AccessTools.PropertyGetter("Vehicles.VehicleHarmony:AllMoveableVehicleDefs"));
    }

    [HarmonyPatch(typeof(EnterMapUtilityVehicles), nameof(EnterMapUtilityVehicles.EnterAndSpawn))]
    public static class Patch_EnterMapUtilityVehicles_EnterAndSpawn
    {
        public static Exception Finalizer(Exception __exception)
        {
            if (__exception != null)
            {
                Messages.Message("VMF_FailedEnterMap".Translate(), MessageTypeDefOf.NegativeEvent);
            }
            return null;
        }
    }

    //車両マップ上からLoadVehicleをしようとした時など
    [HarmonyPatch(typeof(JobDriver_LoadVehicle), nameof(JobDriver_LoadVehicle.FailJob))]
    public static class Patch_JobDriver_LoadVehicle_FailJob
    {
        public static void Postfix(JobDriver_LoadVehicle __instance, ref bool __result)
        {
            if (__result)
            {
                var map = __instance.pawn.MapHeld;
                var maps = map.BaseMapAndVehicleMaps().Except(map);
                if (maps.Any(m => MapComponentCache<VehicleReservationManager>.GetComponent(m).VehicleListed(__instance.Vehicle, __instance.ListerTag)))
                {
                    __result = false;
                }
            }
        }
    }
}