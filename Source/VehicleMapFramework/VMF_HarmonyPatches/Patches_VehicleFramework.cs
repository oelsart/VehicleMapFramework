using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Rendering;
using SmashTools.Targeting;
using UnityEngine;
using Vehicles;
using Vehicles.Rendering;
using Vehicles.World;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Transform = SmashTools.Rendering.Transform;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(RGBMaterialPool), nameof(RGBMaterialPool.SetProperties), typeof(IMaterialCacheTarget), typeof(PatternData), typeof(Func<Rot8, Texture2D>), typeof(Func<Rot8, Texture2D>))]
[PatchLevel(Level.Mandatory)]
public static class Patch_RGBMaterialPool_SetProperties
{
    public static void Postfix(IMaterialCacheTarget target, Dictionary<IMaterialCacheTarget, Material[]> ___Cache)
    {
        if (target is GraphicOverlay graphicOverlay)
        {
            var vehiclePawn = graphicOverlay.Vehicle;
            if (vehiclePawn != null && vehiclePawn.AllComps.OfType<CompOpacityOverlay>().Any(c => c.Props.identifier == graphicOverlay.data?.identifier))
            {
                if (___Cache.TryGetValue(target, out var materials) && materials != null)
                {
                    foreach (var material in materials)
                    {
                        material?.shader = material.shader.OpacityShaderCorrespond();
                    }
                }
            }
        }
    }
}

//VehiclePawnWithMapの場合Movementフラグを持つハンドラーが存在しない場合コントロールできないようにする
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.HasEnoughOperators), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_VehiclePawn_HasEnoughOperators
{
    public static bool Prefix(VehiclePawn __instance, ref bool __result)
    {
        if (__instance is VehiclePawnWithMap)
        {
            if ((__instance.MovementPermissions & VehiclePermissions.Autonomous) > VehiclePermissions.None)
            {
                __result = true;
                return false;
            }
            var matchHandlers = __instance.handlers.Where(h => (h.role.HandlingTypes & HandlingType.Movement) > HandlingType.None).ToList();
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

//VehiclePawnWithMapの場合タレットに対応するハンドラーが存在しない場合ギズモを操作不能にする
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(CompVehicleTurrets), nameof(CompVehicleTurrets.CompGetGizmosExtra))]
[PatchLevel(Level.Safe)]
public static class Patch_CompVehicleTurrets_CompGetGizmosExtra
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos, CompVehicleTurrets __instance)
    {
        foreach (var gizmo in gizmos)
        {
            if (gizmo is Command_Turret command_Turret)
            {
                var turret = command_Turret.turret;
                if (turret is VehicleTurret_Manual &&
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

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.DisembarkPawn))]
[PatchLevel(Level.Safe)]
public static class Patch_VehiclePawn_DisembarkPawn
{
    public static bool Prefix(Pawn pawn, VehiclePawn __instance)
    {
        var handler = __instance.handlers.FirstOrDefault(h => h.thingOwner.Contains(pawn));
        if (handler?.role is VehicleRoleBuildable buildable && __instance is VehiclePawnWithMap vehicle)
        {
            var parent = buildable.upgradeComp.parent;
            var map = parent.Map ?? vehicle.VehicleMap;
            __instance.RemovePawn(pawn);
            if (!pawn.Spawned)
            {
                var cellRect = parent.OccupiedRect().ExpandedBy(1);
                var intVec = parent.Position;
                if (cellRect.EdgeCells.Where(delegate (IntVec3 c)
                {
                    if (c.InBounds(map) && c.Standable(map))
                    {
                        return !c.GetThingList(map).NotNullAndAny(t => t is Pawn);
                    }
                    return false;
                }).TryRandomElement(out var intVec2))
                {
                    intVec = intVec2;
                }
                GenSpawn.Spawn(pawn, intVec, map);
                if (!intVec.Standable(map))
                {
                    pawn.pather.TryRecoverFromUnwalkablePosition(false);
                }
                var lord = __instance.GetLord();
                if (lord != null)
                {
                    var lord2 = pawn.GetLord();
                    lord2?.Notify_PawnLost(pawn, PawnLostCondition.ForcedToJoinOtherLord);
                    lord.AddPawn(pawn);
                }
            }
            __instance.EventRegistry[VehicleEventDefOf.PawnExited].ExecuteEvents();
            return false;
        }
        return true;
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehiclePawn), nameof(VehiclePawn.FullRotation), MethodType.Getter)]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehiclePawn_FullRotation
{
    public static bool Prefix(VehiclePawn __instance, ref Rot8 __result)
    {
        return !__instance.TryGetFullRotation(ref __result);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch("Vehicles.Patch_Rendering", "DrawSelectionBracketsVehicles")]
[PatchLevel(Level.Mandatory)]
public static class Patch_Rendering_DrawSelectionBracketsVehicles
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
    {
        var codes = new CodeMatcher(instructions, generator);
        codes.MatchEndForward(CodeMatch.LoadsField(AccessTools.Field(typeof(Transform), nameof(Transform.rotation))), new CodeMatch(OpCodes.Add));
        codes.DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle);
        codes.CreateLabel(out var label);
        var l_vehicle_ind = original.GetMethodBody()?.LocalVariables.FirstIndexOf(l => l.LocalType == typeof(VehiclePawn)) ?? 0;
        if (l_vehicle_ind == -1) l_vehicle_ind = 0;
        codes.InsertAndAdvance(
            CodeInstruction.LoadLocal(l_vehicle_ind),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            CodeInstruction.LoadLocal(l_vehicle_ind),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FlipAngle));

        codes.CreateLabelWithOffsets(1, out var label2);
        codes.InsertAfter(
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Brfalse_S, label2),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Angle),
            new CodeInstruction(OpCodes.Add));

        var g_RotatedSize = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.RotatedSize));
        var m_BaseRotatedSize = AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.BaseRotatedSize));
        codes.MatchStartForward(CodeMatch.Calls(g_RotatedSize));
        codes.Opcode = OpCodes.Call;
        codes.Operand = m_BaseRotatedSize;
        return codes.Instructions();
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
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

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(GenGridVehicles), nameof(GenGridVehicles.ImpassableForVehicles))]
[PatchLevel(Level.Mandatory)]
public static class Patch_GenGridVehicles_ImpassableForVehicles
{
    public static void Postfix(ThingDef thingDef, ref bool __result)
    {
        __result &= !thingDef.thingClass.SameOrSubclassOf(typeof(Building_VehicleRamp));
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
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

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(TargetingHelper), nameof(TargetingHelper.TargetMeetsRequirements), [typeof(VehicleTurret), typeof(LocalTargetInfo), typeof(IntVec3)], [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out])]
[PatchLevel(Level.Cautious)]
public static class Patch_TargetingHelper_TargetMeetsRequirements1
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(TargetingHelper), nameof(TargetingHelper.TargetMeetsRequirements), [typeof(VehicleTurret), typeof(IntVec3), typeof(LocalTargetInfo), typeof(IntVec3)], [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out])]
[PatchLevel(Level.Cautious)]
public static class Patch_TargetingHelper_TargetMeetsRequirements2
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight1, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight1)
            .MethodReplacer(CachedMethodInfo.m_OccupiedRect, CachedMethodInfo.m_MovedOccupiedRect)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSightToEdges, CachedMethodInfo.m_GenSightOnVehicle_LineOfSightToEdges);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(TurretShotReport), nameof(TurretShotReport.HitReportFor))]
