using CombatExtended;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_CE_AsAboveSoBelowCompat
{
  static Patches_CE_AsAboveSoBelowCompat()
  {
    if (AsAboveSoBelow.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.AsAboveSoBelowCECompat);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelowCECompat)]
[HarmonyPatch("AsAboveSoBelow.Patch_CE_ShiftTarget_ABCrossBand", "Prefix")]
[PatchLevel(Level.Safe)]
public static class Patch_Patch_CE_ShiftTarget_ABCrossBand_Prefix
{
  public static bool Prefix(Verb __0) => !__0.caster.IsOnVehicleMap && !__0.CurrentTarget.Thing.IsOnVehicleMap;
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelowCECompat)]
[HarmonyPatch("AsAboveSoBelow.Patch_CE_ShiftVecReport_ABCrossBand", "Prefix")]
[PatchLevel(Level.Safe)]
public static class Patch_Patch_CE_ShiftVecReport_ABCrossBand_Prefix
{
  public static bool Prefix(Verb __0) => !__0.caster.IsOnVehicleMap && !__0.CurrentTarget.Thing.IsOnVehicleMap;
}

[HarmonyPatchCategory(PatchCategories.AsAboveSoBelowCECompat)]
[HarmonyPatch("AsAboveSoBelow.Patch_CE_ProjectileLaunch_ABCrossBand", "Prefix")]
[PatchLevel(Level.Safe)]
public static class Patch_Patch_CE_ProjectileLaunch_ABCrossBand_Prefix
{
  public static bool Prefix(Thing __0, Thing __1) =>
    __0 is not ProjectileCE { intendedTargetThing.IsOnVehicleMap: true } && !__1.IsOnVehicleMap;
}