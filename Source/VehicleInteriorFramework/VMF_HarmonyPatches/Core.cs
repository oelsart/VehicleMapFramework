using HarmonyLib;
using SmashTools;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches;

public class VMF_Harmony
{
    public static Harmony Instance = new("OELS.VehicleMapFramework");

    public static void PatchCategory(string category)
    {
        var method = new StackTrace().GetFrame(1).GetMethod();
        var assembly = method.ReflectedType.Assembly;
        GenTypes.AllTypes.Where(t => t.Assembly == assembly)
            .Where(t => t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatch)) &&
            t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatchCategory) && a.ConstructorArguments.Any(c => c.Value.Equals(category))))
            .Select(Instance.CreateClassProcessor)
            .Do(p =>
            {
                try
                {
                    p.Patch();
                }
                catch (Exception ex)
                {
                    VMF_Log.Error($"Error while apply patching: {ex}");
                }
            });
    }
}

[StaticConstructorOnModInit]
public static class EarlyPatchCore
{
    static EarlyPatchCore()
    {
        VMF_Harmony.PatchCategory("VehicleInteriors.EarlyPatches");
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StaticConstructorOnStartupPriority(int priority) : Attribute
{
    public int priority = priority;
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
                Log.Error($"Error in static constructor of {type}: {ex}");
            }
        }
    }
}

[StaticConstructorOnStartupPriority(Priority.Normal)]
public static class Core
{
    static Core()
    {
        var assembly = Assembly.GetExecutingAssembly();
        GenTypes.AllTypes.Where(t => t.Assembly == assembly)
            .Where(t => t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatch)) && t.CustomAttributes.All(a => a.AttributeType != typeof(HarmonyPatchCategory)))
            .Select(VMF_Harmony.Instance.CreateClassProcessor)
            .DoIf(p => p.Category.NullOrEmpty(), p =>
            {
                try
                {
                    p.Patch();
                }
                catch (Exception ex)
                {
                    VMF_Log.Error($"Error while apply patching: {ex}");
                }
            });
    }
}

[StaticConstructorOnStartupPriority(Priority.Last)]
public static class HarmonyPatchReport
{
    static HarmonyPatchReport()
    {
        VMF_Log.Message($"{VehicleInteriors.mod.Content.ModMetaData.ModVersion} rev{Assembly.GetExecutingAssembly().GetName().Version.Revision}");
        VMF_Log.Message($"{VMF_Harmony.Instance.GetPatchedMethods().Count()} patches applied.");
        //MethodInfoCache.CachedMethodInfo = null;
    }
}