[PatchLevel(Level.Mandatory)]
public static class Patch_TurretShotReport_HitReportFor
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var g_Thing = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.Thing));
        return new CodeMatcher(instructions, generator).Reset()
            .DeclareLocal(typeof(Thing), out var targetThing)
            .DeclareLocal(typeof(Map), out var targetMap)
            .CreateLabel(out var label)
            .DefineLabel(out var label2)
            .Insert(
                CodeInstruction.LoadArgument(2, true),
                new CodeInstruction(OpCodes.Call, g_Thing),
                new CodeInstruction(OpCodes.Stloc_S, targetThing),
                new CodeInstruction(OpCodes.Ldloc_S, targetThing),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, targetThing),
                new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_Map),
                new CodeInstruction(OpCodes.Br_S, label2),
                CodeInstruction.LoadArgument(0).WithLabels(label),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_BaseMap_Thing),
                new CodeInstruction(OpCodes.Stloc_S, targetMap).WithLabels(label2))
            .MatchStartForward(
                new CodeMatch(OpCodes.Ldloc_0),
                CodeMatch.Calls(AccessTools.Method(typeof(CoverUtility), nameof(CoverUtility.CalculateCoverGiverSet))))
            .Set(OpCodes.Ldloc_S, targetMap)
            .InstructionEnumeration()
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.Roofed)),
                AccessTools.Method(typeof(VehicleMapUtility), nameof(VehicleMapUtility.RoofedAcrossMaps), [typeof(IntVec3), typeof(Map)]));
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.InRange))]
[PatchLevel(Level.Cautious)]
public static class Patch_VehicleTurret_InRange
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.FireTurret))]
[PatchLevel(Level.Cautious)]
public static class Patch_VehicleTurret_FireTurret
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.TurretRotation), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_VehicleTurret_TurretRotation
{
    public static void Postfix(ref float __result, VehiclePawn ___vehicle)
    {
        if (___vehicle.IsOnNonFocusedVehicleMapOf(out var vehicle2))
        {
            __result = Ext_Math.RotateAngle(__result, vehicle2.FullAngle);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleTurret), nameof(VehicleTurret.TurretRotationTargeted), MethodType.Setter)]
