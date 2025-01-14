using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Vehicles;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [LoadedEarly]
    [StaticConstructorOnModInit]
    public static class EarlyPatchCore
    {
        static EarlyPatchCore()
        {
            VIF_Harmony.Instance.PatchCategory("VehicleInteriors.EarlyPatches");
        }
    }

    [StaticConstructorOnStartup]
    public static class Core
    {
        static Core()
        {
            VIF_Harmony.Instance.PatchAllUncategorized(Assembly.GetExecutingAssembly());

            Log.Message($"[VehicleMapFramework] {VehicleInteriors.mod.Content.ModMetaData.ModVersion}");
            Log.Message($"[VehicleMapFramework] {VIF_Harmony.Instance.GetPatchedMethods().Count()} patches applied.");
        }
    }

    public class VIF_Harmony
    {
        public static Harmony Instance = new Harmony("com.harmony.oels.vehicleinteriorframework");
    }
}