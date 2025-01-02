using HarmonyLib;
using SmashTools;
using System.IO;
using System.Linq;
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

            var version = File.ReadAllText(Path.Combine(VehicleInteriors.Mod.Content.RootDir, "Version.txt"));
            Log.Message($"[VehicleMapFramework] {version}");
            Log.Message($"[VehicleMapFramework] {VIF_Harmony.Instance.GetPatchedMethods().Count()} patches applied.");
        }
    }

    public class VIF_Harmony
    {
        public static Harmony Instance;
    }
}