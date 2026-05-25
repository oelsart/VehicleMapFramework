using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.Test_Logics;

public readonly struct PuahDisabler : IDisposable
{
    private readonly Harmony _harmony = new ("OELS.VehicleMapFramework.HaulTest");
    
    public PuahDisabler()
    {
        if (Patch_HaulToStorageJobByRace.m_Original is not null)
            _harmony.Patch(Patch_HaulToStorageJobByRace.m_Original, Patch_HaulToStorageJobByRace.m_Patch);
    }

    public void Dispose()
    {
        if (Patch_HaulToStorageJobByRace.m_Original is not null)
            _harmony.Unpatch(Patch_HaulToStorageJobByRace.m_Original, HarmonyPatchType.All);
    }
}

internal static class Patch_HaulToStorageJobByRace
{
    public static readonly MethodInfo m_Original =
        AccessTools.Method("PickUpAndHaul.HarmonyPatches:HaulToStorageJobByRace");
    
    public static readonly MethodInfo m_Patch =
        AccessTools.Method(typeof(Patch_HaulToStorageJobByRace), nameof(Prefix));

    private static bool Prefix(Pawn p, Thing t, bool forced, out Job __result)
    {
        __result = HaulAIUtility.HaulToStorageJob(p, t, forced);
        return false;
    }
}