using HarmonyLib;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Targeting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Vehicles;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

public enum Level
{
    Mandatory,
    Sensitive,
    Cautious,
    Safe,
    All
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class PatchLevelAttribute(Level level) : Attribute
{
    public Level level = level;
}

public class VMF_Harmony
{
    internal static Harmony Instance = new("OELS.VehicleMapFramework");

    internal static List<string> Categories = [];

    internal static List<Assembly> Assemblies = [];

    internal static List<Type> AllTypesInMod = [];

    public static Level CurrentPatchLevel { get; private set; } = VehicleMapFramework.settings.dynamicPatchEnabled ? VehicleMapFramework.settings.dynamicPatchLevel : Level.All;

    private static Level PrevPatchLevel { get; set; } = Level.Mandatory;

    private readonly static AccessTools.FieldRef<PatchClassProcessor, object> patchMethodsRef = AccessTools.FieldRefAccess<PatchClassProcessor, object>("patchMethods");

    private readonly static AccessTools.FieldRef<object, HarmonyMethod> infoRef = AccessTools.FieldRefAccess<HarmonyMethod>("HarmonyLib.AttributePatch:info");

    private readonly static MethodInfo m_RemoveAll = AccessTools.Method(typeof(List<>).MakeGenericType(GenTypes.GetTypeInAnyAssembly("HarmonyLib.AttributePatch", "HarmonyLib")), nameof(List<>.RemoveAll));

    internal static bool OutOfRange(Level level)
    {
        if (level.CompareTo(PrevPatchLevel) * level.CompareTo(CurrentPatchLevel) > 0) return true;
        var max = PrevPatchLevel.CompareTo(CurrentPatchLevel) > 0 ? PrevPatchLevel : CurrentPatchLevel;
        return level == max;
    }

    internal static PatchClassProcessor AdjustPatchLevel(PatchClassProcessor patchClassProcessor)
    {
        Predicate<object> predicate = static attributePatch =>
        {
            var method = infoRef(attributePatch).method;
            var attribute = method.GetCustomAttribute<PatchLevelAttribute>();
            var level = attribute?.level ?? method.DeclaringType.GetCustomAttribute<PatchLevelAttribute>()?.level ?? Level.Mandatory;
            return OutOfRange(level);
        };

        m_RemoveAll.Invoke(patchMethodsRef(patchClassProcessor), [predicate]);
        return patchClassProcessor;
    }

    internal static bool CheckClassPatchLevel(Type type)
    {
        var attribute = type.GetCustomAttribute<PatchLevelAttribute>();
        if (attribute is null) return true;
        return !OutOfRange(attribute.level);
    }