[PatchLevel(Level.Safe)]
public static class Patch_VehicleTurret_TurretRotationTargeted
{
    public static void Prefix(ref float value, VehicleTurret __instance)
    {
        if (__instance.vehicle.IsOnNonFocusedVehicleMapOf(out var vehicle2) && (__instance.TargetLocked || TurretTargeter.Turret == __instance))
        {
            value = Ext_Math.RotateAngle(value, -vehicle2.FullAngle);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
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

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.BeginTargeting))]
[PatchLevel(Level.Cautious)]
public static class Patch_TurretTargeter_BeginTargeting
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(TurretTargeter), "CurrentTargetUnderMouse")]
[PatchLevel(Level.Cautious)]
public static class Patch_TurretTargeter_CurrentTargetUnderMouse
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.m_GenUI_TargetsAtMouse, CachedMethodInfo.m_GenUIOnVehicle_TargetsAtMouse);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.TargeterUpdate))]
[PatchLevel(Level.Cautious)]
public static class Patch_TurretTargeter_TargeterUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(TurretTargeter), nameof(TurretTargeter.ProcessInputEvents))]
[PatchLevel(Level.Cautious)]
public static class Patch_TurretTargeter_ProcessInputEvents
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMapSpawned);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(TurretTargeter), "TargeterValid", MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_TurretTargeter_TargeterValid
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

//車両マップ上からLoadVehicleをしようとした時など
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch]
[PatchLevel(Level.Safe)]
public static class Patch_JobDriverLoadVehicleBase_ShouldFailJob
{
    private static readonly Dictionary<Type, Predicate<JobDriverLoadVehicleBase>> ShouldFailJob = [];

    private static bool working;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var g_Map = AccessTools.PropertyGetter(typeof(JobDriver), "Map");
        foreach (var method in typeof(JobDriverLoadVehicleBase).AllSubclasses()
                     .Select(type => AccessTools.DeclaredMethod(type, "ShouldFailJob"))
                     .Where(method => method is not null &&
                         PatchHelper.ReadMethodBodyWrapper(method)
                             .Any(i => g_Map.Equals(i.Value))))
        {
            yield return method;
        }
    }
    
    public static void Postfix(JobDriverLoadVehicleBase __instance, ref bool __result)
    {
        if (working) return;
        
        if (__result)
        {
            var map = __instance.pawn.Map;
            try
            {
                working = true;
                var type = __instance.GetType();
                if (!ShouldFailJob.TryGetValue(type, out var predicate))
                {
                    var m_ShouldFailJob = AccessTools.DeclaredMethod(type, "ShouldFailJob");
                    predicate = ShouldFailJob[type] =
                        AccessTools.MethodDelegate<Predicate<JobDriverLoadVehicleBase>>(m_ShouldFailJob);
                }
                foreach (var map2 in map.BaseMapAndVehicleMaps(false))
                {
                    __instance.pawn.VirtualMapTransfer(map2);
                    if (!predicate(__instance))
                        __result = false;
                }
            }
            finally
            {
                working = false;
                __instance.pawn.VirtualMapTransfer(map);
            }
        }
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(LaunchProtocol), nameof(LaunchProtocol.GetArrivalOptions))]
[PatchLevel(Level.Safe)]
public static class Patch_LaunchProtocol_GetArrivalOptions
{
    public static IEnumerable<ArrivalOption> Postfix(IEnumerable<ArrivalOption> values, GlobalTargetInfo target, LaunchProtocol __instance)
    {
        foreach (var arrivalOption in values)
        {
            yield return arrivalOption;
        }

        if (__instance.Vehicle is VehiclePawnWithMap)
        {
            yield break;
        }

        var mapParents = Find.World.pocketMaps.Where(p => p.Tile == target.Tile).OfType<MapParent_Vehicle>();
        foreach (var mapParent in mapParents)
        {
            var vehicle = __instance.Vehicle;
            if (mapParent.HasMap && !mapParent.EnterCooldownBlocksEntering())
            {
                yield return new ArrivalOption("LandInExistingMap".Translate(mapParent.Label),
                  continueWith: delegate (TargetData<GlobalTargetInfo> targetData)
                  {
                      Current.Game.CurrentMap = mapParent.Map;
                      CameraJumper.TryHideWorld();
                      LandingTargeter.Instance.BeginTargeting(vehicle, mapParent.Map,
                action: delegate (LocalTargetInfo landingCell, Rot4 rot)
                      {
                          if (vehicle.Spawned)
                          {
                              vehicle.CompVehicleLauncher.Launch(targetData,
                        new ArrivalAction_LandToCell(vehicle, mapParent, landingCell.Cell, rot));
                          }
                          else
                          {
                              var aerialVehicle = vehicle.GetOrMakeAerialVehicle();
                              var nodes = targetData.targets.Select(targetInfo => new FlightNode(targetInfo)).ToList();
                              aerialVehicle.OrderFlyToTiles(nodes,
                        new ArrivalAction_LandToCell(vehicle, mapParent, landingCell.Cell, rot));
                              vehicle.CompVehicleLauncher.inFlight = true;
                              CameraJumper.TryShowWorld();
                          }
                      }, allowRotating: vehicle.VehicleDef.rotatable,
                targetValidator: targetInfo => targetInfo.Cell.InBounds(mapParent.Map) &&
                  !Ext_Vehicles.IsRoofRestricted(vehicle.VehicleDef, targetInfo.Cell, mapParent.Map));
                  });
            }
        }
    }
}

