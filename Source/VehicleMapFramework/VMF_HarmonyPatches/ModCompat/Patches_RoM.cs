using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Jobs;
using Verse;
using Verse.AI;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_RoM
{
    public const string Category = "VMF_Patches_RoM";

    static Patches_RoM()
    {
        if (ModCompat.RimWorldOfMagic)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_RoM.Category)]
[HarmonyPatch("TorannMagic.TorannMagicMod+FloatMenuMakerMap_Patch", "Postfix")]
[PatchLevel(Level.Cautious)]
public static class Patch_FloatMenuMakerMap_Patch_Postfix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        codes.MatchStartForward(CodeMatch.IsLdarg(2), CodeMatch.Calls(CachedMethodInfo.g_Thing_Map));
        codes.Repeat(c =>
        {
            c.Opcode = OpCodes.Ldarg_0;
        });
        return codes.Instructions();
    }
}

[HarmonyPatchCategory(Patches_RoM.Category)]
[HarmonyPatch("TorannMagic.TorannMagicMod+TryFindShootLineFromTo_Base_Patch", "Prefix")]
[PatchLevel(Level.Sensitive)]
public static class Patch_TryFindShootLineFromTo_Base_Patch_Prefix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        var m_CanReachImmediate = AccessTools.Method(typeof(ReachabilityImmediate), nameof(ReachabilityImmediate.CanReachImmediate), [typeof(IntVec3), typeof(LocalTargetInfo), typeof(Map), typeof(PathEndMode), typeof(Pawn)]);
        codes.MatchStartForward(CodeMatch.Calls(m_CanReachImmediate));
        codes.MatchStartBackwards(CodeMatch.IsLdarg(1));
        codes.InsertAfter(
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(Verb), nameof(Verb.caster)),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToThingMapCoord));
        return codes.Instructions().MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_RoM.Category)]
[HarmonyPatch("TorannMagic.TorannMagicMod+TryFindCastPosition_Base_Patch", "Prefix")]
[PatchLevel(Level.Cautious)]
public static class Patch_TryFindCastPosition_Base_Patch_Prefix
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_RoM.Category)]
[HarmonyPatch("AbilityUser.AbilityUserMod", "ConfirmStillValid")]
[PatchLevel(Level.Cautious)]
public static class Patch_AbilityUserMod_ConfirmStillValid
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_RoM.Category)]
[HarmonyPatch("TorannMagic.AutoCast.Phase", "Evaluate")]
[PatchLevel(Level.Cautious)]
public static class Patch_Phase_Evaluate
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(Patches_RoM.Category)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_UseAbility
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var t_Verb_UseAbility = GenTypes.GetTypeInAnyAssembly("AbilityUser.Verb_UseAbility", "AbilityUser");
        var nestedTypes = AccessTools.InnerTypes(t_Verb_UseAbility);
        foreach (var type in t_Verb_UseAbility.AllSubclasses().Concat(t_Verb_UseAbility).Concat(nestedTypes))
        {
            foreach (var method in type.GetDeclaredMethods())
            {
                if (VMF_Harmony.ReadMethodBodyWrapper(method).Any(i =>
                {
                    return CachedMethodInfo.g_Thing_Position.Equals(i.Value) ||
                    CachedMethodInfo.g_Thing_PositionHeld.Equals(i.Value) ||
                    CachedMethodInfo.m_GetThingList.Equals(i.Value) ||
                    CachedMethodInfo.g_LocalTargetInfo_Cell.Equals(i.Value) ||
                    CachedMethodInfo.g_Thing_Map.Equals(i.Value) ||
                    CachedMethodInfo.g_Thing_MapHeld.Equals(i.Value) ||
                    CachedMethodInfo.m_OccupiedRect.Equals(i.Value) ||
                    CachedMethodInfo.m_BreadthFirstTraverse.Equals(i.Value);
                }))
                {
                    yield return method;
                }
            }
        }
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_PositionHeld, CachedMethodInfo.m_PositionHeldOnBaseMap)
            .MethodReplacer(CachedMethodInfo.m_OccupiedRect, CachedMethodInfo.m_MovedOccupiedRect)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap)
            .MethodReplacer(CachedMethodInfo.m_BreadthFirstTraverse, CachedMethodInfo.m_BreadthFirstTraverseAcrossMaps)
            .MethodReplacer(CachedMethodInfo.m_GetThingList, CachedMethodInfo.m_GetThingListAcrossMaps);
    }
}

[HarmonyPatchCategory(Patches_RoM.Category)]
[HarmonyPatch("AbilityUser.Verb_UseAbility", "UpdateTargets")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_UseAbility_UpdateTargets
{
    private static List<Thing> tmpList = [];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new CodeMatcher(instructions);
        var g_AllThings = AccessTools.PropertyGetter(typeof(ListerThings), nameof(ListerThings.AllThings));
        codes.MatchStartForward(CodeMatch.Calls(g_AllThings));
        codes.Repeat(c =>
        {
            c.InsertAfterAndAdvance(
                CodeInstruction.LoadArgument(0),
                CodeInstruction.Call(typeof(Patch_Verb_UseAbility_UpdateTargets), nameof(AddThingList)));
        });
        return codes.Instructions();
    }

    private static List<Thing> AddThingList(List<Thing> list, Verb verb)
    {
        var vehicles = VehiclePawnWithMapCache.AllVehiclesOn(verb.caster.BaseMap());
        if (vehicles.Count == 0) return list;

        tmpList.Clear();
        tmpList.AddRange(list);
        tmpList.AddRange(vehicles.SelectMany(v => v.VehicleMap.listerThings.AllThings));
        return tmpList;
    }
}

[HarmonyPatchCategory(Patches_RoM.Category)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_TMJobDriver_CastAbilityVerb_MakeNewToils
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(GenTypes.GetTypeInAnyAssembly("TorannMagic.TMJobDriver_CastAbilityVerb", "TorannMagic"), t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m => m.Name == "MoveNext");
        });
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var g_TargetLocA = AccessTools.PropertyGetter(typeof(JobDriver), "TargetLocA");
        var m_TargetLocAOnBaseMap = AccessTools.Method(typeof(Patch_TMJobDriver_CastAbilityVerb_MakeNewToils), nameof(TargetLocAOnBaseMap));
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(g_TargetLocA, m_TargetLocAOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }

    private static IntVec3 TargetLocAOnBaseMap(JobDriver instance)
    {
        return instance.job.targetA.CellOnBaseMap();
    }
}