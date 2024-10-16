using HarmonyLib;
using System.Reflection;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    public static class Core
    {
        static Core()
        {
            VIF_Harmony.Instance = new Harmony("com.harmony.rimworld.vehicleinteriorframework");
            VIF_Harmony.Instance.PatchAllUncategorized(Assembly.GetExecutingAssembly());
        }
    }

    public class VIF_Harmony
    {
        public static Harmony Instance;
    }
}