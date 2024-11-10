using HarmonyLib;
using SmashTools;
using System.Reflection;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [LoadedEarly]
    [StaticConstructorOnModInit]
    public static class EarlyPatchCore
    {
        static EarlyPatchCore()
        {
            VIF_Harmony.Instance = new Harmony("com.harmony.oels.vehicleinteriorframework");
            VIF_Harmony.Instance.PatchCategory("VehicleInteriors.EarlyPatches");
        }
    }

    [StaticConstructorOnStartup]
    public static class Core
    {
        static Core()
        {
            VIF_Harmony.Instance.PatchAllUncategorized(Assembly.GetExecutingAssembly());
        }
    }

    public class VIF_Harmony
    {
        public static Harmony Instance;
    }
}