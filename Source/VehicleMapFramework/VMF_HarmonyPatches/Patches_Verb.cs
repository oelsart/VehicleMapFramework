using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SmashTools;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatch(typeof(Verb), nameof(Verb.TryFindShootLineFromTo))]
[PatchLevel(Level.Safe)]
public static class Patch_Verb_TryFindShootLineFromTo
{
    private static bool Prepare()
    {
        return !CombatExtended;
    }

    public static bool Prefix(Verb __instance, IntVec3 root, LocalTargetInfo targ, ref ShootLine resultingLine, bool ignoreRange, ref bool __result)
    {
        if (VehiclePawnWithMapCache.AllVehiclesOn(__instance.caster.GroundMap).NullOrEmpty())
            return true;
        
        if ((__instance.caster.IsOnVehicleMapOf(out _) ||
            targ.Thing.IsOnVehicleMapOf(out _) ||
            (__instance.caster.TryGetTargetMap(out var map) && map.IsVehicleMapOf(out _)) ||
            root.IsValid && GenSight.PointsOnLineOfSight(root, targ.Cell).Any(c => c.InBounds(__instance.caster.Map) && c.TryGetVehicleMap(__instance.caster.Map, out _))))
        {
            __result = __instance.TryFindShootLineFromToOnVehicle(root, targ, out resultingLine, ignoreRange);
            return false;
        }
        return true;
    }
}

//CanHitTargetFrom内でrootとターゲットとの距離を測ってたりする時用（Jumpなど）
[HarmonyPatch(typeof(Verb), nameof(Verb.CanHitTarget))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CanHitTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Verb), nameof(Verb.DrawHighlight))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_DrawHighlight
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Verb), "DrawHighlightFieldRadiusAroundTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_DrawHighlightFieldRadiusAroundTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Verb_LaunchProjectile), "GetForcedMissTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectile_GetForcedMissTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_LaunchProjectile_GetForcedMissTarget_Delegate
{
    private static MethodInfo TargetMethod()
    {
        return typeof(Verb_LaunchProjectile).GetDeclaredMethods().First(m => m.Name.Contains("<GetForcedMissTarget>"));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LaunchProjectile_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_TryStartCastOn
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Verb_ShootBeam), "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatch(typeof(Verb_ShootBeam), "TryGetHitCell")]
[PatchLevel(Level.Safe)]
public static class Patch_Verb_ShootBeam_TryGetHitCell
{
    public static bool Prefix(IntVec3 source, IntVec3 targetCell, out IntVec3 hitCell, Thing ___caster, VerbProperties ___verbProps, out bool __result)
    {
        var intVec = GenSight.LastPointOnLineOfSight(source, targetCell, c => c.CanBeSeenOverOnVehicle(___caster.BaseMap()), true);
        if (___verbProps.beamCantHitWithinMinRange && intVec.DistanceTo(source) < ___verbProps.minRange)
        {
            hitCell = default;
            __result = false;
            return false;
        }
        hitCell = intVec.IsValid ? intVec : targetCell;
        __result = intVec.IsValid;
        return false;
    }
}

[HarmonyPatch]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_GetBeamHitNeighbourCells
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(typeof(Verb_ShootBeam), t =>
            !t.Name.Contains("<GetBeamHitNeighbourCells>") ? null : AccessTools.Method(t, "MoveNext"));
    }
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight1, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight1);
    }
}

[HarmonyPatch(typeof(Verb_ShootBeam), nameof(Verb_ShootBeam.BurstingTick))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_BurstingTick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_ShootBeam_BurstingTick_Delegate
{
    private static MethodInfo TargetMethod()
    {
        return typeof(Verb_ShootBeam).GetDeclaredMethods().First(m => m.Name.Contains("<BurstingTick>"));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_CanBeSeenOverFast, CachedMethodInfo.m_CanBeSeenOverOnVehicleFast);
    }
}

[HarmonyPatch(typeof(Verb_ShootBeam), "CalculatePath")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_CalculatePath
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(Verb_ShootBeam), "HitCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_HitCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatch(typeof(Verb_ShootBeam), "ApplyDamage")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ShootBeam_ApplyDamage
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_ShootBeam_Delegate
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return typeof(Verb_ShootBeam).FindIncludingInnerTypes(
            t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<ApplyDamage>")));
        yield return typeof(Verb_ShootBeam).FindIncludingInnerTypes(
            t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<BurstingTick>")));
        yield return typeof(Verb_ShootBeam).FindIncludingInnerTypes(
            t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryGetHitCell>")));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_CanBeSeenOverFast, CachedMethodInfo.m_CanBeSeenOverOnVehicleFast);
    }
}

[HarmonyPatch(typeof(Verb_Spray), "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Spray_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatch(typeof(Verb_ArcSpray), "PreparePath")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ArcSpray_PreparePath
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatch(typeof(Verb_ArcSprayProjectile), "HitCell")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_ArcSprayProjectile_HitCell
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(JumpUtility), nameof(JumpUtility.CanHitTargetFrom))]
[PatchLevel(Level.Sensitive)]
public static class Patch_JumpUtility_CanHitTargetFrom
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_TargetCellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight1, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight1).ToList();

        var pos = codes.FindIndex(c => c.Calls(CachedMethodInfo.m_TargetCellOnBaseMap));
        codes.Insert(pos, CodeInstruction.LoadArgument(0));
        return codes;
    }
}

