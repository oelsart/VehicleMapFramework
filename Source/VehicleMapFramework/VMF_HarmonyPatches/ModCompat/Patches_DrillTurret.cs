using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_DrillTurret
{
    static Patches_DrillTurret()
    {
        if (ModCompat.DrillTurret)
        {
            VMF_Harmony.PatchCategory(PatchCategories.DrillTurret);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.DrillTurret)]
[HarmonyPatch("DrillTurret.Building_DrillTurret", "lookForNewTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DrillTurret_lookForNewTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.DrillTurret)]
[HarmonyPatch("DrillTurret.Building_DrillTurret", "isValidTargetAt")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DrillTurret_isValidTargetAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2);
    }
}

[HarmonyPatchCategory(PatchCategories.DrillTurret)]
[HarmonyPatch("DrillTurret.Building_DrillTurret", "isValidTargetAtForGizmo")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DrillTurret_isValidTargetAtForGizmo
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.m_GenSight_LineOfSight2, CachedMethodInfo.m_GenSightOnVehicle_LineOfSight2);
    }
}

[HarmonyPatchCategory(PatchCategories.DrillTurret)]
[HarmonyPatch("DrillTurret.Building_DrillTurret", "drillRock")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DrillTurret_drillRock
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.DrillTurret)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_DrillTurret_selectTarget
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes<MethodBase>(AccessTools.TypeByName("DrillTurret.Building_DrillTurret"), t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<selectTarget>")));
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.DrillTurret)]
[HarmonyPatch("DrillTurret.Building_DrillTurret", "setForcedTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_DrillTurret_setForcedTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}

[HarmonyPatchCategory(PatchCategories.DrillTurret)]
[HarmonyPatch]
public static class Patch_Building_DrillTurret_DrawAt
{
    private static Vector3? overridePos;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method("DrillTurret.Building_DrillTurret:DrawAt");
        yield return AccessTools.Method("DrillTurret.Building_DrillTurret:computeDrawingParameters");
    }

    [PatchLevel(Level.Safe)]
    public static void Prefix(Thing __instance)
    {
        if (__instance.IsOnNonFocusedVehicleMapOf(out var vehicle))
        {
            overridePos = __instance.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.Projectile.AltitudeFor()).ToBaseMapCoord(vehicle);
            return;
        }
        overridePos = null;
    }

    [PatchLevel(Level.Cautious)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var m_ToVector3Override = AccessTools.Method(typeof(Patch_Building_DrillTurret_DrawAt), nameof(ToVector3Override));
        return instructions.MethodReplacer(CachedMethodInfo.m_IntVec3_ToVector3ShiftedWithAltitude, m_ToVector3Override);
    }

    public static Vector3 ToVector3Override(ref IntVec3 c, float AddedAltitude)
    {
        return overridePos ?? c.ToVector3ShiftedWithAltitude(AddedAltitude);
    }
}
