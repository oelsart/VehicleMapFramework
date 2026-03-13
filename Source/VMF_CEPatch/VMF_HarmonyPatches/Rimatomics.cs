using System.Collections.Generic;
using HarmonyLib;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_CE_RimatomicsCompat
{
    static Patches_CE_RimatomicsCompat()
    {
        if (ModCompat.Rimatomics.Active)
        {
            VMF_Harmony.PatchCategory(PatchCategories.CombatExtendedRimatomicsCompat);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedRimatomicsCompat)]
[HarmonyPatch("CombatExtended.Compatibility.Rimatomics", "getShields")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Rimatomics_getShields
{
    private static readonly List<Building> tmpList = [];
    
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(CodeMatch.LoadsField(
                AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist))))
            .InsertAfter(
                CodeInstruction.LoadArgument(0),
                CodeInstruction.Call(typeof(Patch_Rimatomics_getShields), nameof(AddBuildingList)))
            .InstructionEnumeration();
    }

    private static List<Building> AddBuildingList(List<Building> list, Map map)
    {
        var hashSet = map.BaseMapAndVehicleMaps(false);
        if (hashSet.NullOrEmpty()) return list;
        tmpList.Clear();
        tmpList.AddRange(list);
        foreach (var map2 in hashSet)
            tmpList.AddRange(map2.listerBuildings.allBuildingsColonist);
        return tmpList;
    }
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedRimatomicsCompat)]
[HarmonyPatch("CombatExtended.Compatibility.Rimatomics", "CheckForCollisionBetweenCallback")]
[PatchLevel(Level.Cautious)]
public static class Patch_Rimatomics_CheckForCollisionBetweenCallback
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }   
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedRimatomicsCompat)]
[HarmonyPatch("CombatExtended.Compatibility.Rimatomics", "ImpactSomethingCallback")]
[PatchLevel(Level.Cautious)]
public static class Patch_Rimatomics_ImpactSomethingCallback
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing)
            .MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }   
}

[HarmonyPatchCategory(PatchCategories.CombatExtendedRimatomicsCompat)]
[HarmonyPatch("CombatExtended.Compatibility.Rimatomics", "ShieldZonesCallback")]
[PatchLevel(Level.Cautious)]
public static class Patch_Rimatomics_ShieldZonesCallback
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }   
}