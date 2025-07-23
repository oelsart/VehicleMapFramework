using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_Vivi
{
    public const string Category = "VMF_Patches_Vivi";

    static Patches_Vivi()
    {
        if (ModCompat.Vivi)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_Vivi.Category)]
[HarmonyPatch]
public static class Patch_ArcanePlant_Turret_TryFindNewTarget_Delegate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes<MethodBase>(AccessTools.TypeByName("VVRace.ArcanePlant_Turret"),
            t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryFindNewTarget>")));
    }

    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(Patches_Vivi.Category)]
[HarmonyPatch("VVRace.ArcanePlant_Turret", "TryFindNewTarget")]
public static class Patch_ArcanePlant_Turret_TryFindNewTarget
{
    [PatchLevel(Level.Sensitive)]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var f_allBuildingsColonist = AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_allBuildingsColonist)) + 1;
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            CodeInstruction.Call(typeof(Patch_ArcanePlant_Turret_TryFindNewTarget), nameof(AddBuildingList))
        ]);
        return codes;
    }

    private static List<Building> AddBuildingList(List<Building> list, Building instance)
    {
        tmpList.Clear();
        tmpList.AddRange(list);
        var maps = instance.Map.BaseMapAndVehicleMaps().Except(instance.Map);
        tmpList.AddRange(maps.SelectMany(m => m.listerBuildings.allBuildingsColonist));
        return tmpList;
    }

    private static List<Building> tmpList = [];
}
