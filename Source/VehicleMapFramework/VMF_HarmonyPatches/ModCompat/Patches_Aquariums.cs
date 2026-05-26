using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;
using static VehicleMapFramework.ModCompat.Aquariums;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_Aquariums
{
  static Patches_Aquariums()
  {
    if (Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.Aquariums);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.Aquariums)]
[HarmonyPatch("Aquariums.ThingComp_WaterGraphic", "PostPrintOnto")]
[PatchLevel(Level.Cautious)]
public static class Patch_ThingComp_WaterGraphic_PostPrintOnto
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return Patch_ThingComp_AdditionalGraphics_PostPrintOnto.Transpiler(instructions);
  }
}

[HarmonyPatchCategory(PatchCategories.Aquariums)]
[HarmonyPatch("Aquariums.TankNet", "DrawTankOutline")]
[PatchLevel(Level.Safe)]
public static class Patch_TankNet_DrawTankOutline
{
  public static bool Prefix(List<IntVec3> ___netCells, Map ___map)
  {
    GenDrawOnVehicle.DrawFieldEdges(___netCells, ColorLibrary.LightBlue, map: ___map);
    return false;
  }
}

[HarmonyPatchCategory(PatchCategories.Aquariums)]
[HarmonyPatch("Aquariums.FishMovementBehavior", "PositionWithOffsets", MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_FishMovementBehavior_PositionWithOffsets
{
  public static void Postfix(object ___aquariumFish, ref Vector3 __result)
  {
    if (((Thing)CurrentTank(___aquariumFish)).IsOnVehicleMapOf(out var vehicle))
    {
      __result = __result.ToBaseMapCoord(vehicle);
    }
  }
}
