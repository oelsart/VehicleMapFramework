﻿using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Rendering;
using SmashTools.Targeting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Vehicles.Rendering;
using Vehicles.World;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(RGBMaterialPool), nameof(RGBMaterialPool.SetProperties), typeof(IMaterialCacheTarget), typeof(PatternData), typeof(Func<Rot8, Texture2D>), typeof(Func<Rot8, Texture2D>))]
[PatchLevel(Level.Safe)]
public static class Patch_RGBMaterialPool_SetProperties
{
    public static bool Prefix(IMaterialCacheTarget target, PatternData patternData,
    Func<Rot8, Texture2D> mainTexGetter, Func<Rot8, Texture2D> maskTexGetter, Dictionary<IMaterialCacheTarget, Material[]> ___cache)
    {
        if (target is GraphicOverlay graphicOverlay)
        {
            var vehiclePawn = vehicle(graphicOverlay);
            if (vehiclePawn != null && vehiclePawn.AllComps.OfType<CompOpacityOverlay>().Any(c => c.Props.identifier == graphicOverlay.data?.identifier))
            {
                if (___cache.TryGetValue(target, out var materials))
                {
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material material = materials[i];

                        material.SetColor(AdditionalShaderPropertyIDs.ColorOne, patternData.color);
                        material.SetColor(ShaderPropertyIDs.ColorTwo, patternData.colorTwo);
                        material.SetColor(AdditionalShaderPropertyIDs.ColorThree, patternData.colorThree);

                        Rot8 rot = new(i);
                        Texture2D mainTex = material.mainTexture as Texture2D;
                        if (mainTexGetter != null)
                        {
                            mainTex = mainTexGetter(rot);
                        }

                        Texture2D maskTex = maskTexGetter?.Invoke(rot);
                        if (patternData.patternDef != PatternDefOf.Default)
                        {
                            float tiles = patternData.tiles;
                            if (patternData.patternDef.properties.tiles.TryGetValue("All", out float allTiles))
                            {
                                tiles *= allTiles;
                            }

                            if (!Mathf.Approximately(tiles, 0))
                            {
                                material.SetFloat(AdditionalShaderPropertyIDs.TileNum, tiles);
                            }

                            if (patternData.patternDef.properties.equalize)
                            {
                                float scaleX = 1;
                                float scaleY = 1;
                                if (mainTex.width > mainTex.height)
                                {
                                    scaleY = (float)mainTex.height / mainTex.width;
                                }
                                else
                                {
                                    scaleX = (float)mainTex.width / mainTex.height;
                                }

                                material.SetFloat(AdditionalShaderPropertyIDs.ScaleX, scaleX);
                                material.SetFloat(AdditionalShaderPropertyIDs.ScaleY, scaleY);
                            }

                            if (patternData.patternDef.properties.dynamicTiling)
                            {
                                material.SetFloat(AdditionalShaderPropertyIDs.DisplacementX,
                                  patternData.displacement.x);
                                material.SetFloat(AdditionalShaderPropertyIDs.DisplacementY,
                                  patternData.displacement.y);
                            }
                        }

                        Shader opacityShader = patternData.patternDef.ShaderTypeDef.Shader.OpacityShaderCorrespond();
                        if (opacityShader != material.shader)
                        {
                            material.shader = opacityShader;
                        }

                        Texture2D patternTex = patternData.patternDef[rot];
                        if (patternData.patternDef.ShaderTypeDef == VehicleShaderTypeDefOf.CutoutComplexSkin)
                        {
                            //Null reverts to original tex. Default would calculate to red
                            material.SetTexture(AdditionalShaderPropertyIDs.SkinTex, patternTex);
                        }
                        else if (patternData.patternDef.ShaderTypeDef ==
                          VehicleShaderTypeDefOf.CutoutComplexPattern)
                        {
                            //Default to full red mask for full ColorOne pattern
                            material.SetTexture(AdditionalShaderPropertyIDs.PatternTex, patternTex);
                        }

                        material.mainTexture = mainTex;
                        if (maskTex != null)
                        {
                            material.SetTexture(ShaderPropertyIDs.MaskTex, maskTex);
                        }

                        material.SetColor(AdditionalShaderPropertyIDs.ColorOne, patternData.color);
                        material.SetColor(ShaderPropertyIDs.ColorTwo, patternData.colorTwo);
                        material.SetColor(AdditionalShaderPropertyIDs.ColorThree, patternData.colorThree);
                    }
                    return false;
                }
            }
        }
        return true;
    }

    private static AccessTools.FieldRef<GraphicOverlay, VehiclePawn> vehicle = AccessTools.FieldRefAccess<GraphicOverlay, VehiclePawn>("vehicle");
}

