using HarmonyLib;
using RimWorld;
using System;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_CallTradeShips
{
    public const string Category = "VMF_Patches_CallTradeShips";

    static Patches_CallTradeShips()
    {
        if (ModCompat.CallTradeShips)
        {
            VMF_Harmony.Instance.PatchCategory(Category);
        }
    }
}

[HarmonyPatchCategory(Patches_CallTradeShips.Category)]
[HarmonyPatch(typeof(Job), nameof(Job.Clone))]
[PatchLevel(Level.Safe)]
public static class Patch_Job_Clone
{
    public static bool Prefix(Job __instance, ref Job __result)
    {
        if (__instance.GetType() == t_Job_CallTradeShip)
        {
            _ = (Job)t_Job_CallTradeShip.CreateInstance();
            __result = __instance.Clone();
            TraderKindDef(__result) = TraderKindDef(__instance);
            TraderKind(__result) = TraderKind(__instance);
            return false;
        }
        return true;
    }

    private static readonly Type t_Job_CallTradeShip = AccessTools.TypeByName("CallTradeShips.Job_CallTradeShip");

    private static readonly AccessTools.FieldRef<Job, TraderKindDef> TraderKindDef = AccessTools.FieldRefAccess<TraderKindDef>(t_Job_CallTradeShip, "TraderKindDef");

    private static readonly AccessTools.FieldRef<Job, int> TraderKind = AccessTools.FieldRefAccess<int>(t_Job_CallTradeShip, "TraderKind");
}