    public static void DynamicPatchAll(Level patchLevel)
    {
        if (CurrentPatchLevel < patchLevel)
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                PrevPatchLevel = CurrentPatchLevel;
                CurrentPatchLevel = patchLevel;
                var patchCountBefore = Instance.GetPatchedMethods().Count();
                PatchAllUncategorized();
                foreach (var category in Categories)
                {
                    PatchCategory(category);
                }
                var patchCountAfter = Instance.GetPatchedMethods().Count();
                VMF_Log.Message($"Dynamic patches applied: {patchCountAfter - patchCountBefore} Total: {patchCountAfter}");
            }, "VMF_ApplyingDynamicPatches", false, null, false);
            return;
        }
        else if (VehicleMapFramework.settings.dynamicUnpatchEnabled && CurrentPatchLevel != patchLevel)
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                PrevPatchLevel = CurrentPatchLevel;
                CurrentPatchLevel = patchLevel;
                var patchCountBefore = Instance.GetPatchedMethods().Count();
                UnpatchAllUncategorized();
                foreach (var category in Categories)
                {
                    UnpatchCategory(category);
                }
                var patchCountAfter = Instance.GetPatchedMethods().Count();
                VMF_Log.Message($"Dynamic patches unapplied: {patchCountBefore - patchCountAfter} Total: {patchCountAfter}");
            }, "VMF_UnpatchingDynamicPatches", false, null, false);
            return;
        }
    }

    private static List<Type> TypesInAssembly(Assembly assembly)
    {
        if (!Assemblies.Contains(assembly))
        {
            Assemblies.Add(assembly);
            AllTypesInMod.AddRange(GenTypes.AllTypes.Where(t => t.Assembly == assembly));
        }
        return AllTypesInMod;
    }

    internal static void PatchCategory(string category)
    {
        var method = new StackTrace().GetFrame(1).GetMethod();
        var assembly = method.ReflectedType.Assembly;
        if (!Categories.Contains(category))
        {
            Categories.Add(category);
        }
        TypesInAssembly(assembly)
            .Where(t => t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatch)) &&
            t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatchCategory) && a.ConstructorArguments.Any(c => c.Value.Equals(category))))
            .Where(CheckClassPatchLevel)
            .Select(Instance.CreateClassProcessor)
            .Do(patchClass =>
            {
                try
                {
                    AdjustPatchLevel(patchClass);
                    patchClass.Patch();
                }
                catch (Exception ex)
                {
                    VMF_Log.Error($"Error while apply patching.\n{ex}");
                }
            });
    }

    internal static void UnpatchCategory(string category)
    {
        var method = new StackTrace().GetFrame(1).GetMethod();
        var assembly = method.ReflectedType.Assembly;
        TypesInAssembly(assembly)
            .Where(t => t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatch)) &&
            t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatchCategory) && a.ConstructorArguments.Any(c => c.Value.Equals(category))))
            .Where(CheckClassPatchLevel)
            .Select(Instance.CreateClassProcessor)
            .Do(patchClass =>
            {
                try
                {
                    AdjustPatchLevel(patchClass);
                    patchClass.Unpatch();
                }
                catch (Exception ex)
                {
                    VMF_Log.Error($"Error while apply unpatching.\n{ex}");
                }
            });
    }

    internal static void PatchAllUncategorized()
    {
        var method = new StackTrace().GetFrame(1).GetMethod();
        var assembly = method.ReflectedType.Assembly;
        TypesInAssembly(assembly)
            .Where(t => t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatch)) && t.CustomAttributes.All(a => a.AttributeType != typeof(HarmonyPatchCategory)))
            .Where(CheckClassPatchLevel)
            .Select(Instance.CreateClassProcessor)
            .DoIf(p => p.Category.NullOrEmpty(), patchClass =>
            {
                try
                {
                    AdjustPatchLevel(patchClass);
                    patchClass.Patch();
                }
                catch (Exception ex)
                {
                    VMF_Log.Error($"Error while apply patching\n{ex}");
                }
            });
    }

    internal static void UnpatchAllUncategorized()
    {
        var method = new StackTrace().GetFrame(1).GetMethod();
        var assembly = method.ReflectedType.Assembly;
        TypesInAssembly(assembly)
            .Where(t => t.CustomAttributes.Any(a => a.AttributeType == typeof(HarmonyPatch)) && t.CustomAttributes.All(a => a.AttributeType != typeof(HarmonyPatchCategory)))
            .Where(CheckClassPatchLevel)
            .Select(Instance.CreateClassProcessor)
            .DoIf(p => p.Category.NullOrEmpty(), patchClass =>
            {
                try
                {
                    AdjustPatchLevel(patchClass);
                    patchClass.Unpatch();
                }
                catch (Exception ex)
                {
                    VMF_Log.Error($"Error while apply unpatching\n{ex}");
                }
            });
    }

    public static IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBodyWrapper(MethodBase method)
    {
        try
        {
            return PatchProcessor.ReadMethodBody(method);
        }
        catch(Exception ex)
        {
            VMF_Log.Error($"Error within ReadMethodBody(). {method.FullDescription()} is likely referencing an old signature.\n{ex}");
            return [];
        }
    }
}

public static class EarlyPatchCore
{
    public static void EarlyPatch()
    {
        VMF_Harmony.PatchCategory(Category);
    }

    public const string Category = "VehicleMapFramework.EarlyPatches";
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StaticConstructorOnStartupPriorityAttribute(int priority) : Attribute
{
    public int priority = priority;
}

[StaticConstructorOnStartup]
public static class StaticConstructorOnStartupPriorityUtility
{
    static StaticConstructorOnStartupPriorityUtility()
    {
        var types = GenTypes.AllTypesWithAttribute<StaticConstructorOnStartupPriorityAttribute>();
        types.SortByDescending(t => t.GetCustomAttribute<StaticConstructorOnStartupPriorityAttribute>().priority);
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
        VMF_Harmony.PatchAllUncategorized();
    }
}

[StaticConstructorOnStartupPriority(Priority.Last)]
public static class HarmonyPatchReport
{
    static HarmonyPatchReport()
    {
        VMF_Log.Message($"{VehicleMapFramework.mod.Content.ModMetaData.ModVersion} rev{FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FilePrivatePart}");
        VMF_Log.Message($"{VMF_Harmony.Instance.GetPatchedMethods().Count()} patches applied.");
    }
}