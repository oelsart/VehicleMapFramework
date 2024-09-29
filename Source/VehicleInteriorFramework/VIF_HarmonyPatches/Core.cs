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
            Core.harmonyInstance = new Harmony("com.harmony.rimworld.vehicleinteriorframework");
            Core.harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static Harmony harmonyInstance;
    }
}