//VehicleMap上を右クリックしている時は複数ポーンのVehicle乗り込みフロートメニューをオフにする
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(SelectionHelper), nameof(SelectionHelper.MultiSelectClicker))]
[PatchLevel(Level.Safe)]
public static class Patch_SelectionHelper_MultiSelectClicker
{
    public static bool Prefix(ref bool __result)
    {
        if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out _, VehicleMapFlag.None))
        {
            __result = false;
            return false;
        }
        return true;
    }
}

//ポーンがVehicleRoleBuildableに割り当てられている時はその席へのCanReachにすり替える
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(CaravanFormation), "CheckForErrors")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CaravanFormation_CheckForErrors
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var codes = instructions.ToList();

        //コンパイルごとにインデックスがころころ変わるのでここだけ多少変更に強くしてます
        var ind = original.GetMethodBody()!.LocalVariables.First(l => l.LocalType == typeof(VehiclePawn)).LocalIndex;
        var pos = codes.FindIndex(c =>
        {
            switch (ind)
            {
                case 0:
                    return c.opcode == OpCodes.Ldloc_0;
                case 1:
                    return c.opcode == OpCodes.Ldloc_1;
                case 2:
                    return c.opcode == OpCodes.Ldloc_2;
                case 3:
                    return c.opcode == OpCodes.Ldloc_3;
                default:
                {
                    var localBuilder = codes.Select(c2 => c2.operand).OfType<LocalBuilder>().First(l => l.LocalIndex == ind);
                    return c.IsLdloc(localBuilder);
                }
            }
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
        var assignedSeat = CaravanHelper.assignedSeats.GetAssignment(pawn);
        if (assignedSeat != null && assignedSeat.handler.role is VehicleRoleBuildable vehicleRoleBuildable)
        {
            return vehicleRoleBuildable.upgradeComp.parent;
        }
        return vehicle;
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(CaravanFormation), "TryFindExitSpot",
    [typeof(Map), typeof(List<Pawn>), typeof(bool), typeof(Rot4), typeof(IntVec3)],
    [ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out])]
[PatchLevel(Level.Safe)]
public static class Patch_CaravanFormation_TryFindExitSpot
{
    public static void Prefix(Map map, List<Pawn> pawns)
    {
        foreach (var pawn in pawns)
        {
            pawn.DestMap = map;
        }
    }

    public static void Finalizer(List<Pawn> pawns)
    {
        foreach (var pawn in pawns)
        {
            pawn.RemoveDestMap();
        }
    }
}

//キャラバン編成画面でVehicleRoleBuildableに割り当てられているポーンはその席へ行くようにする
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(JobDriver_Board), "MakeNewToils")]
[PatchLevel(Level.Safe)]
public static class Patch_JobDriver_Board_MakeNewToils
{
    public static IEnumerable<Toil> Postfix(IEnumerable<Toil> values)
    {
        foreach (var toil in values)
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
                        if (ToilFailConditions.DespawnedOrNull(dest, actor))
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

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
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

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleTabHelper_Passenger), nameof(VehicleTabHelper_Passenger.DrawPassengersFor))]
[PatchLevel(Level.Safe)]
public static class Patch_VehicleTabHelper_Passenger_DrawPassengersFor
{
    private const float PawnRowHeight = 50f;