//VehiclePawnWithMapの場合Movementフラグを持つハンドラーが存在しない場合コントロールできないようにする
[HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.HasEnoughOperators), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_VehiclePawn_HasEnoughOperators
{
    public static bool Prefix(VehiclePawn __instance, ref bool __result)
    {
        if (__instance is VehiclePawnWithMap)
        {
            if (__instance.MovementPermissions.HasFlag(VehiclePermissions.Autonomous))
            {
                __result = true;
                return false;
            }
            var matchHandlers = __instance.handlers.Where(h => h.role.HandlingTypes.HasFlag(HandlingType.Movement)).ToList();
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
[PatchLevel(Level.Safe)]
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
            var matchHandlers = __instance.vehicle.handlers.FindAll(h => h.role.HandlingTypes.HasFlag(HandlingType.Turret) && (h.role.TurretIds.Contains(__instance.key) || h.role.TurretIds.Contains(__instance.groupKey)));
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
[PatchLevel(Level.Safe)]
public static class Patch_CompVehicleTurrets_CompGetGizmosExtra
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, CompVehicleTurrets __instance)
    {
        foreach (var gizmo in gizmos)
        {
            if (gizmo is Command_Turret command_Turret && __instance.Vehicle is VehiclePawnWithMap)
            {
                var turret = command_Turret.turret;
                if (turret != null &&
                    !command_Turret.Disabled &&
                    !VehicleMod.settings.debug.debugShootAnyTurret &&
                    !__instance.Vehicle.handlers.Any(h => h.role.handlingTypes.HasFlag(HandlingType.Turret) && (h.role.TurretIds?.Contains(!turret.groupKey.NullOrEmpty() ? turret.groupKey : turret.key) ?? false)))
                {
                    command_Turret.Disable("VMF_NoRoles".Translate(__instance.Vehicle.LabelShort));
                }
            }
            yield return gizmo;
        }
    }
}

[HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.DisembarkPawn))]
[PatchLevel(Level.Safe)]
public static class Patch_VehiclePawn_DisembarkPawn
{
    public static bool Prefix(Pawn pawn, VehiclePawn __instance)
    {
        var handler = __instance.handlers.First(h => h.thingOwner.Contains(pawn));
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
                        return !c.GetThingList(vehicle.VehicleMap).NotNullAndAny(t => t is Pawn);
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
[PatchLevel(Level.Sensitive)]
public static class Patch_VehiclePawn_FullRotation
{
    public static bool Prefix(VehiclePawn __instance, ref Rot8 __result)
    {
        return !__instance.TryGetFullRotation(ref __result);
    }
}

[HarmonyPatch("Vehicles.Patch_Rendering", "DrawSelectionBracketsVehicles")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Rendering_DrawSelectionBracketsVehicles
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_3);
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
        var label = generator.DefineLabel();

        codes[pos].labels.Add(label);
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadLocal(0),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Angle),
            new CodeInstruction(OpCodes.Conv_I4),
            new CodeInstruction(OpCodes.Add)
        ]);
        return codes;
    }
}

[HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.AngleBetween))]
[PatchLevel(Level.Safe)]
public static class Patch_VehicleTurret_AngleBetween
{
    public static void Prefix(VehicleTurret __instance, ref Vector3 position)
    {
        if (__instance.vehicle.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            position = Ext_Math.RotatePoint(position, __instance.TurretLocation, vehicle.FullRotation.AsAngle);
        }
    }
}

