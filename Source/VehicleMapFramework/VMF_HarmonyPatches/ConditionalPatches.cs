using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using SmashTools;
#if DEV
using SmashTools.Burst;
#endif
using Vehicles;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class ConditionalPatches
{
    static ConditionalPatches()
    {
        // This class is just a placeholder for conditional patches.
    }

    internal static void DebugError(string methodName)
    {
        VMF_Log.DebugError($"The method {methodName} targeted for patching was not found. This should mean the removal of the stubs targeted for patching.");
    }
}

// 引数を変更するPRを出したので、変更を吸収するよう備えておく
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehicleGhostUtility_DrawGhostOverlays
{
    private static MethodBase TargetMethod()
    {
        var type = typeof(VehicleGhostUtility);
        const string name = nameof(VehicleGhostUtility.DrawGhostOverlays);
        List<Type> args =
        [
            typeof(IntVec3), typeof(Rot8), typeof(VehicleDef), typeof(Graphic), typeof(Color), typeof(AltitudeLayer),
            typeof(Thing)
        ];
        var method = AccessTools.Method(type, name, args.ToArray());
        if (method is null)
        {
            args.AddRange([typeof(Rot8?), typeof(float)]);
            method = AccessTools.Method(type, name, args.ToArray());
        }
        return method;
    }
    
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

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch]
public static class Patch_VehiclePath_DrawPath
{
    private static MethodBase TargetMethod()
    {
#if DEV
        return AccessTools.Method(typeof(Ext_Path), nameof(VehiclePath.DrawPath));
#endif
        return AccessTools.Method(typeof(VehiclePath), nameof(VehiclePath.DrawPath));
    }
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) => Patch_PawnPath_DrawPath.Transpiler(instructions, generator);
}