    public static void Postfix(ref float curY, Rect viewRect, Vector2 scrollPos, VehiclePawn vehicle, ref Pawn moreDetailsForPawn
        , Pawn ___draggedPawn, ref IThingHolder ___transferToHolder, ref bool ___overDropSpot, ref Pawn ___hoveringOverPawn)
    {
        if (vehicle is VehiclePawnWithMap mapVehicle)
        {
            var pawns = Patch_MapPawns_AllPawnsSpawned.AllPawnsSpawned(mapVehicle.VehicleMap.mapPawns);
            var rect = new Rect(0f, curY, viewRect.width - 48f, 25f + (PawnRowHeight * pawns.Count));
            if (___draggedPawn != null && Mouse.IsOver(rect) && ___draggedPawn.Map != mapVehicle.VehicleMap)
            {
                ___transferToHolder = mapVehicle.VehicleMap;
                ___overDropSpot = true;
                Widgets.DrawHighlight(rect);
            }
            Widgets.ListSeparator(ref curY, viewRect.width, mapVehicle.LabelCap + "VMF_VehicleMap".Translate());

            foreach (var pawn in pawns)
            {
                if (VehicleTabHelper_Passenger.DoRow(curY, viewRect, scrollPos, pawn, ref moreDetailsForPawn, true))
                {
                    ___hoveringOverPawn = pawn;
                }
                curY += PawnRowHeight;
            }
        }
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleTabHelper_Passenger), nameof(VehicleTabHelper_Passenger.HandleDragEvent))]
[PatchLevel(Level.Safe)]
[HotSwap]
public static class Patch_VehicleTabHelper_Passenger_HandleDragEvent
{
    public static bool Prefix(ref Pawn ___draggedPawn, IThingHolder ___transferToHolder, Pawn ___hoveringOverPawn)
    {
        if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
        {
            if (___draggedPawn != null && ___draggedPawn.Faction == Faction.OfPlayer && ___transferToHolder != null)
            {
                if (___transferToHolder is Map map && map.IsVehicleMapOf(out var vehicle))
                {
                    var caravan = ___draggedPawn.GetVehicleCaravan();
                    if (___draggedPawn.ParentHolder is VehicleRoleHandler vehicleHandler)
                    {
                        if (!___draggedPawn.Spawned && TryFindSpawnSpot(vehicle, vehicleHandler, out var intVec, out var map2))
                        {
                            vehicle.RemovePawn(___draggedPawn);
                            GenSpawn.Spawn(___draggedPawn, intVec, map2);
                            vehicleHandler.vehicle.EventRegistry[VehicleEventDefOf.PawnExited].ExecuteEvents();
                            SoundDefOf.Click.PlayOneShotOnCamera();
                            ___draggedPawn = null;
                            return false;
                        }
                    }
                    else if (!___draggedPawn.Spawned && ___draggedPawn.IsWorldPawn() && TryFindSpawnSpot(vehicle, null, out var intVec, out var map2))
                    {
                        Find.WorldPawns.RemovePawn(___draggedPawn);
                        GenSpawn.Spawn(___draggedPawn, intVec, map2);
                        caravan?.RecacheVehicles();
                        caravan?.GetInspectTabs()?.FirstOrDefault(t => t is WITab_Vehicle_Manifest)?.OnOpen();
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        ___draggedPawn = null;
                        return false;
                    }
                    else if (___draggedPawn.IsOnVehicleMapOf(out var vehicle2) && vehicle != vehicle2 && TryFindSpawnSpot(vehicle2, null, out intVec, out map2))
                    {
                        ___draggedPawn.DeSpawn();
                        GenSpawn.Spawn(___draggedPawn, intVec, map2);
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        ___draggedPawn = null;
                        return false;
                    }
                    Messages.Message("VMF_CannotSpawn".Translate(___draggedPawn), MessageTypeDefOf.RejectInput, false);
                    ___draggedPawn = null;
                    return false;
                }

                if (___draggedPawn.IsOnVehicleMapOf(out vehicle))
                {
                    if (___transferToHolder is VehicleRoleHandler vehicleHandler)
                    {
                        if (!vehicleHandler.CanOperateRole(___draggedPawn))
                        {
                            Messages.Message("VF_HandlerNotEnoughRoom".Translate(___draggedPawn, vehicleHandler.role.label), MessageTypeDefOf.RejectInput, false);
                            ___draggedPawn = null;
                            return false;
                        }
                        if (!vehicleHandler.AreSlotsAvailable)
                        {
                            if (___hoveringOverPawn != null)
                            {
                                if (TryFindSpawnSpot(vehicle, vehicleHandler, out var intVec, out var map2))
                                {
                                    vehicle.RemovePawn(___hoveringOverPawn);
                                    GenSpawn.Spawn(___hoveringOverPawn, intVec, map2);
                                    vehicleHandler.vehicle.EventRegistry[VehicleEventDefOf.PawnExited].ExecuteEvents();
                                }
                                else
                                {
                                    Messages.Message("VMF_CannotSpawn".Translate(___hoveringOverPawn), MessageTypeDefOf.RejectInput, false);
                                    ___draggedPawn = null;
                                    return false;
                                }
                            }
                            else
                            {
                                Messages.Message("VF_HandlerNotEnoughRoom".Translate(___draggedPawn, vehicleHandler.role.label), MessageTypeDefOf.RejectInput, false);
                                ___draggedPawn = null;
                                return false;
                            }
                        }
                    }

                    var pos = ___draggedPawn.Position;
                    ___draggedPawn.DeSpawn();
                    if (___transferToHolder.GetDirectlyHeldThings().TryAddOrTransfer(___draggedPawn, false))
                    {
                        SoundDefOf.Click.PlayOneShotOnCamera();
                        if (___transferToHolder is VehicleRoleHandler vehicleHandler2)
                        {
                            vehicleHandler2.vehicle.EventRegistry[VehicleEventDefOf.PawnEntered].ExecuteEvents();
                        }
                        else if (!___draggedPawn.IsWorldPawn())
                        {
                            Find.WorldPawns.PassToWorld(___draggedPawn);
                        }
                        if (___transferToHolder is VehicleCaravan caravan)
                        {
                            caravan.RecacheVehicles();
                            caravan.GetInspectTabs()?.FirstOrDefault(t => t is WITab_Vehicle_Manifest)?.OnOpen();
                        }
                    }
                    else
                    {
                        GenSpawn.Spawn(___draggedPawn, pos, vehicle.VehicleMap);
                    }
                    ___draggedPawn = null;
                    return false;
                }
            }
        }
        return true;
    }

