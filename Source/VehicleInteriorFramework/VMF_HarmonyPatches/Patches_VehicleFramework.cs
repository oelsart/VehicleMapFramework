using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using static VehicleInteriors.MethodInfoCache;

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

    [HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.FullRotation), MethodType.Getter)]
    public static class Patch_VehiclePawn_FullRotation
    {
        public static bool Prefix(VehiclePawn __instance, ref Rot8 __result)
        {
            return !__instance.TryGetFullRotation(ref __result);
        }
    }

    [HarmonyPatch("Vehicles.Rendering", "DrawSelectionBracketsVehicles")]
    public static class Patch_Rendering_DrawSelectionBracketsVehicles
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindLastIndex(c => c.opcode == OpCodes.Stloc_S && ((LocalBuilder)c.operand).LocalIndex == 4);
            var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
            var rot = generator.DeclareLocal(typeof(Rot8));
            var label = generator.DefineLabel();

            codes[pos].labels.Add(label);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(0),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_FullRotation),
                new CodeInstruction(OpCodes.Stloc_S, rot),
                new CodeInstruction(OpCodes.Ldloca_S, rot),
                new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Rot8_AsAngle),
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

    [HarmonyPatch(typeof(VehicleHarmonyOnMod), "ShaderFromAssetBundle")]
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

    [HarmonyPatch(typeof(TargetingHelper), nameof(TargetingHelper.BestAttackTarget))]
    public static class Patch_TargetingHelper_BestAttackTarget
    {
        public static void Postfix(VehicleTurret turret, TargetScanFlags flags, Predicate<Thing> validator, float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus, bool canTakeTargetsCloserThanEffectiveMinRange, ref IAttackTarget __result)
        {
            var searcher = turret.vehicle;
            var map = searcher.Thing.Map;
            if (!searcher.IsHashIntervalTick(10) || !map.BaseMapAndVehicleMaps().Except(map).Any()) return;

            var target = TargetingHelperOnVehicle.BestAttackTarget(turret, flags, validator, minDist, maxDist, locus, maxTravelRadiusFromLocus, canTakeTargetsCloserThanEffectiveMinRange);
            if (__result == null || target != null && (__result.Thing.Position - searcher.Thing.Position).LengthHorizontalSquared > (target.Thing.PositionOnBaseMap() - searcher.Thing.PositionOnBaseMap()).LengthHorizontalSquared)
            {
                __result = target;
            }
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.InRange))]
    public static class Patch_VehicleTurret_InRange
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.TryFindShootLineFromTo))]
    public static class Patch_VehicleTurret_TryFindShootLineFromTo
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.FireTurret))]
    public static class Patch_VehicleTurret_FireTurret
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
                .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
                .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
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
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
        }
    }

    [HarmonyPatch(typeof(TurretTargeter), "CurrentTargetUnderMouse")]
    public static class Patch_TurretTargeter_CurrentTargetUnderMouse
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.m_GenUI_TargetsAtMouse, CachedMethodInfo.m_GenUIOnVehicle_TargetsAtMouse);
        }
    }

    [HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.TargeterUpdate))]
    public static class Patch_TurretTargeter_TargeterUpdate
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.ProcessInputEvents))]
    public static class Patch_TurretTargeter_ProcessInputEvents
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.TargeterValid), MethodType.Getter)]
    public static class Patch_TurretTargeter_TargeterValid
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
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
            yield return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(Dialog_FormVehicleCaravan), t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryFindExitSpot>b__2")));
            yield return AccessTools.Method(typeof(Dialog_FormVehicleCaravan), "TryFindExitSpot",
                new Type[] { typeof(Map), typeof(List<Pawn>), typeof(bool), typeof(Rot4), typeof(IntVec3).MakeByRefType(), typeof(bool) });
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            if (instructions.FirstOrDefault(c => c.OperandIs(CachedMethodInfo.m_ReachabilityUtility_CanReach)) == null) Log.Error(method.Name);
            return instructions.MethodReplacer(CachedMethodInfo.m_ReachabilityUtility_CanReach, CachedMethodInfo.m_ReachabilityUtilityOnVehicle_CanReach);
        }
    }

    //ポーンがVehicleRoleBuildableに割り当てられている時はその席へのCanReachにすり替える
    [HarmonyPatch(typeof(Dialog_FormVehicleCaravan), "CheckForErrors")]
    public static class Patch_Dialog_FormVehicleCaravan_CheckForErrors
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalIndex == 11) + 1;

            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(13),
                CodeInstruction.Call(typeof(Patch_Dialog_FormVehicleCaravan_CheckForErrors), nameof(TargetThing))
            });
            return codes;
        }

        private static Thing TargetThing(VehiclePawn vehicle, Pawn pawn)
        {
            if (CaravanHelper.assignedSeats.TryGetValue(pawn, out var assignedSeat) && assignedSeat.handler.role is VehicleRoleBuildable vehicleRoleBuildable)
            {
                return vehicleRoleBuildable.upgradeComp.parent;
            }
            return vehicle;
        }
    }

    //キャラバン編成画面でVehicleRoleBuildableに割り当てられているポーンはその席へ行くようにする
    [HarmonyPatch(typeof(JobDriver_Board), "MakeNewToils")]
    public static class Patch_JobDriver_Board_MakeNewToils
    {
        public static IEnumerable<Toil> Postfix(IEnumerable<Toil> values)
        {
            foreach (Toil toil in values)
            {
                if (toil.debugName == "GotoThing")
                {
                    var oldAction = toil.initAction;
                    toil.initAction = () =>
                    {
                        var actor = toil.actor;
                        if (actor.GetLord()?.LordJob is LordJob_FormAndSendVehicles lordJob_FormAndSendVehicles &&
                        lordJob_FormAndSendVehicles.GetVehicleAssigned(actor).handler?.role is VehicleRoleBuildable vehicleRoleBuildable)
                        {
                            var dest = vehicleRoleBuildable.upgradeComp?.parent;
                            if (!dest?.Spawned ?? true || ToilFailConditions.DespawnedOrNull(dest, actor))
                            {
                                actor.jobs.EndCurrentJob(JobCondition.Incompletable, canReturnToPool: false);
                                return;
                            }
                            actor.pather.StartPath(dest, PathEndMode.Touch);
                            return;
                        }
                        oldAction();
                    };
                }
                yield return toil;
            }
        }
    }

    //サイズの大きなVehicleの場合はVF本体のスレッド管理を回避しつつ独自にマルチスレッド化
    //最新版では本体で直ってるからたぶん要らない
    //[HarmonyPatch]
    //public static class Patch_VehiclePathing_SetRotationAndUpdateVehicleRegionsClipping
    //{
    //    private static MethodBase TargetMethod()
    //    {
    //        return AccessTools.Method("Vehicles.VehiclePathing:SetRotationAndUpdateVehicleRegionsClipping");
    //    }

    //    public static bool Prefix(ref bool __result, object[] __args, HashSet<IntVec3> ___hitboxUpdateCells)
    //    {
    //        if (VehicleInteriors.settings.threadingPathCost && __args[0] is VehiclePawnWithMap vehicle && vehicle.Spawned && vehicle.def.size.x * vehicle.def.size.z > VehicleInteriors.settings.minAreaForThreading)
    //        {
    //            __result = SetRotationAndUpdateVehicleRegionsClipping(vehicle, (Rot4)__args[1], ___hitboxUpdateCells);
    //            return false;
    //        }
    //        return true;
    //    }

    //    private static bool SetRotationAndUpdateVehicleRegionsClipping(VehiclePawn vehicle, Rot4 value, HashSet<IntVec3> hitboxUpdateCells)
    //    {
    //        var map = vehicle.Map;
    //        if (!vehicle.OccupiedRectShifted(IntVec2.Zero, new Rot4?(value)).InBounds(map))
    //        {
    //            return false;
    //        }
    //        hitboxUpdateCells.Clear();
    //        hitboxUpdateCells.AddRange(vehicle.OccupiedRectShifted(IntVec2.Zero, new Rot4?(Rot4.East)));
    //        hitboxUpdateCells.AddRange(vehicle.OccupiedRectShifted(IntVec2.Zero, new Rot4?(Rot4.North)));
    //        var mapping = MapComponentCache<VehicleMapping>.GetComponent(map);
    //        var allMoveableVehicleDefs = (List<VehicleDef>)AllMoveableVehicleDefs(null);
    //        Parallel.ForEach(hitboxUpdateCells, c =>
    //        {
    //            foreach (VehicleDef vehicleDef in allMoveableVehicleDefs)
    //            {
    //                if (c.InBounds(map))
    //                {
    //                    mapping[vehicleDef].VehiclePathGrid.RecalculatePerceivedPathCostAt(c);
    //                }
    //            }
    //        });
    //        hitboxUpdateCells.Clear();
    //        return true;
    //    }

    //    private static FastInvokeHandler AllMoveableVehicleDefs = MethodInvoker.GetHandler(AccessTools.PropertyGetter("Vehicles.VehicleHarmony:AllMoveableVehicleDefs"));
    //}

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

    [HarmonyPatch(typeof(VehicleTabHelper_Passenger), nameof(VehicleTabHelper_Passenger.DrawPassengersFor))]
    public static class Patch_VehicleTabHelper_Passenger_DrawPassengersFor
    {
        public static void Postfix(ref float curY, Rect viewRect, Vector2 scrollPos, VehiclePawn vehicle, ref Pawn moreDetailsForPawn)
        {
            if (vehicle is VehiclePawnWithMap mapVehicle)
            {
                var draggedPawn = Patch_VehicleTabHelper_Passenger_DrawPassengersFor.draggedPawn();
                var pawns = mapVehicle.VehicleMap.mapPawns.AllPawnsSpawned;
                var rect = new Rect(0f, curY, viewRect.width - 48f, 25f + PawnRowHeight * pawns.Count);
                if (draggedPawn != null && Mouse.IsOver(rect) && draggedPawn.Map != mapVehicle.VehicleMap)
                {
                    transferToHolder() = mapVehicle.VehicleMap;
                    overDropSpot() = true;
                    Widgets.DrawHighlight(rect);
                }
                Widgets.ListSeparator(ref curY, viewRect.width, mapVehicle.LabelCap + "VMF_VehicleMap".Translate());

                if (DoRow == null) return;
                foreach (var pawn in pawns)
                {
                    if (DoRow(curY, viewRect, scrollPos, pawn, ref moreDetailsForPawn, true))
                    {
                        hoveringOverPawn() = pawn;
                    }
                    curY += PawnRowHeight;
                }
            }
        }

        public static AccessTools.FieldRef<Pawn> draggedPawn = AccessTools.StaticFieldRefAccess<Pawn>(AccessTools.Field(typeof(VehicleTabHelper_Passenger), "draggedPawn"));

        public static AccessTools.FieldRef<IThingHolder> transferToHolder = AccessTools.StaticFieldRefAccess<IThingHolder>(AccessTools.Field(typeof(VehicleTabHelper_Passenger), "transferToHolder"));

        public static AccessTools.FieldRef<bool> overDropSpot = AccessTools.StaticFieldRefAccess<bool>(AccessTools.Field(typeof(VehicleTabHelper_Passenger), "overDropSpot"));

        public static AccessTools.FieldRef<Pawn> hoveringOverPawn = AccessTools.StaticFieldRefAccess<Pawn>(AccessTools.Field(typeof(VehicleTabHelper_Passenger), "hoveringOverPawn"));

        private delegate bool DoRowGetter(float curY, Rect viewRect, Vector2 scrollPos, Pawn pawn, ref Pawn moreDetailsForPawn, bool highlight);

        private static DoRowGetter DoRow = AccessTools.MethodDelegate<DoRowGetter>(AccessTools.Method(typeof(VehicleTabHelper_Passenger), "DoRow"));

        private const float PawnRowHeight = 50f;
    }

    [HarmonyPatch(typeof(VehicleTabHelper_Passenger), nameof(VehicleTabHelper_Passenger.HandleDragEvent))]
    public static class Patch_VehicleTabHelper_Passenger_HandleDragEvent
    {
        public static bool Prefix()
        {
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                var draggedPawn = Patch_VehicleTabHelper_Passenger_DrawPassengersFor.draggedPawn();
                var transferToHolder = Patch_VehicleTabHelper_Passenger_DrawPassengersFor.transferToHolder();
                if (draggedPawn != null && transferToHolder != null)
                {
                    if (transferToHolder is Map map && map.IsVehicleMapOf(out var vehicle))
                    {
                        if (draggedPawn.ParentHolder is VehicleHandler vehicleHandler)
                        {
                            if (!draggedPawn.Spawned && TryFindSpawnSpot(vehicle, vehicleHandler, out var intVec))
                            {
                                vehicle.RemovePawn(draggedPawn);
                                GenSpawn.Spawn(draggedPawn, intVec, vehicle.VehicleMap, WipeMode.Vanish);
                                vehicleHandler.vehicle.EventRegistry[VehicleEventDefOf.PawnExited].ExecuteEvents();
                                SoundDefOf.Click.PlayOneShotOnCamera();
                            }
                        }
                        else if (!draggedPawn.Spawned && draggedPawn.IsWorldPawn() && TryFindSpawnSpot(vehicle, null, out var intVec))
                        {
                            Find.WorldPawns.RemovePawn(draggedPawn);
                            GenSpawn.Spawn(draggedPawn, intVec, vehicle.VehicleMap, WipeMode.Vanish);
                            SoundDefOf.Click.PlayOneShotOnCamera();
                        }
                        else if (draggedPawn.IsOnVehicleMapOf(out var vehicle2) && vehicle != vehicle2 && TryFindSpawnSpot(vehicle2, null, out intVec))
                        {
                            draggedPawn.DeSpawn();
                            GenSpawn.Spawn(draggedPawn, intVec, vehicle.VehicleMap, WipeMode.Vanish);
                            SoundDefOf.Click.PlayOneShotOnCamera();
                        }
                        else
                        {
                            Messages.Message("VMF_CannotSpawn".Translate(draggedPawn), MessageTypeDefOf.RejectInput, true);
                        }
                        Patch_VehicleTabHelper_Passenger_DrawPassengersFor.draggedPawn() = null;
                        return false;
                    }
                    else if(draggedPawn.IsOnVehicleMapOf(out vehicle))
                    {
                        if (transferToHolder is VehicleHandler vehicleHandler)
                        {
                            if (!vehicleHandler.CanOperateRole(draggedPawn))
                            {
                                Messages.Message("VF_HandlerNotEnoughRoom".Translate(draggedPawn, vehicleHandler.role.label), MessageTypeDefOf.RejectInput, true);
                                Patch_VehicleTabHelper_Passenger_DrawPassengersFor.draggedPawn() = null;
                                return false;
                            }
                            if (!vehicleHandler.AreSlotsAvailable)
                            {
                                var hoveringOverPawn = Patch_VehicleTabHelper_Passenger_DrawPassengersFor.hoveringOverPawn();
                                if (hoveringOverPawn != null)
                                {
                                    if (TryFindSpawnSpot(vehicle, vehicleHandler, out var intVec))
                                    {
                                        vehicle.RemovePawn(hoveringOverPawn);
                                        GenSpawn.Spawn(hoveringOverPawn, intVec, vehicle.VehicleMap, WipeMode.Vanish);
                                        vehicleHandler.vehicle.EventRegistry[VehicleEventDefOf.PawnExited].ExecuteEvents();
                                    }
                                    else
                                    {
                                        Messages.Message("VMF_CannotSpawn".Translate(hoveringOverPawn), MessageTypeDefOf.RejectInput, true);
                                        Patch_VehicleTabHelper_Passenger_DrawPassengersFor.draggedPawn() = null;
                                        return false;
                                    }
                                }
                                else
                                {
                                    Messages.Message("VF_HandlerNotEnoughRoom".Translate(draggedPawn, vehicleHandler.role.label), MessageTypeDefOf.RejectInput, true);
                                    Patch_VehicleTabHelper_Passenger_DrawPassengersFor.draggedPawn() = null;
                                    return false;
                                }
                            }
                        }

                        var pos = draggedPawn.Position;
                        draggedPawn.DeSpawn();
                        if (transferToHolder.GetDirectlyHeldThings().TryAddOrTransfer(draggedPawn, false))
                        {
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            if (transferToHolder is VehicleHandler vehicleHandler2)
                            {
                                vehicleHandler2.vehicle.EventRegistry[VehicleEventDefOf.PawnEntered].ExecuteEvents();
                            }
                            else if (!draggedPawn.IsWorldPawn())
                            {
                                Find.WorldPawns.PassToWorld(draggedPawn);
                            }
                        }
                        else
                        {
                            GenSpawn.Spawn(draggedPawn, pos, vehicle.VehicleMap);
                        }
                        Patch_VehicleTabHelper_Passenger_DrawPassengersFor.draggedPawn() = null;
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool TryFindSpawnSpot(VehiclePawnWithMap vehicle, VehicleHandler vehicleHandler, out IntVec3 spot)
        {
            bool Predicate(IntVec3 c)
            {
                return c.Standable(vehicle.VehicleMap) || c.GetDoor(vehicle.VehicleMap) != null;
            }

            if (vehicleHandler != null && vehicleHandler.vehicle == vehicle && vehicleHandler.role is VehicleRoleBuildable vehicleRoleBuildable)
            {
                var parent = vehicleRoleBuildable.upgradeComp.parent;
                CellRect cellRect = parent.OccupiedRect().ExpandedBy(1);
                IntVec3 intVec = parent.Position;
                if (cellRect.EdgeCells.Where(delegate (IntVec3 c)
                {
                    if (c.InBounds(vehicle.VehicleMap) && c.Standable(vehicle.VehicleMap))
                    {
                        return !c.GetThingList(vehicle.VehicleMap).NotNullAndAny((Thing t) => t is Pawn);
                    }
                    return false;
                }).TryRandomElement(out spot))
                {
                    return true;
                }
                spot = IntVec3.Invalid;
                return false;
            }
            if (vehicle.EnterComps.Any() && vehicle.EnterComps.Select(c => c.parent.Position).TryRandomElement(Predicate, out spot))
            {
                return true;
            }
            if (vehicle.CachedMapEdgeCells.TryRandomElement(Predicate, out spot))
            {
                return true;
            }
            var cell = vehicle.CachedMapEdgeCells.RandomElement();
            if (RCellFinder.TryFindRandomCellNearWith(cell, Predicate, vehicle.VehicleMap, out spot))
            {
                return true;
            }
            spot = IntVec3.Invalid;
            return false;
        }
    }
}