[HarmonyPatch(typeof(GenGridVehicles), nameof(GenGridVehicles.ImpassableForVehicles))]
[PatchLevel(Level.Mandatory)]
public static class Patch_GenGridVehicles_ImpassableForVehicles
{
    public static void Postfix(ThingDef thingDef, ref bool __result)
    {
        __result &= !thingDef.thingClass.SameOrSubclass(typeof(Building_VehicleRamp));
    }
}

[HarmonyPatch(typeof(TargetingHelper), "BestAttackTarget")]
[PatchLevel(Level.Safe)]
public static class Patch_TargetingHelper_BestAttackTarget
{
    public static void Postfix(VehicleTurret turret, TargetScanFlags flags, Predicate<Thing> validator, float minDist, float maxDist, IntVec3 locus, float maxTravelRadiusFromLocus, bool canTakeTargetsCloserThanEffectiveMinRange, ref IAttackTarget __result)
    {
        var searcher = turret.vehicle;
        var target = TargetingHelperOnVehicle.BestAttackTarget(turret, flags, validator, minDist, maxDist, locus, maxTravelRadiusFromLocus, canTakeTargetsCloserThanEffectiveMinRange);
        __result = AttackTargetFinderOnVehicle.CompareTarget(__result, target, searcher);
    }
}

[HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.InRange))]
[PatchLevel(Level.Cautious)]
public static class Patch_VehicleTurret_InRange
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.TryFindShootLineFromTo))]
[PatchLevel(Level.Cautious)]
public static class Patch_VehicleTurret_TryFindShootLineFromTo
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.FireTurret))]
[PatchLevel(Level.Cautious)]
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
[PatchLevel(Level.Safe)]
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
[PatchLevel(Level.Safe)]
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
[PatchLevel(Level.Sensitive)]
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
[PatchLevel(Level.Cautious)]
public static class Patch_TurretTargeter_BeginTargeting
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatch(typeof(TurretTargeter), "CurrentTargetUnderMouse")]
[PatchLevel(Level.Cautious)]
public static class Patch_TurretTargeter_CurrentTargetUnderMouse
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_GenUI_TargetsAtMouse, CachedMethodInfo.m_GenUIOnVehicle_TargetsAtMouse);
    }
}

[HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.TargeterUpdate))]
[PatchLevel(Level.Cautious)]
public static class Patch_TurretTargeter_TargeterUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.ProcessInputEvents))]
[PatchLevel(Level.Cautious)]
public static class Patch_TurretTargeter_ProcessInputEvents
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatch(typeof(TurretTargeter), "TargeterValid", MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_TurretTargeter_TargeterValid
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

//タレットの自動ロードがVehicleDefのCargoCapacityを参照してたので、これをVehiclePawnのインスタンスからステータスを参照させる
[HarmonyPatch(typeof(Command_CooldownAction), "DrawBottomBar")]
[PatchLevel(Level.Sensitive)]
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

[HarmonyPatch(typeof(LaunchProtocol), nameof(LaunchProtocol.GetArrivalOptions))]
[PatchLevel(Level.Safe)]
public static class Patch_LaunchProtocol_GetArrivalOptions
{
    public static IEnumerable<ArrivalOption> Postfix(IEnumerable<ArrivalOption> __result, GlobalTargetInfo target, LaunchProtocol __instance)
    {
        foreach (var arrivalOption in __result)
        {
            yield return arrivalOption;
        }

        if (__instance.Vehicle is VehiclePawnWithMap)
        {
            yield break;
        }

        IEnumerable<VehiclePawnWithMap> vehicles = null;
        if (target.WorldObject is MapParent mapParent && mapParent.HasMap)
        {
            vehicles = VehiclePawnWithMapCache.AllVehiclesOn(mapParent.Map);
        }
        else if (target.WorldObject is Caravan caravan)
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
        else if (target.WorldObject is AerialVehicleInFlight aerial)
        {
            vehicles = aerial.Vehicles.OfType<VehiclePawnWithMap>();
        }

        if (vehicles is null) yield break;

        foreach (var vehiclePawnWithMap in vehicles)
        {
            mapParent = vehiclePawnWithMap.VehicleMap.Parent;

            var vehicle = __instance.Vehicle;
            if (mapParent is { Spawned: true, HasMap: true } && !mapParent.EnterCooldownBlocksEntering())
            {
                yield return new ArrivalOption("LandInExistingMap".Translate(vehicle.Label),
                  continueWith: delegate (TargetData<GlobalTargetInfo> targetData)
                  {
                      Current.Game.CurrentMap = mapParent.Map;
                      CameraJumper.TryHideWorld();
                      LandingTargeter.Instance.BeginTargeting(vehicle,
                action: delegate (LocalTargetInfo landingCell, Rot4 rot)
                      {
                          if (vehicle.Spawned)
                          {
                              vehicle.CompVehicleLauncher.Launch(targetData,
                        new ArrivalAction_LandToCell(vehicle, mapParent, landingCell.Cell, rot));
                          }
                          else
                          {
                              AerialVehicleInFlight aerialVehicle = vehicle.GetOrMakeAerialVehicle();
                              List<FlightNode> nodes = targetData.targets.Select(target => new FlightNode(target)).ToList();
                              aerialVehicle.OrderFlyToTiles(nodes,
                        new ArrivalAction_LandToCell(vehicle, mapParent, landingCell.Cell, rot));
                              vehicle.CompVehicleLauncher.inFlight = true;
                              CameraJumper.TryShowWorld();
                          }
                      }, allowRotating: vehicle.VehicleDef.rotatable,
                targetValidator: targetInfo =>
                  !Ext_Vehicles.IsRoofRestricted(vehicle.VehicleDef, targetInfo.Cell, mapParent.Map));
                  });
            }
        }
    }
}

//カーソルが画面からマップの表示範囲から大きく外れた時に範囲外でGetRoofしようとしてしまう、おそらくVF本体のバグ修正
[HarmonyPatch(typeof(Ext_Vehicles), nameof(Ext_Vehicles.IsRoofRestricted), typeof(IntVec3), typeof(Map), typeof(bool))]
[PatchLevel(Level.Safe)]
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
[PatchLevel(Level.Safe)]
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
[PatchLevel(Level.Sensitive)]
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
        codes.InsertRange(pos,
        [
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Brtrue_S, label2),
            new CodeInstruction(OpCodes.Pop),
            new CodeInstruction(OpCodes.Br_S, label)
        ]);
        return codes;
    }
}

//主にRepairVehicleに使用される。ターゲットAをVehicleとしてターゲットB(=直す場所)に先に向かうため、StartGotoDestMapJobを挟む
[HarmonyPatch(typeof(JobDriver_WorkVehicle), "MakeNewToils")]
[PatchLevel(Level.Safe)]
public static class Patch_JobDriver_WorkVehicle_MakeNewToils
{
    public static IEnumerable<Toil> Postfix(IEnumerable<Toil> values, Pawn ___pawn, Job ___job)
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
[PatchLevel(Level.Safe)]
public static class Patch_WorkGiver_RefuelVehicleTurret_JobOnThing
{
    public static bool Prefix(Thing thing)
    {
        return thing.Position.GetRegion(thing.Map) != null;
    }
}

[HarmonyPatch(typeof(CaravanFormation), "TryFindExitSpot",
    [typeof(Map), typeof(List<Pawn>), typeof(bool), typeof(Rot4), typeof(IntVec3), typeof(bool)],
    [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal])]
[PatchLevel(Level.Safe)]
public static class Patch_CaravanFormation_TryFindExitSpot
{
    public static void Prefix(Map map)
    {
        CrossMapReachabilityUtility.DestMap = map;
    }

    public static void Finalizer()
    {
        CrossMapReachabilityUtility.DestMap = null;
    }
}

//ポーンがVehicleRoleBuildableに割り当てられている時はその席へのCanReachにすり替える
[HarmonyPatch(typeof(CaravanFormation), "CheckForErrors")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CaravanFormation_CheckForErrors
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var codes = instructions.ToList();

        //コンパイルごとにインデックスがころころ変わるのでここだけ多少変更に強くしてます
        var ind = original.GetMethodBody().LocalVariables.First(l => l.LocalType == typeof(VehiclePawn)).LocalIndex;
        var pos = codes.FindIndex(c =>
        {
            if (ind == 0) return c.opcode == OpCodes.Ldloc_0;
            if (ind == 1) return c.opcode == OpCodes.Ldloc_1;
            if (ind == 2) return c.opcode == OpCodes.Ldloc_2;
            if (ind == 3) return c.opcode == OpCodes.Ldloc_3;
            var localBuilder = codes.Select(c => c.operand).OfType<LocalBuilder>().First(l => l.LocalIndex == ind);
            return c.IsLdloc(localBuilder);
        });

        codes.InsertRange(pos + 1,
        [
            CodeInstruction.LoadLocal(ind + 2),
            CodeInstruction.Call(typeof(Patch_CaravanFormation_CheckForErrors), nameof(TargetThing))
        ]);
        return codes;
    }

