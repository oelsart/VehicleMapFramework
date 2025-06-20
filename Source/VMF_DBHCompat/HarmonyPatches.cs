using DubsBadHygiene;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using static VehicleInteriors.MethodInfoCache;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_VEF
    {
        static Patches_VEF()
        {
            VMF_Harmony.PatchCategory("VMF_Patches_DBH");
            if (DubsBadHygiene.Settings.LiteMode)
            {
                DefDatabase<ThingDef>.GetNamed("VMF_PipeConnector").comps.RemoveAll(c => c is CompProperties_PipeConnectorDBH);
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DBH")]
    [HarmonyPatch(typeof(CompPipe), nameof(CompPipe.Props), MethodType.Getter)]
    public static class Patch_CompResource_Props
    {
        public static void Postfix(CompPipe __instance, ref CompProperties_Pipe __result)
        {
            if (__instance is CompPipeConnectorDBH connector)
            {
                dummy.mode = connector.mode;
                dummy.stuffed = connector.Props.stuffed;
                dummy.vertPipe = connector.Props.vertPipe;
                __result = dummy;
            }
        }

        private static readonly CompProperties_Pipe dummy = new CompProperties_Pipe();
    }

    [HarmonyPatchCategory("VMF_Patches_DBH")]
    [HarmonyPatch(typeof(PlaceWorker_SewageArea), nameof(PlaceWorker_SewageArea.DrawGhost))]
    public static class Patch_PlaceWorker_SewageArea_DrawGhost
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stfld);
            var label = generator.DefineLabel();
            var label2 = generator.DefineLabel();
            var f_visibleMap = codes[pos].operand;

            codes[pos].labels.Add(label2);
            codes.InsertRange(pos - 1, new[]
            {
                CodeInstruction.LoadArgument(5),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Callvirt, CachedMethodInfo.g_Thing_Map),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Br_S, label2),
                new CodeInstruction(OpCodes.Pop).WithLabels(label)
            });
            pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_GenDraw_DrawFieldEdges));
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadLocal(0),
                new CodeInstruction(OpCodes.Ldfld, f_visibleMap)
            });
            return codes.MethodReplacer(CachedMethodInfo.g_Find_CurrentMap, CachedMethodInfo.g_VehicleMapUtility_CurrentMap)
                .MethodReplacer(CachedMethodInfo.m_GenDraw_DrawFieldEdges, CachedMethodInfo.m_GenDrawOnVehicle_DrawFieldEdges);
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DBH")]
    [HarmonyPatch]
    public static class Patch_PlaceWorker_SewageArea_DrawGhost_Predicate
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(PlaceWorker_SewageArea),
                t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<DrawGhost>")));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var m_GetFirstBuilding = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetFirstBuilding));
            var pos = codes.FindIndex(c => c.Calls(m_GetFirstBuilding));
            var label = generator.DefineLabel();
            var map = generator.DeclareLocal(typeof(Map));

            codes.InsertRange(pos, new[]
            {
                new CodeInstruction(OpCodes.Stloc_S, map),
                new CodeInstruction(OpCodes.Ldloc_S, map),
                CodeInstruction.Call(typeof(GenGrid), nameof(GenGrid.InBounds), new[] { typeof(IntVec3), typeof(Map) }),
                new CodeInstruction(OpCodes.Brtrue_S, label),
                new CodeInstruction(OpCodes.Ldc_I4_0),
                new CodeInstruction(OpCodes.Ret),
                CodeInstruction.LoadArgument(1).WithLabels(label),
                new CodeInstruction(OpCodes.Ldloc_S, map)
            });
            return codes;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DBH")]
    [HarmonyPatch(typeof(MapComponent_Hygiene), nameof(MapComponent_Hygiene.CanHaveSewage))]
    public static class Patch_MapComponent_Hygiene_CanHaveSewage
    {
        public static bool Prefix(IntVec3 c, Map ___map, ref bool __result)
        {
            if (!c.InBounds(___map))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_DBH")]
    [HarmonyPatch(typeof(MapComponent_Hygiene), nameof(MapComponent_Hygiene.MapComponentUpdate))]
    public static class Patch_MapComponent_Hygiene_MapComponentUpdate
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(CachedMethodInfo.m_CellRect_ClipInsideMap, CachedMethodInfo.m_ClipInsideVehicleMap);
        }
    }
}
