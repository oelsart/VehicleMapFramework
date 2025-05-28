using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using static VehicleInteriors.ModCompat;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [HarmonyPatch(typeof(Verb_LaunchProjectile), "GetForcedMissTarget")]
    public static class Patch_Verb_LaunchProjectile_GetForcedMissTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch]
    public static class Patch_Verb_LaunchProjectile_GetForcedMissTarget_Delegate
    {
        private static MethodInfo TargetMethod()
        {
            return typeof(Verb_LaunchProjectile).GetMethods(AccessTools.all).First(m => m.Name.Contains("<GetForcedMissTarget>"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Verb_LaunchProjectile), "TryCastShot")]
    public static class Patch_Verb_LaunchProjectile_TryCastShot
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_Verb_TryFindShootLineFromTo, MethodInfoCache.m_TryFindShootLineFromToOnVehicle)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool))]
    public static class Patch_Verb_TryStartCastOn
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            instructions = instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
            if (!CombatExtended.Active)
            {
                instructions = instructions.MethodReplacer(MethodInfoCache.m_Verb_TryFindShootLineFromTo, MethodInfoCache.m_TryFindShootLineFromToOnVehicle);
            }
            return instructions;
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.CanHitTarget))]
    public static class Patch_Verb_CanHitTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.CanHitTargetFrom))]
    public static class Patch_Verb_CanHitTargetFrom
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_Verb_TryFindShootLineFromTo, MethodInfoCache.m_TryFindShootLineFromToOnVehicle);
        }
    }

    [HarmonyPatch(typeof(Verb_ShootBeam), "TryCastShot")]
    public static class Patch_Verb_ShootBeam_TryCastShot
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_Verb_TryFindShootLineFromTo, MethodInfoCache.m_TryFindShootLineFromToOnVehicle)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Verb_ShootBeam), nameof(Verb_ShootBeam.DrawHighlight))]
    public static class Patch_Verb_ShootBeam_DrawHighlight
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotation_Thing)
                .MethodReplacer(MethodInfoCache.g_Rot4_AsQuat, MethodInfoCache.m_Rot8_AsQuatRef)
                .MethodReplacer(MethodInfoCache.m_Verb_TryFindShootLineFromTo, MethodInfoCache.m_TryFindShootLineFromToOnVehicle)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Verb_ShootBeam), "TryGetHitCell")]
    public static class Patch_Verb_ShootBeam_TryGetHitCell
    {
        public static bool Prefix(IntVec3 source, IntVec3 targetCell, out IntVec3 hitCell, Thing ___caster, VerbProperties ___verbProps, out bool __result)
        {
            IntVec3 intVec = GenSight.LastPointOnLineOfSight(source, targetCell, (IntVec3 c) => c.CanBeSeenOverOnVehicle(___caster.BaseMap()), true);
            if (___verbProps.beamCantHitWithinMinRange && intVec.DistanceTo(source) < ___verbProps.minRange)
            {
                hitCell = default;
                __result = false;
                return false;
            }
            hitCell = (intVec.IsValid ? intVec : targetCell);
            __result = intVec.IsValid;
            return false;
        }
    }

    [HarmonyPatch(typeof(Verb_ShootBeam), "GetBeamHitNeighbourCells")]
    public static class Patch_Verb_ShootBeam_GetBeamHitNeighbourCells
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSight1, MethodInfoCache.m_GenSightOnVehicle_LineOfSight1);
        }
    }

    [HarmonyPatch(typeof(Verb_ShootBeam), nameof(Verb_ShootBeam.BurstingTick))]
    public static class Patch_Verb_ShootBeam_BurstingTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch]
    public static class Patch_Verb_ShootBeam_BurstingTick_Delegate
    {
        private static MethodInfo TargetMethod()
        {
            return typeof(Verb_ShootBeam).GetMethods(AccessTools.all).First(m => m.Name.Contains("<BurstingTick>"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_CanBeSeenOverFast, MethodInfoCache.m_CanBeSeenOverOnVehicle);
        }
    }

    [HarmonyPatch(typeof(Verb_ShootBeam), "CalculatePath")]
    public static class Patch_Verb_ShootBeam_CalculatePath
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Verb_ShootBeam), "HitCell")]
    public static class Patch_Verb_ShootBeam_HitCell
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatch(typeof(Verb_ShootBeam), "ApplyDamage")]
    public static class Patch_Verb_ShootBeam_ApplyDamage
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch]
    public static class Patch_Verb_ShootBeam_ApplyDamage_Delegate
    {
        private static MethodInfo TargetMethod()
        {
            return typeof(Verb_ShootBeam).GetMethods(AccessTools.all).First(m => m.Name.Contains("<ApplyDamage>"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_CanBeSeenOverFast, MethodInfoCache.m_CanBeSeenOverOnVehicle);
        }
    }

    [HarmonyPatch(typeof(Verb_Spray), "TryCastShot")]
    public static class Patch_Verb_Spray_TryCastShot
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.m_Verb_TryFindShootLineFromTo, MethodInfoCache.m_TryFindShootLineFromToOnVehicle)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Verb_ArcSpray), "PreparePath")]
    public static class Patch_Verb_ArcSpray_PreparePath
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_LocalTargetInfo_Cell, MethodInfoCache.m_CellOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(Verb_ArcSprayProjectile), "HitCell")]
    public static class Patch_Verb_ArcSprayProjectile_HitCell
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSight2, MethodInfoCache.m_GenSightOnVehicle_LineOfSight2)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(JumpUtility), nameof(JumpUtility.CanHitTargetFrom))]
    public static class Patch_JumpUtility_CanHitTargetFrom
    {
        public static void Prefix(ref LocalTargetInfo targ)
        {
            if (GenUIOnVehicle.TargetMap != null)
            {
                targ = targ.Cell.ToBaseMapCoord(GenUIOnVehicle.TargetMap);
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSight1, MethodInfoCache.m_GenSightOnVehicle_LineOfSight1);
        }
    }

    [HarmonyPatch(typeof(JumpUtility), nameof(JumpUtility.OrderJump))]
    public static class Patch_JumpUtility_OrderJump
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, m_TargetMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }

        public static Map TargetMap(Pawn _)
        {
            return GenUIOnVehicle.TargetMap ?? Find.CurrentMap;
        }

        public static MethodInfo m_TargetMap = AccessTools.Method(typeof(Patch_JumpUtility_OrderJump), nameof(TargetMap));
    }

    [HarmonyPatch]
    public static class Patch_JumpUtility_OrderJump_Delegate
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(JumpUtility), t => t.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("<OrderJump>")));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap);
        }
    }

    [HarmonyPatch(typeof(JumpUtility), nameof(JumpUtility.DoJump))]
    public static class Patch_JumpUtility_DoJump
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, Patch_JumpUtility_OrderJump.m_TargetMap);
        }
    }

    [HarmonyPatch(typeof(JobDriver_CastJump), nameof(JobDriver_CastJump.TryMakePreToilReservations))]
    public static class Patch_JobDriver_CastJump_TryMakePreToilReservations
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, Patch_JumpUtility_OrderJump.m_TargetMap);
        }
    }

    [HarmonyPatch(typeof(PawnFlyer), nameof(PawnFlyer.SpawnSetup))]
    public static class Patch_PawnFlyer_SpawnSetup
    {
        public static void Prefix(Map map, Vector3 ___startVec, IntVec3 ___destCell, ref float ___flightDistance)
        {
            ___flightDistance = ___destCell.ToBaseMapCoord(map).DistanceTo(___startVec.ToIntVec3());
        }
    }

    [HarmonyPatch(typeof(Verb_Jump), nameof(Verb_Jump.DrawHighlight))]
    public static class Patch_Verb_Jump_DrawHighlight
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_CenterVector3 = AccessTools.PropertyGetter(typeof(LocalTargetInfo), nameof(LocalTargetInfo.CenterVector3));
            var m_CenterVector3Offset = AccessTools.Method(typeof(Patch_Verb_Jump_DrawHighlight), nameof(CenterVector3Offset));
            return instructions.MethodReplacer(m_CenterVector3, m_CenterVector3Offset)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, Patch_JumpUtility_OrderJump.m_TargetMap);
        }

        public static Vector3 CenterVector3Offset(ref LocalTargetInfo target)
        {
            var thing = target.Thing;
            if (thing != null)
            {
                if (thing.Spawned)
                {
                    return thing.DrawPos;
                }
                if (thing.SpawnedOrAnyParentSpawned)
                {
                    if (GenUIOnVehicle.TargetMap != null)
                    {
                        return thing.PositionHeld.ToVector3Shifted().ToBaseMapCoord(GenUIOnVehicle.TargetMap);
                    }
                    else
                    {
                        return thing.PositionHeld.ToVector3Shifted();
                    }
                }
                if (GenUIOnVehicle.TargetMap != null)
                {
                    return thing.Position.ToVector3Shifted().ToBaseMapCoord(GenUIOnVehicle.TargetMap);
                }
                else
                {
                    return thing.Position.ToVector3Shifted();
                }
            }
            else
            {
                var cell = target.Cell;
                if (cell.IsValid)
                {
                    if (GenUIOnVehicle.TargetMap != null)
                    {
                        return cell.ToVector3Shifted().ToBaseMapCoord(GenUIOnVehicle.TargetMap);
                    }
                    else
                    {
                        return cell.ToVector3Shifted();
                    }
                }
                return default(Vector3);
            }
        }
    }

    [HarmonyPatch(typeof(Verb_CastAbilityJump), nameof(Verb_CastAbilityJump.DrawHighlight))]
    public static class Patch_Verb_CastAbilityJump_DrawHighlight
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_Verb_Jump_DrawHighlight.Transpiler(instructions);
    }

    [HarmonyPatch(typeof(Verb_Jump), nameof(Verb_Jump.OnGUI))]
    public static class Patch_Verb_Jump_OnGUI
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, Patch_JumpUtility_OrderJump.m_TargetMap);
        }
    }

    [HarmonyPatch(typeof(Verb_CastAbilityJump), nameof(Verb_CastAbilityJump.OnGUI))]
    public static class Patch_Verb_CastAbilityJump_OnGUI
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_Verb_Jump_OnGUI.Transpiler(instructions);
    }

    [HarmonyPatch(typeof(Verb_Jump), nameof(Verb_Jump.ValidateTarget))]
    public static class Patch_Verb_Jump_ValidateTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, Patch_JumpUtility_OrderJump.m_TargetMap);
        }
    }

    [HarmonyPatch(typeof(Verb_CastAbilityJump), nameof(Verb_CastAbilityJump.ValidateTarget))]
    public static class Patch_Verb_CastAbilityJump_ValidateTarget
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => Patch_Verb_Jump_ValidateTarget.Transpiler(instructions);
    }

    [HarmonyPatch]
    public static class Patch_Verb_Jump_DrawHighlight_Delegate
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return typeof(Verb_Jump).GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("<DrawHighlight>"));
            yield return typeof(Verb_CastAbilityJump).GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("<DrawHighlight>"));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSight1, MethodInfoCache.m_GenSightOnVehicle_LineOfSight1);
        }
    }
}
