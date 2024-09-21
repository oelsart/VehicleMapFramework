using HarmonyLib;
using System.Reflection;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    class Core
    {
        static Core()
        {
            var harmony = new Harmony("com.harmony.rimworld.vehicleinteriorframework");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
