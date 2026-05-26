using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_CutPlantsBeforeBuilding
{
  static Patches_CutPlantsBeforeBuilding()
  {
    if (CutPlantsBeforeBuilding)
    {
      VMF_Harmony.PatchCategory(PatchCategories.CutPlantsBeforeBuilding);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.CutPlantsBeforeBuilding)]
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