    private static bool TryFindSpawnSpot(VehiclePawnWithMap vehicle, VehicleRoleHandler vehicleHandler, out IntVec3 spot, out Map map)
    {
        if (vehicleHandler != null && vehicleHandler.vehicle == vehicle && vehicleHandler.role is VehicleRoleBuildable vehicleRoleBuildable)
        {
            var parent = vehicleRoleBuildable.upgradeComp.parent;
            var cellRect = parent.OccupiedRect().ExpandedBy(1);
            if (cellRect.EdgeCells.Where(delegate (IntVec3 c)
            {
                if (c.InBounds(parent.Map) && Predicate(c, parent.Map))
                {
                    return !c.GetThingList(parent.Map).NotNullAndAny(t => t is Pawn);
                }
                return false;
            }).TryRandomElement(out spot))
            {
                map = parent.Map;
                return true;
            }
            spot = IntVec3.Invalid;
            map = null;
            return false;
        }
        if (vehicle.EnterComps.Any() && vehicle.EnterComps.Select(c => c.parent.Position)
                .TryRandomElement(c => Predicate(c, vehicle.VehicleMap), out spot) ||
            vehicle.CachedMapEdgeCells.TryRandomElement(c => Predicate(c, vehicle.VehicleMap), out spot))
        {
            map = vehicle.VehicleMap;
            return true;
        }

        var cell = vehicle.CachedMapEdgeCells.RandomElement();
        if (RCellFinder.TryFindRandomCellNearWith(cell, c => Predicate(c, vehicle.VehicleMap), vehicle.VehicleMap, out spot))
        {
            map = vehicle.VehicleMap;
            return true;
        }
        spot = IntVec3.Invalid;
        map = null;
        return false;

        static bool Predicate(IntVec3 c, Map map)
        {
            return (c.Standable(map) || c.GetDoor(map) != null) && c.GetFirstPawn(map) is null;
        }
    }
}

