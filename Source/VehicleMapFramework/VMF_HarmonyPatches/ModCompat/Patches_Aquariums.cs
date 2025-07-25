using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_Aquariums
{
    public const string Category = "VMF_Patches_Aquariums";

    static Patches_Aquariums()
    {
        if (ModCompat.Aquariums)
        {
            VMF_Harmony.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_Aquariums.Category)]
[HarmonyPatch("Aquariums.ThingComp_WaterGraphic", "PostPrintOnto")]
[PatchLevel(Level.Cautious)]
public static class Patch_ThingComp_WaterGraphic_PostPrintOnto
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Patch_ThingComp_AdditionalGraphics_PostPrintOnto.Transpiler(instructions);
    }
}

[HarmonyPatchCategory("Patches_Aquariums.Category")]
[HarmonyPatch("Aquariums.TankNet", "DrawTankOutline")]
[PatchLevel(Level.Safe)]
public static class Patch_TankNet_DrawTankOutline
{
    public static bool Prefix(List<IntVec3> ___netCells, Map ___map)
    {
        GenDrawOnVehicle.DrawFieldEdges(___netCells, ColorLibrary.LightBlue, null, map: ___map);
        return false;
    }
}

[HarmonyPatchCategory("Patches_Aquariums.Category")]
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

    private static FastInvokeHandler CurrentTank = MethodInvoker.GetHandler(AccessTools.PropertyGetter("Aquariums.AquariumFish:CurrentTank"));
}