    private static Thing TargetThing(VehiclePawn vehicle, Pawn pawn)
    {
        AssignedSeat assignedSeat = CaravanHelper.assignedSeats.GetAssignment(pawn);
        if (assignedSeat != null && assignedSeat.handler.role is VehicleRoleBuildable vehicleRoleBuildable)
        {
            return vehicleRoleBuildable.upgradeComp.parent;
        }
        return vehicle;
    }
}

//キャラバン編成画面でVehicleRoleBuildableに割り当てられているポーンはその席へ行くようにする
[HarmonyPatch(typeof(JobDriver_Board), "MakeNewToils")]
[PatchLevel(Level.Safe)]
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
                        if (!dest?.Spawned ?? (true || ToilFailConditions.DespawnedOrNull(dest, actor)))
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

[HarmonyPatch(typeof(EnterMapUtilityVehicles), nameof(EnterMapUtilityVehicles.EnterAndSpawn))]
[PatchLevel(Level.Safe)]
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
[HarmonyPatch(typeof(JobDriver_LoadVehicle), "FailJob")]
[PatchLevel(Level.Safe)]
public static class Patch_JobDriver_LoadVehicle_FailJob
{
    public static void Postfix(JobDriver_LoadVehicle __instance, ref bool __result)
    {
        if (__result)
        {
            var map = __instance.pawn.MapHeld;
            var maps = map.BaseMapAndVehicleMaps().Except(map);
            var vehicle = __instance.job.GetTarget(TargetIndex.B).Thing as VehiclePawn;
            if (maps.Any(m => MapComponentCache<VehicleReservationManager>.GetComponent(m).VehicleListed(vehicle, ListerTag(__instance))))
            {
                __result = false;
            }
        }
    }

    private static readonly Func<JobDriver_LoadVehicle, string> ListerTag = (Func<JobDriver_LoadVehicle, string>)AccessTools.PropertyGetter(typeof(JobDriver_LoadVehicle), "ListerTag").CreateDelegate(typeof(Func<JobDriver_LoadVehicle, string>));
}

