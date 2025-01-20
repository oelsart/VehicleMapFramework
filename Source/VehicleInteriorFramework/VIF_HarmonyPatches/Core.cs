using HarmonyLib;
using SmashTools;
using System.Linq;
using System.Reflection;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [LoadedEarly]
    [StaticConstructorOnModInit]
    public static class EarlyPatchCore
    {
        static EarlyPatchCore()
        {
            VMF_Harmony.Instance.PatchCategory("VehicleInteriors.EarlyPatches");
        }
    }

    [StaticConstructorOnStartup]
    public static class Core
    {
        static Core()
        {
            VMF_Harmony.Instance.PatchAllUncategorized(Assembly.GetExecutingAssembly());

            Log.Message($"[VehicleMapFramework] {VehicleInteriors.mod.Content.ModMetaData.ModVersion}");
            Log.Message($"[VehicleMapFramework] {VMF_Harmony.Instance.GetPatchedMethods().Count()} patches applied.");
        }
    }

    public class VMF_Harmony
    {
        public static Harmony Instance = new Harmony("com.harmony.oels.vehicleinteriorframework");
    }
}