//非MultiSelect時は既にターゲットマップある想定
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(FloatMenuOptionProvider_OrderVehicle), "VehicleCanGoto")]
[PatchLevel(Level.Safe)]
public static class Patch_FloatMenuOptionProvider_OrderVehicle_VehicleCanGoto
{
    public static bool Prefix(VehiclePawn vehicle, IntVec3 gotoLoc, ref AcceptanceReport __result)
    {
        if (vehicle.TryGetTargetMap(out var map) && vehicle.Map != map)
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

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(FloatMenuOptionProvider_OrderVehicle), "PawnGotoAction")]
[PatchLevel(Level.Safe)]
public static class Patch_FloatMenuOptionProvider_OrderVehicle_PawnGotoAction
{
    public static bool Prefix(IntVec3 clickCell, VehiclePawn vehicle, IntVec3 gotoLoc, ref Rot8 rot)
    {
        if (vehicle.TryGetTargetMap(out var map))
        {
            // 車両マップがターゲットの場合TryGetFullRotationにより回るため
            if (map.IsVehicleMapOf(out var vehicle2) && rot.IsValid)
            {
                rot = new Rot8(Rot8.FromIntClockwise(GenMath.PositiveMod(rot.AsIntClockwise - vehicle2.FullRotation.AsIntClockwise, 8)));
            }
            if (vehicle.Map != map)
            {
                if (vehicle.CanReachVehicle(gotoLoc, PathEndMode.OnCell, Danger.Deadly, TraverseMode.ByPawn, map, out var exitSpot, out var enterSpot))
                {
                    PawnGotoAction(clickCell, vehicle, map, gotoLoc, rot, exitSpot, enterSpot);
                    vehicle.RemoveTargetInfo();
                }
                return false;
            }
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
                var isOnEdge = isBaseMap && CellRect.WholeMap(baseMap).IsOnEdge(clickCell, 3);
                var exitCell = isBaseMap && baseMap.exitMapGrid.IsExitCell(clickCell);
                var vehicleCellsOverlapExit = isBaseMap && vehicle.InhabitedCellsProjected(clickCell, rot)
                 .NotNullAndAny(cell => cell.InBounds(baseMap) &&
                    baseMap.exitMapGrid.IsExitCell(cell));
                var exitMapOnArrival = exitCell || vehicleCellsOverlapExit;
                job.exitMapOnArrival = exitMapOnArrival;
                if (!exitMapOnArrival && !baseMap.IsPlayerHome && !baseMap.exitMapGrid.MapUsesExitGrid &&
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
                jobSuccess = vehicle.jobs?.TryTakeOrderedJob(job, JobTag.Misc) ?? false;
                if (jobSuccess)
                    vehicle.vehiclePather.SetEndRotation(rot);
            }
        }
        if (jobSuccess)
            FleckMaker.Static(gotoLoc, map, FleckDefOf.FeedbackGoto);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
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
        radius = Mathf.Min(radius, GenRadial.MaxRadialPatternRadius);
        VehiclePawnWithMap vehicle2 = null;
        if (vehicle.TryGetTargetMap(out var map))
        {
            if (vehicle.Map != map)
            {
                __result = CrossMapReachabilityUtility.TryFindNearestStandableCell(vehicle, cell, map, out result, radius);
                if (result.IsValid)
                {
                    return false;
                }
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
                vehicle.TargetMap = map;
                return false;
            }
        }
        return true;
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleOrientationController), "Init")]
[PatchLevel(Level.Safe)]
public static class Patch_VehicleOrientationController_Init
{
    public static void Postfix(List<VehiclePawn> vehicles, ref IntVec3 ___start, ref IntVec3 ___end)
    {
        if (vehicles.All(p => p is VehiclePawnWithMap)) return;
        if (UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle, VehicleMapFlag.None))
        {
            ___start = ___start.ToBaseMapCoord(vehicle);
            ___end = ___end.ToBaseMapCoord(vehicle);
        }
    }


}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleOrientationController), "RecomputeDestinations")]
public static class Patch_VehicleOrientationController_RecomputeDestinations
{
    [PatchLevel(Level.Safe)]
    public static void Prefix(List<VehiclePawn> ___vehicles)
    {
        if (___vehicles.Count > 1)
        {
            ___vehicles.Do(v => v.RemoveTargetInfo());
        }
    }

    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnTargetMap);
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleOrientationController), nameof(VehicleOrientationController.TargeterUpdate))]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehicleOrientationController_TargeterUpdate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var m_ToVector3ShiftedWithAltitude = AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToVector3ShiftedWithAltitude), [typeof(float)]);
        var m_ToVector3ShiftedOffsetWithAltitude = AccessTools.Method(typeof(Patch_MultiPawnGotoController_Draw), "ToVector3ShiftedOffsetWithAltitude");
        var num = 0;
        var ind = codes.Select(c => c.operand).OfType<LocalBuilder>().First(l => l.LocalType == typeof(VehiclePawn)).LocalIndex;
        foreach (var code in codes)
        {
            if (code.Calls(m_ToVector3ShiftedWithAltitude))
            {
                num++;
                if (num > 2)
                {
                    yield return CodeInstruction.LoadLocal(ind);
                    code.operand = m_ToVector3ShiftedOffsetWithAltitude;
                }
            }
            yield return code;
        }
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
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
        return thing.TryGetTargetMap(out var map) ? original.ToBaseMapCoord(map).WithY(original.y) : original;
    }
}

//ここでのTransformData.rotationは西向き時反転する前提の数値なので、車両マップキャラバン描画のユースケースでは正しく描画されるように補正する
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleTurret), "ParallelPreRenderResults")]
[PatchLevel(Level.Safe)]
public static class Patch_VehicleTurret_ParallelPreRenderResults
{
    public static void Prefix(VehicleTurret __instance, ref TransformData transformData, ref float rotation, ref float parentRotation)
    {
        //車両マップキャラバン画面で複数車両を描画する機能の実装を想定した条件
        if (__instance.vehicle is VehiclePawnWithMap && Find.CurrentMap.IsVehicleMapOf(out _) && (Rot4)transformData.orientation == Rot4.West)
        {
            var offset = transformData.rotation * 2f;
            rotation -= offset;
            parentRotation -= offset;
        }
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleCaravan), nameof(VehicleCaravan.GetGizmos))]
[PatchLevel(Level.Safe)]
public static class Patch_VehicleCaravan_GetGizmos
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, List<VehiclePawn> ___vehicles)
    {
        foreach (var gizmo in values)
        {
            VehiclePawn vehicle;
            if (gizmo is Command_Action action && action.defaultLabel == "CommandLaunchGroup".Translate() && (vehicle = ___vehicles.FirstOrDefault()) != null)
            {
                if (vehicle.CompVehicleLauncher is CompVehicleLauncherWithMap compLauncherWithmap)
                {
                    gizmo.Disabled = false;
                    if (!compLauncherWithmap.CanLaunchWithCargoCapacityWithMap(out var disableReason))
                    {
                        gizmo.Disable(disableReason);
                    }
                }
                if (!gizmo.Disabled && vehicle.CompVehicleLauncher is CompVehicleLauncherGravshipVehicle compLauncherGravship)
                {
                    if (!compLauncherGravship.CanLaunchGravship(out var disableReason, out _, out _, out _, out _))
                    {
                        gizmo.Disable(disableReason);
                    }
                }
            }
            yield return gizmo;
        }
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehicleCaravan_Notify_MemberDied_Predicate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(typeof(VehicleCaravan), t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<Notify_MemberDied>"));
        });
    }

    public static void Postfix(Pawn x, ref bool __result)
    {
        __result = __result || x is VehiclePawnWithMap vehicle && vehicle.VehicleMap.mapPawns.AnyPawnBlockingMapRemoval;
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch]
[PatchLevel(Level.Safe)]
public static class Patch_SettingsCache_TryGetValue
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools.GetDeclaredMethods(typeof(SettingsCache)).Where(m => m.Name == "TryGetValue").Select(m =>
            m.IsGenericMethodDefinition ? m.MakeGenericMethod(typeof(bool)) : m);
    }

    public static void Prefix(ref VehicleDef def)
    {
        var props = def.GetModExtension<VehicleMapProps_Unique>();
        if (props is { baseDef: not null })
        {
            def = props.baseDef;
        }
    }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch("Vehicles.SectionDrawer", "RecacheVehicleFilter")]