[HarmonyPatch(typeof(VehicleTabHelper_Passenger), nameof(VehicleTabHelper_Passenger.DrawPassengersFor))]
[PatchLevel(Level.Safe)]
public static class Patch_VehicleTabHelper_Passenger_DrawPassengersFor
{
    public static void Postfix(ref float curY, Rect viewRect, Vector2 scrollPos, VehiclePawn vehicle, ref Pawn moreDetailsForPawn)
    {
        if (vehicle is VehiclePawnWithMap mapVehicle)
        {
            var draggedPawn = Patch_VehicleTabHelper_Passenger_DrawPassengersFor.draggedPawn();
            var pawns = mapVehicle.VehicleMap.mapPawns.AllPawnsSpawned;
            var rect = new Rect(0f, curY, viewRect.width - 48f, 25f + (PawnRowHeight * pawns.Count));
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
[PatchLevel(Level.Safe)]
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
                    if (draggedPawn.ParentHolder is VehicleRoleHandler vehicleHandler)
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
                else if (draggedPawn.IsOnVehicleMapOf(out vehicle))
                {
                    if (transferToHolder is VehicleRoleHandler vehicleHandler)
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
                        if (transferToHolder is VehicleRoleHandler vehicleHandler2)
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

    private static bool TryFindSpawnSpot(VehiclePawnWithMap vehicle, VehicleRoleHandler vehicleHandler, out IntVec3 spot)
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
                    return !c.GetThingList(vehicle.VehicleMap).NotNullAndAny(t => t is Pawn);
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

//非MultiSelect時は既にターゲットマップある想定
[HarmonyPatch(typeof(FloatMenuOptionProvider_OrderVehicle), "VehicleCanGoto")]
[PatchLevel(Level.Safe)]
public static class Patch_FloatMenuOptionProvider_OrderVehicle_VehicleCanGoto
{
    public static bool Prefix(VehiclePawn vehicle, IntVec3 gotoLoc, ref AcceptanceReport __result)
    {
        if (TargetMapManager.HasTargetMap(vehicle, out var map))
        {
            if (!vehicle.CanReachVehicle(gotoLoc, PathEndMode.OnCell, Danger.Deadly, TraverseMode.ByPawn, map, out _, out _))
            {
                __result = "VF_CannotMoveToCell".Translate(vehicle.LabelCap);
            }
            else
            {
                __result = true;
            }
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(FloatMenuOptionProvider_OrderVehicle), "PawnGotoAction")]
[PatchLevel(Level.Safe)]
public static class Patch_FloatMenuOptionProvider_OrderVehicle_PawnGotoAction
{
    public static bool Prefix(IntVec3 clickCell, VehiclePawn vehicle, IntVec3 gotoLoc, Rot8 rot)
    {
        if (TargetMapManager.HasTargetMap(vehicle, out var map))
        {
            if (vehicle.CanReachVehicle(gotoLoc, PathEndMode.OnCell, Danger.Deadly, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
            {
                PawnGotoAction(clickCell, vehicle, map, gotoLoc, rot, exitSpot, enterSpot);
                TargetMapManager.RemoveTargetInfo(vehicle);
            }
            return false;
        }
        return true;
    }

    public static void PawnGotoAction(IntVec3 clickCell, VehiclePawn vehicle, Map map, IntVec3 gotoLoc, Rot8 rot, TargetInfo exitSpot, TargetInfo enterSpot)
    {
        bool jobSuccess;
        if (vehicle.Map == map && vehicle.Position == gotoLoc)
        {
            jobSuccess = true;
            vehicle.FullRotation = rot;
            if (vehicle.CurJobDef == VMF_DefOf.VMF_GotoAcrossMaps)
            {
                vehicle.jobs.EndCurrentJob(JobCondition.Succeeded);
            }
        }
        else
        {
            if (vehicle.CurJobDef == VMF_DefOf.VMF_GotoAcrossMaps &&
                vehicle.jobs?.curDriver is JobDriverAcrossMaps driver && driver.DestMap == map &&
                vehicle.CurJob.targetA.Cell == gotoLoc)
            {
                jobSuccess = true;
            }
            else
            {
                Job job = new(VMF_DefOf.VMF_GotoAcrossMaps, gotoLoc);
                job.SetSpotsToJobAcrossMaps(vehicle, exitSpot, enterSpot);
                job.globalTarget = new GlobalTargetInfo(gotoLoc, map);
                var baseMap = map.BaseMap();
                var isBaseMap = map == baseMap;
                bool isOnEdge = isBaseMap && CellRect.WholeMap(baseMap).IsOnEdge(clickCell, 3);
                bool exitCell = isBaseMap && baseMap.exitMapGrid.IsExitCell(clickCell);
                bool vehicleCellsOverlapExit = isBaseMap && vehicle.InhabitedCellsProjected(clickCell, rot)
                 .NotNullAndAny(cell => cell.InBounds(baseMap) &&
                    baseMap.exitMapGrid.IsExitCell(cell));

                if (exitCell || vehicleCellsOverlapExit)
                {
                    job.exitMapOnArrival = true;
                }
                else if (!baseMap.IsPlayerHome && !baseMap.exitMapGrid.MapUsesExitGrid &&
                  isOnEdge &&
                  baseMap.Parent.GetComponent<FormCaravanComp>() is { } formCaravanComp &&
                  MessagesRepeatAvoider.MessageShowAllowed(
                    $"MessagePlayerTriedToLeaveMapViaExitGrid-{baseMap.uniqueID}", 60f))
                {
                    string text = formCaravanComp.CanFormOrReformCaravanNow ?
                      "MessagePlayerTriedToLeaveMapViaExitGrid_CanReform".Translate() :
                      "MessagePlayerTriedToLeaveMapViaExitGrid_CantReform".Translate();
                    Messages.Message(text, baseMap.Parent, MessageTypeDefOf.RejectInput, false);
                }
                jobSuccess = vehicle.jobs.TryTakeOrderedJob(job, JobTag.Misc);

                if (jobSuccess)
                    vehicle.vehiclePather.SetEndRotation(rot);
            }
        }
        if (jobSuccess)
            FleckMaker.Static(gotoLoc, map, FleckDefOf.FeedbackGoto);
    }
}

[HarmonyPatch(typeof(PathingHelper), nameof(PathingHelper.TryFindNearestStandableCell))]
[PatchLevel(Level.Safe)]
public static class Patch_PathingHelper_TryFindNearestStandableCell
{
    public static bool Prefix(VehiclePawn vehicle, IntVec3 cell, ref IntVec3 result, ref float radius, ref bool __result)
    {
        if (radius < 0f)
        {
            radius = Mathf.Min(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z) * 2;
        }
        radius = Mathf.Min(radius, 56.4f);
        VehiclePawnWithMap vehicle2 = null;
        if (TargetMapManager.HasTargetMap(vehicle, out var map))
        {
            __result = CrossMapReachabilityUtility.TryFindNearestStandableCell(vehicle, cell, map, out result, radius);
            if (result.IsValid)
            {
                return false;
            }
        }
        else if ((cell.InBounds(Find.CurrentMap) && cell.TryGetVehicleMap(Find.CurrentMap, out vehicle2)) || vehicle.IsOnNonFocusedVehicleMapOf(out _))
        {
            var dest = vehicle2 != null ? cell.ToVehicleMapCoord(vehicle2) : cell;
            map = vehicle2 != null ? vehicle2.VehicleMap : Find.CurrentMap;
            __result = CrossMapReachabilityUtility.TryFindNearestStandableCell(
                vehicle,
                dest,
                map,
                out result,
                radius);
            if (result.IsValid)
            {
                TargetMapManager.SetTargetMap(vehicle, map);
                return false;
            }
        }
        return true;
    }
}

[HarmonyPatch(typeof(VehiclePath), nameof(VehiclePath.DrawPath))]
public static class Patch_VehiclePath_DrawPath
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) => Patch_PawnPath_DrawPath.Transpiler(instructions, generator);
}

[HarmonyPatch(typeof(VehicleOrientationController), "Init")]
[PatchLevel(Level.Safe)]
public static class Patch_VehicleOrientationController_Init
{
    public static void Postfix(ref IntVec3 ___start, ref IntVec3 ___end)
    {
        if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle, false))
        {
            ___start = ___start.ToBaseMapCoord(vehicle);
            ___end = ___end.ToBaseMapCoord(vehicle);
        }
    }


}

[HarmonyPatch(typeof(VehicleOrientationController), "RecomputeDestinations")]
public static class Patch_VehicleOrientationController_RecomputeDestinations
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(List<VehiclePawn> ___vehicles)
    {
        if (___vehicles.Count > 1)
        {
            ___vehicles.Do(v => TargetMapManager.RemoveTargetInfo(v));
        }
    }

    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnTargetMap);
    }
}