[HarmonyPatch(typeof(JumpUtility), nameof(JumpUtility.OrderJump))]
[PatchLevel(Level.Cautious)]
public static class Patch_JumpUtility_OrderJump
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_JumpUtility_OrderJump_Delegate
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(JumpUtility), t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<OrderJump>")));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatch(typeof(JumpUtility), nameof(JumpUtility.DoJump))]
public static class Patch_JumpUtility_DoJump
{
    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }

    [PatchLevel(Level.Safe)]
    public static void Finalizer(Pawn pawn, bool __result)
    {
        if (!__result) return;
        pawn.RemoveTargetInfo();
    }
}

[HarmonyPatch(typeof(JobDriver_CastJump), nameof(JobDriver_CastJump.TryMakePreToilReservations))]
[PatchLevel(Level.Cautious)]
public static class Patch_JobDriver_CastJump_TryMakePreToilReservations
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}

[HarmonyPatch(typeof(PawnFlyer), nameof(PawnFlyer.SpawnSetup))]
[PatchLevel(Level.Safe)]
public static class Patch_PawnFlyer_SpawnSetup
{
    public static void Prefix(Map map, Vector3 ___startVec, IntVec3 ___destCell, ref float ___flightDistance)
    {
        ___flightDistance = ___destCell.ToBaseMapCoord(map).DistanceTo(___startVec.ToIntVec3());
    }
}

[HarmonyPatch(typeof(Verb_Jump), nameof(Verb_Jump.DrawHighlight))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_Jump_DrawHighlight
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        instructions = instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);

        var m_CenterVector3 = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.CenterVector3));
        var m_CenterVector3Offset = AccessTools.Method(typeof(Patch_Verb_Jump_DrawHighlight), nameof(CenterVector3Offset));
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(m_CenterVector3))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return new CodeInstruction(OpCodes.Call, m_CenterVector3Offset);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static Vector3 CenterVector3Offset(ref LocalTargetInfo target, Verb verb)
    {
        var caster = verb.caster;

        var thing = target.Thing;
        Map map;
        if (thing != null)
        {
            if (thing.Spawned)
            {
                return thing.DrawPos;
            }
            if (thing.SpawnedOrAnyParentSpawned)
            {
                return caster.TryGetTargetMap(out map) ? thing.PositionHeld.ToVector3Shifted().ToBaseMapCoord(map) : thing.PositionHeld.ToVector3Shifted();
            }
            return caster.TryGetTargetMap(out map) ? thing.Position.ToVector3Shifted().ToBaseMapCoord(map) : thing.Position.ToVector3Shifted();
        }

        var cell = target.Cell;
        if (!cell.IsValid) return default;
        return caster.TryGetTargetMap(out map) ? cell.ToVector3Shifted().ToBaseMapCoord(map) : cell.ToVector3Shifted();
    }
}

[HarmonyPatch(typeof(Verb_CastAbilityJump), nameof(Verb_CastAbilityJump.DrawHighlight))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityJump_DrawHighlight
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_Verb_Jump_DrawHighlight.Transpiler(instructions);
}

[HarmonyPatch(typeof(Verb_Jump), nameof(Verb_Jump.OnGUI))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Jump_OnGUI
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}

[HarmonyPatch(typeof(Verb_CastAbilityJump), nameof(Verb_CastAbilityJump.OnGUI))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityJump_OnGUI
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_Verb_Jump_OnGUI.Transpiler(instructions);
}

[HarmonyPatch(typeof(Verb_Jump), nameof(Verb_Jump.ValidateTarget))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_Jump_ValidateTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_TargetMapOrThingMap);
    }
}

[HarmonyPatch(typeof(Verb_CastAbilityJump), nameof(Verb_CastAbilityJump.ValidateTarget))]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_CastAbilityJump_ValidateTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_Verb_Jump_ValidateTarget.Transpiler(instructions);
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_Jump_DrawHighlight_Delegate
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return typeof(Verb_Jump).GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<DrawHighlight>"));
        yield return typeof(Verb_CastAbilityJump).GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<DrawHighlight>"));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight1, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight1);
    }
}