using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    public class VMF_Harmony
    {
        public static Harmony Instance = new Harmony("com.harmony.oels.vehiclemapframework");

        public static void PatchCategory(string category)
        {
            var method = new StackTrace().GetFrame(1).GetMethod();
            var assembly = method.ReflectedType.Assembly;
            AccessTools.GetTypesFromAssembly(assembly)
                .Where(t => t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatch)) &&
                t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatchCategory) && a.ConstructorArguments.Any(c => c.Value.Equals(category))))
                .Select(Instance.CreateClassProcessor)
                .Do(p => p.Patch());
        }
    }

    [LoadedEarly]
    [StaticConstructorOnModInit]
    public static class EarlyPatchCore
    {
        static EarlyPatchCore()
        {
            VMF_Harmony.PatchCategory("VehicleInteriors.EarlyPatches");
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class StaticConstructorOnStartupPriority : Attribute
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
            AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly())
                .Where(t => t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatch)) && t.CustomAttributes.All(a => a.AttributeType != typeof(HarmonyPatchCategory)))
                .Select(VMF_Harmony.Instance.CreateClassProcessor)
                .DoIf(p => p.Category.NullOrEmpty(), p => p.Patch());
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