[HarmonyPatch(typeof(VehicleOrientationController), nameof(VehicleOrientationController.TargeterUpdate))]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehicleOrientationController_TargeterUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_ToVector3ShiftedWithAltitude = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3ShiftedWithAltitude), [typeof(float)]);
        var m_ToVector3ShiftedOffsetWithAltitude = AccessTools.Method(typeof(Patch_MultiPawnGotoController_Draw), "ToVector3ShiftedOffsetWithAltitude");
        var num = 0;
        var ind = instructions.Select(c => c.operand).OfType<LocalBuilder>().First(l => l.LocalType == typeof(VehiclePawn)).LocalIndex;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(m_ToVector3ShiftedWithAltitude))
            {
                num++;
                if (num > 2)
                {
                    yield return CodeInstruction.LoadLocal(ind);
                    instruction.operand = m_ToVector3ShiftedOffsetWithAltitude;
                }
            }
            yield return instruction;
        }
    }
}

[HarmonyPatch(typeof(VehicleGhostUtility), nameof(VehicleGhostUtility.DrawGhostVehicleDef))]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehicleGhostUtility_DrawGhostVehicleDef
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenThing_TrueCenter2));
        codes.InsertAfter(
            CodeInstruction.LoadArgument(5),
            CodeInstruction.Call(typeof(Patch_VehicleGhostUtility_DrawGhostVehicleDef), nameof(ToTargetMapCoord)));
        return codes.Instructions();
    }

    public static Vector3 ToTargetMapCoord(Vector3 original, Thing thing)
    {
        if (TargetMapManager.HasTargetMap(thing, out var map))
        {
            return original.ToBaseMapCoord(map).WithY(original.y);
        }
        return original;
    }
}

