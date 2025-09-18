using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_CutPlantsBeforeBuilding
{
    public const string Category = "VMF_Patches_Patches_CutPlantsBeforeBuilding";

    static Patches_CutPlantsBeforeBuilding()
    {
        if (ModCompat.CutPlantsBeforeBuilding)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_CutPlantsBeforeBuilding.Category)]
[HarmonyPatch("CutPlantsBeforeBuilding.Util", "DesignatePlants")]
public static class Patch_Util_DesignatePlants
{
    public static void Prefix(ref Map map)
    {
        if (Command_FocusVehicleMap.FocusedVehicle != null)
        {
            map = Command_FocusVehicleMap.FocusedVehicle.CurrentLevel;
        }
    }
}