[PatchLevel(Level.Safe)]
public static class Patch_SectionDrawer_RecacheVehicleFilter
{
    public static void Postfix(List<VehicleDef> ___filteredVehicleDefs)
    {
        ___filteredVehicleDefs.RemoveAll(d =>
        {
            var props = d.GetModExtension<VehicleMapProps_Unique>();
            return props is { baseDef: not null };
        });
    }
}

[HarmonyPatch(typeof(RenderHelper), nameof(RenderHelper.DrawLinesBetweenTargets))]
[PatchLevel(Level.Sensitive)]
public static class Patch_RenderHelper_DrawLinesBetweenTargets
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(CachedMethodInfo.g_Thing_Position));
        codes.RemoveRange(pos, 4);
        var g_Pawn_DrawPos = AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.DrawPos));
        codes.Insert(pos, new CodeInstruction(OpCodes.Callvirt, g_Pawn_DrawPos));

        var g_CenterVector3 = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.CenterVector3));
        var m_CenterVector3VehicleOffset = AccessTools.Method(typeof(Patch_Pawn_JobTracker_DrawLinesBetweenTargets), nameof(Patch_Pawn_JobTracker_DrawLinesBetweenTargets.CenterVector3VehicleOffset));
        foreach (var code in codes)
        {
            if (code.opcode == OpCodes.Call && code.OperandIs(g_CenterVector3))
            {
                yield return CodeInstruction.LoadArgument(0);
                code.operand = m_CenterVector3VehicleOffset;
            }
            yield return code;
        }
    }
}

[HarmonyPatch(typeof(TransferableVehicleWidget), "DrawCard")]
[PatchLevel(Level.Safe)]
public static class Patch_TransferableVehicleWidget_DrawCard
{
    internal static VehiclePawnWithMap vehicle;
    
    public static void Prefix(TransferableOneWay transferable)
    {
        if (Event.current.type == EventType.Repaint)
            vehicle = transferable.AnyThing as VehiclePawnWithMap;
    }
}

[HarmonyPatch(typeof(TextureDrawer), "Draw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_TextureDrawer_Draw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.Calls(
                AccessTools.Method(typeof(UIElements), nameof(UIElements.DrawTextureWithMaterialOnGUI))))
            .InsertAfter(
                CodeInstruction.LoadLocal(3),
                CodeInstruction.Call(typeof(Patch_TextureDrawer_Draw), nameof(TryRenderVehicleMap)))
            .InstructionEnumeration();
    }
    
    public static void TryRenderVehicleMap(Rect drawRect)
    {
        ref var vehicle = ref Patch_TransferableVehicleWidget_DrawCard.vehicle;
        if (vehicle is not null)
        {
            Vector2? drawSize = null;
            Vector3? drawOffset = null;
            if (!vehicle.def.HasModExtension<VehicleMapProps_Gravship>())
            {
                drawSize = vehicle.DrawSize;
                drawOffset = VehicleMapUtility.OffsetFor(vehicle, Rot4.East);
            }
            var texture = VehicleMapUIRenderer.GetVehicleMapTexture(vehicle, Rot4.East, new Vector2Int(256, 256),
                drawSize, drawOffset);
            var rect2 = new Rect(0f, 0f, 150f, 150f)
            {
                center = drawRect.center
            };
            Widgets.DrawTextureFitted(rect2, texture, 1f);
            
            vehicle = null;
        }
    }
}