[HarmonyPatch(typeof(VehicleGhostUtility), nameof(VehicleGhostUtility.DrawGhostOverlays))]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehicleGhostUtility_DrawGhostOverlays
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenThing_TrueCenter2));
        codes.InsertAfter(
            CodeInstruction.LoadArgument(6),
            CodeInstruction.Call(typeof(Patch_VehicleGhostUtility_DrawGhostVehicleDef), nameof(Patch_VehicleGhostUtility_DrawGhostVehicleDef.ToTargetMapCoord)));
        return codes.Instructions();
    }
}

//VehicleSkyfallerのyが上書きされてたので車上のVehicleSkyfallerはy足しときなね
[HarmonyPatch(typeof(LaunchProtocol), nameof(LaunchProtocol.Draw))]
[PatchLevel(Level.Sensitive)]
public static class Patch_LaunchProtocol_Draw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = instructions.ToList();
        var m_AltitudeFor = AccessTools.Method(typeof(Altitudes), nameof(Altitudes.AltitudeFor), [typeof(AltitudeLayer)]);
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(m_AltitudeFor)) + 1;
        var label = generator.DefineLabel();
        var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

        codes[pos].labels.Add(label);
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(LaunchProtocol), "map"),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_YOffsetFull2)
        ]);
        return codes;
    }
}

[HarmonyPatch(typeof(VehicleTurret), "ParallelPreRenderResults")]
[PatchLevel(Level.Cautious)]
public static class Patch_VehicleTurret_ParallelPreRenderResults
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var f_rotation = AccessTools.Field(typeof(TransformData), nameof(TransformData.rotation));
        var m_Rotation = AccessTools.Method(typeof(Patch_VehicleTurret_ParallelPreRenderResults), nameof(Rotation));
        return instructions.Manipulator(c => c.LoadsField(f_rotation), c =>
        {
            c.opcode = OpCodes.Call;
            c.operand = m_Rotation;
        });
    }

    private static float Rotation(in TransformData transformData)
    {
        return transformData.orientation == Rot8.West ? -transformData.rotation : transformData.rotation;
    }
}

[HarmonyPatch]
[PatchLevel(Level.Safe)]
public static class Patch_SettingsCache_TryGetValue
{
    private static bool Prepare()
    {
        return ModsConfig.OdysseyActive;
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredMethods(typeof(SettingsCache)).Where(m => m.Name == "TryGetValue").Select(m =>
        {
            if (m.IsGenericMethodDefinition)
            {
                return m.MakeGenericMethod(typeof(bool));
            }
            return m;
        });
    }

    public static void Prefix(ref VehicleDef def)
    {
        if (def.HasModExtension<VehicleMapProps_Gravship>())
        {
            def = VMF_DefOf.VMF_GravshipVehicleBase;
        }
    }
}

[HarmonyPatch("Vehicles.SectionDrawer", "RecacheVehicleFilter")]
[PatchLevel(Level.Safe)]
public static class Patch_SectionDrawer_RecacheVehicleFilter
{
    private static bool Prepare()
    {
        return ModsConfig.OdysseyActive;
    }

    public static void Postfix(List<VehicleDef> ___filteredVehicleDefs)
    {
        ___filteredVehicleDefs.RemoveAll(d => d.HasModExtension<VehicleMapProps_Gravship>());
    }
}