using HarmonyLib;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    public class VMF_Harmony
    {
        public static Harmony Instance = new Harmony("com.harmony.oels.vehicleinteriorframework");
    }

    [LoadedEarly]
    [StaticConstructorOnModInit]
    public static class EarlyPatchCore
    {
        static EarlyPatchCore()
        {
            VMF_Harmony.Instance.PatchCategory("VehicleInteriors.EarlyPatches");
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class StaticConstructorOnStartupPriority : Attribute
    {
        public StaticConstructorOnStartupPriority(int priority)
        {
            this.priority = priority;
        }

        public int priority = -1;
    }

    [StaticConstructorOnStartup]
    public static class StaticConstructorOnStartupPriorityUtility
    {
        static StaticConstructorOnStartupPriorityUtility()
        {
            var types = GenTypes.AllTypesWithAttribute<StaticConstructorOnStartupPriority>();
            types.SortByDescending(t => t.GetCustomAttribute<StaticConstructorOnStartupPriority>().priority);
            foreach (Type type in types)
            {
                try
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat(new object[]
                    {
                        "Error in static constructor of ",
                        type,
                        ": ",
                        ex
                    }));
                }
            }
        }
    }

    [StaticConstructorOnStartupPriority(Priority.Normal)]
    public static class Core
    {
        static Core()
        {
            VMF_Harmony.Instance.PatchAllUncategorized(Assembly.GetExecutingAssembly());
        }
    }

    [StaticConstructorOnStartupPriority(Priority.Last)]
    public static class HarmonyPatchReport
    {
        static HarmonyPatchReport()
        {
            Log.Message($"[VehicleMapFramework] {VehicleInteriors.mod.Content.ModMetaData.ModVersion} rev{Assembly.GetExecutingAssembly().GetName().Version.Revision}");
            Log.Message($"[VehicleMapFramework] {VMF_Harmony.Instance.GetPatchedMethods().Count()} patches applied.");
        }
    }
}