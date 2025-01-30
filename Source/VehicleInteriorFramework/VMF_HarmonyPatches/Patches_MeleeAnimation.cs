using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Permissions;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VMF_HarmonyPatches.AM
{
    [StaticConstructorOnStartup]
    public static class Patches_MeleeAnimation
    {
        static Patches_MeleeAnimation()
        {
            if (ModsConfig.IsActive("co.uk.epicguru.meleeanimation"))
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_MeleeAnimation");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class Patch_Pawn_JobTracker_StartJob
    {
        public static void Prefix(Pawn ___pawn, ref Job newJob)
        {
            if (newJob.def == DefDatabase<JobDef>.GetNamedSilentFail("AM_WalkToExecution") && newJob.targetA.HasThing && newJob.targetA.Thing.MapHeld != ___pawn.Map &&
                ___pawn.CanReach(newJob.targetA, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, newJob.targetA.Thing.MapHeld, out var exitSpot, out var enterSpot))
            {
                newJob = JobAcrossMapsUtility.GotoDestMapJob(___pawn, exitSpot, enterSpot, newJob);
            }
        }
    }
   
    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.Controller.ActionController", "GetGrappleReport")]
    public static class Patch_ActionController_GetGrappleReport
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSightToThing, MethodInfoCache.m_GenSightOnVehicle_LineOfSightToThing).ToList();

            //GrapplerとTargetのマップ比較のとこだけBaseMapに変換する
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map));
            pos = codes.FindIndex(pos + 1, c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Map));
            codes[pos].opcode = OpCodes.Callvirt;
            codes[pos].operand = MethodInfoCache.m_BaseMap_Thing;

            pos = codes.FindLastIndex(pos, c => c.opcode == OpCodes.Ldarg_1);
            codes.Insert(pos, new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Map));

            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.Grappling.GrappleFlyer", "SpawnSetup")]
    public static class Patch_GrappleFlyer_SpawnSetup
    {
        public static void Postfix(PawnFlyer __instance, Pawn ___Grappler, IntVec3 ___destCell, ref int ___ticksFlightTime)
        {
            if (___Grappler != null)
            {
                float num = Mathf.Max(__instance.FlyingPawn.PositionOnAnotherThingMap(___Grappler).DistanceTo(___destCell), 1f) / Mathf.Max(__instance.FlyingPawn.Position.DistanceTo(___destCell), 1f);
                ___ticksFlightTime = (int)(___ticksFlightTime * num);
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.Grappling.JobDriver_GrapplePawn", "TickPreEnsnare")]
    public static class Patch_JobDriver_GrapplePawn_TickPreEnsnare
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.m_GenSight_LineOfSightToThing, MethodInfoCache.m_GenSightOnVehicle_LineOfSightToThing);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.Controller.ActionController", "CheckCell")]
    public static class Patch_ActionController_CheckCell
    {
        public static bool Prefix(ref IntVec3 cell, Map map, ref bool __result)
        {
            if (map.IsVehicleMapOf(out var vehicle))
            {
                cell = cell.ToVehicleMapCoord(vehicle);
                if (!cell.InBounds(map))
                {
                    __result = true;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch]
    public static class Patch_ActionController_UpdateClosestCells
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.TypeByName("AM.Controller.ActionController").GetMethods(AccessTools.all).Where(m => m.Name == "UpdateClosestCells");
        }

        //req.Target.Position -> req.Target.PositionOnAnotherThingMap(req.Grappler)
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Position));
            pos = codes.FindIndex(pos + 1, c => c.opcode == OpCodes.Callvirt && c.OperandIs(MethodInfoCache.g_Thing_Position));
            codes[pos].opcode = OpCodes.Call;
            codes[pos].operand = MethodInfoCache.m_PositionOnAnotherThingMap;
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(1),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field("AM.Controller.Requests.GrappleAttemptRequest:Grappler"))
            });

            return codes;
        }
    }

    //Find.CurrentMap != this.Map -> Find.CurrentMap != this.Map.BaseMap()
    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.AnimRenderer", "Draw")]
    public static class Patch_AnimRenderer_Draw
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var f_AnimRenderer_Map = AccessTools.Field("AM.AnimRenderer:Map");
            var f_RootTransform = AccessTools.Field("AM.AnimRenderer:RootTransform");
            return instructions.Manipulator(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_AnimRenderer_Map), c =>
            {
                c.opcode = OpCodes.Call;
                c.operand = AccessTools.Method(typeof(Patch_AnimRenderer_Draw), nameof(Patch_AnimRenderer_Draw.BaseMap));
            }).Manipulator(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_RootTransform), c =>
            {
                c.opcode = OpCodes.Call;
                c.operand = AccessTools.Method(typeof(Patch_AnimRenderer_Draw), nameof(Patch_AnimRenderer_Draw.RootTransformOffset));
            });
        }

        public static Map BaseMap(object instance)
        {
            return f_Map(instance).BaseMap();
        }

        public static Matrix4x4 RootTransformOffset(object instance)
        {
            var root = f_RootTransform(instance);
            if (f_Map(instance).IsNonFocusedVehicleMapOf(out var vehicle) && f_cellData(f_Def(instance)).Count > 0)
            {
                var rootPos = root.Position();
                root.SetColumn(3, rootPos.ToBaseMapCoord(vehicle).WithY(rootPos.y));
                return root;
            }
            return root;
        }

        public static AccessTools.FieldRef<object, Map> f_Map = AccessTools.FieldRefAccess<Map>("AM.AnimRenderer:Map");

        public static AccessTools.FieldRef<object, Matrix4x4> f_RootTransform = AccessTools.FieldRefAccess<Matrix4x4>("AM.AnimRenderer:RootTransform");

        public static AccessTools.FieldRef<object, Def> f_Def = AccessTools.FieldRefAccess<Def>("AM.AnimRenderer:Def");

        public static AccessTools.FieldRef<Def, IReadOnlyList<object>> f_cellData = AccessTools.FieldRefAccess<IReadOnlyList<object>>("AM.AnimDef:cellData");
    }

    //[HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    //[HarmonyPatch("AM.AnimRenderer", "OnStart")]
    //public static class Patch_AnimRenderer_OnStart
    //{
    //    public static void Postfix(ref Matrix4x4 ___RootTransform, Map ___Map)
    //    {
    //        if (___Map.IsNonFocusedVehicleMapOf(out var vehicle))
    //        {
    //            ___RootTransform.SetColumn(3, ___RootTransform.Position().ToBaseMapCoord(vehicle));
    //        }
    //    }
    //}

    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.AnimRenderer", "DrawPawns")]
    public static class Patch_AnimRenderer_DrawPawns
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(m_GetWorldPosition, m_GetWorldPositionOffset);
        }

        public static MethodInfo m_GetWorldPosition = AccessTools.Method("AnimPartSnapshot:GetWorldPosition");

        public static MethodInfo m_GetWorldPositionOffset = AccessTools.Method(typeof(Patch_AnimRenderer_DrawPawns), nameof(Patch_AnimRenderer_DrawPawns.GetWorldPositionOffset));

        [HarmonyPatch("AnimPartSnapshot", "GetWorldPosition")]
        [HarmonyReversePatch]
        private static Vector3 GetWorldPositionOriginal(ref object instance, Vector3 vector) => throw new NotImplementedException();

        public static Vector3 GetWorldPositionOffset(ref object instance, Vector3 vector)
        {
            var result = GetWorldPositionOriginal(ref instance, vector);
            if (Patch_AnimRenderer_Draw.f_Map(instance).IsNonFocusedVehicleMapOf(out var vehicle) && Patch_AnimRenderer_Draw.f_cellData(Patch_AnimRenderer_Draw.f_Def(instance)).Count > 0)
            {
                return result.ToBaseMapCoord(vehicle).WithY(result.y);
            }
            return result;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.Sweep.PartWithSweep", "Draw")]
    public static class Patch_PartWithSweep_Draw
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var f_RootTransform = AccessTools.Field("AM.AnimRenderer:RootTransform");
            return instructions.Manipulator(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_RootTransform), c =>
            {
                c.opcode = OpCodes.Call;
                c.operand = AccessTools.Method(typeof(Patch_AnimRenderer_Draw), nameof(Patch_AnimRenderer_Draw.RootTransformOffset));
            });
        }
    }

    //カリング範囲に入るようにRootPositionにオフセットをかける
    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.AnimRenderer", "DrawSingle")]
    public static class Patch_AnimRenderer_DrawSingle
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var t_AnimRenderer = AccessTools.TypeByName("AM.AnimRenderer");
            var g_RootPosition = AccessTools.PropertyGetter(t_AnimRenderer, "RootPosition");
            var f_RootPosition = MethodInvoker.GetHandler(g_RootPosition);
            Vector3 result = default;
            f_RootPositionOffset = instance => result = (Vector3)f_RootPosition(instance);
            f_RootPositionOffset += instance =>
            {
                if (Patch_AnimRenderer_Draw.f_Map(instance).IsNonFocusedVehicleMapOf(out var vehicle) && Patch_AnimRenderer_Draw.f_cellData(Patch_AnimRenderer_Draw.f_Def(instance)).Count > 0)
                {
                    return result.ToBaseMapCoord(vehicle);
                }
                return result;
            };
            var m_RootPositionOffset = AccessTools.Method(typeof(Patch_AnimRenderer_DrawSingle), nameof(Patch_AnimRenderer_DrawSingle.RootPositionOffset));
            return instructions.MethodReplacer(g_RootPosition, m_RootPositionOffset);
        }

        public static Func<object, Vector3> f_RootPositionOffset;

        public static Vector3 RootPositionOffset(object instance) => f_RootPositionOffset(instance);
    }

    //実際の描画位置のオフセット
    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.Patches.Patch_PawnRenderer_RenderPawnAt", "MakeDrawArgs")]
    public static class Patch_Patch_PawnRenderer_RenderPawnAt_MakeDrawArgs
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(Patch_AnimRenderer_DrawPawns.m_GetWorldPosition, Patch_AnimRenderer_DrawPawns.m_GetWorldPositionOffset);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.Events.Workers.MoteWorker", "Run")]
    public static class Patch_MoteWorker_Run
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(Patch_AnimRenderer_DrawPawns.m_GetWorldPosition, Patch_AnimRenderer_DrawPawns.m_GetWorldPositionOffset);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.Events.Workers.TextMoteWorker", "Run")]
    public static class Patch_TextMoteWorker_Run
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(Patch_AnimRenderer_DrawPawns.m_GetWorldPosition, Patch_AnimRenderer_DrawPawns.m_GetWorldPositionOffset);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AnimPartSnapshot", "GetWorldDirection")]
    public static class Patch_AnimPartSnapshot_GetWorldDirection
    {
        public static void Postfix(object ___Renderer, ref Rot4 __result)
        {
            if (Patch_AnimRenderer_Draw.f_Map(___Renderer).IsNonFocusedVehicleMapOf(out var vehicle))
            {
                __result.AsInt += vehicle.Rotation.AsInt;
            }
        }
    }

    //Jobをすり替えたらエラーを出す処理をしていたので回避する。一応GotoDestMapJobのnextJobはちゃんとチェックするよ
    [HarmonyPatchCategory("VMF_Patches_MeleeAnimation")]
    [HarmonyPatch("AM.UI.DraftedFloatMenuOptionsUI", "ExecutionEnabledOnClick")]
    public static class Patch_DraftedFloatMenuOptionsUI_ExecutionEnabledOnClick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldstr && ((string)c.operand).StartsWith("CRITICAL ERROR: Failed to force interrupt"));
            var label = generator.DefineLabel();

            var ldarg1 = CodeInstruction.LoadArgument(1);
            codes[pos].MoveLabelsTo(ldarg1);
            codes.InsertRange(pos, new[]
            {
                ldarg1,
                CodeInstruction.Call(typeof(JobAcrossMapsUtility), nameof(JobAcrossMapsUtility.NextJobOfGotoDestmapJob)),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Dup),
                CodeInstruction.LoadField(typeof(Job), nameof(Job.def)),
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field("AM.AM_DefOf:AM_WalkToExecution")),
                new CodeInstruction(OpCodes.Ceq),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Pop),
                new CodeInstruction(OpCodes.Ret),
                new CodeInstruction(OpCodes.Pop).WithLabels(label)
            });

            return codes;
        }
    }
}