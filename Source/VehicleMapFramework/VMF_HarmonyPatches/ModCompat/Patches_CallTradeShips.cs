using HarmonyLib;
using Verse.AI;
using static VehicleMapFramework.ModCompat.CallTradeShips;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_CallTradeShips
{
  static Patches_CallTradeShips()
  {
    if (Active)
    {
      VMF_Harmony.Instance.PatchCategory(PatchCategories.CallTradeShips);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.CallTradeShips)]
[HarmonyPatch(typeof(Job), nameof(Job.Clone))]
[PatchLevel(Level.Safe)]
public static class Patch_Job_Clone
{
  public static bool Prefix(Job __instance, ref Job __result)
  {
    if (__instance.GetType() == Job_CallTradeShip)
    {
      _ = (Job)Job_CallTradeShip.CreateInstance();
      __result = __instance.Clone();
      TraderKindDef(__result) = TraderKindDef(__instance);
      TraderKind(__result) = TraderKind(__instance);
      return false;
    }
    return true;
  }
}
