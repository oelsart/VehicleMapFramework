using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

public static class PatchHelper
{
    public static IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBodyWrapper(MethodBase method)
    {
        try
        {
            return PatchProcessor.ReadMethodBody(method);
        }
        catch(Exception ex)
        {
            VMF_Log.Warning($"Autopatching to {method.FullDescription()} failed. It may be referencing outdated signatures. The patch will simply be skipped.\n{ex}");
            return [];
        }
    }
    
    private static readonly FieldInfo f_allBuildingsColonist = AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist));

    public static IEnumerable<CodeInstruction> AddAllBuildingsColonistForThingInstance(this IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (instruction.LoadsField(f_allBuildingsColonist))
            {
                yield return CodeInstruction.LoadArgument(0);
                yield return new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_AddColonistBuildingList);
            }
        }
    }

    public static CodeMatcher AddAltitudeFor(this CodeMatcher codeMatcher, out LocalBuilder vehicle,
        float offset = 0f, CodeMatch[] matches = null, CodeInstruction[] getInstance = null)
    {
        matches ??= [CodeMatch.Calls(CachedMethodInfo.m_Altitudes_AltitudeFor)];
        getInstance ??= [CodeInstruction.LoadArgument(0)];
        codeMatcher
            .MatchStartForward(matches)
            .Advance()
            .CreateLabel(out var label)
            .DeclareLocal(typeof(VehiclePawnWithMap), out vehicle)
            .InsertAndAdvance(getInstance)
            .InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_YOffsetFull));
        if (offset != 0f)
        {
            codeMatcher
                .InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldc_R4, offset),
                    new CodeInstruction(OpCodes.Add));
        }

        return codeMatcher;
    }
}