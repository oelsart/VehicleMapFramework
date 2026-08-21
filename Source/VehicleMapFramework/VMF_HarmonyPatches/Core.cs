global using static VehicleMapFramework.MethodInfoCache;
global using static VehicleMapFramework.ModCompat;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using HarmonyLib;
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
  public readonly Level level = level;
}

public class VMF_Harmony
{
  internal static readonly Harmony Instance = new("OELS.VehicleMapFramework");

  internal static readonly List<string> Categories = [];

  internal static Dictionary<string, List<Type>> PatchesInCategories
  {
    get
    {
      if (field is null)
      {
        var assemblies = VehicleMapFramework.mod.Content.assemblies.loadedAssemblies;
        field = GenTypes.AllTypes.AsParallel()
          .Where(t => assemblies.Contains(t.Assembly) &&
                      t.CustomAttributes
                        .Select(attribute => attribute.AttributeType)
                        .Contains(typeof(HarmonyPatch)))
          .GroupBy(t =>
          {
            var patchCategory = t.CustomAttributes
              .FirstOrDefault(attribute => attribute.AttributeType == typeof(HarmonyPatchCategory));

            if (patchCategory is null) return "";
            var category = patchCategory.ConstructorArguments
              .Select(c => c.Value).OfType<string>().FirstOrDefault();
            return category ?? "";
          }).ToDictionary(group => group.Key, group =>
          {
            if (group.Key == PatchCategories.VehicleFramework)
            {
              return group.Where(t => t.GetCustomAttribute<VFVersionalPatchAttribute>() is not { } attr ||
                                      attr.Available).ToList();
            }

            return group.ToList();
          });
      }
      
      return field;
    }
  }

  internal static Level CurrentPatchLevel { get; private set; } =
    (VehicleMapFramework.settings?.dynamicPatchEnabled ?? false)
      ? VehicleMapFramework.settings.dynamicPatchLevel
      : Level.All;

  internal static Level PrevPatchLevel { get; set; } = Level.Mandatory;

  private static readonly AccessTools.FieldRef<PatchClassProcessor, object> patchMethodsRef =
    AccessTools.FieldRefAccess<PatchClassProcessor, object>("patchMethods");

  private static readonly AccessTools.FieldRef<object, HarmonyMethod> infoRef =
    AccessTools.FieldRefAccess<HarmonyMethod>("HarmonyLib.AttributePatch:info");

  private static readonly MethodInfo m_RemoveAll = AccessTools.Method(
    typeof(List<>).MakeGenericType(AccessTools.TypeByName("HarmonyLib.AttributePatch")), nameof(List<>.RemoveAll));

  internal static bool OutOfRange(Level level)
  {
    if ((level < PrevPatchLevel && level < CurrentPatchLevel) ||
        (level > PrevPatchLevel && level > CurrentPatchLevel))
      return true;
    
    var max = PrevPatchLevel > CurrentPatchLevel ? PrevPatchLevel : CurrentPatchLevel;
    return level == max;
  }

  internal static void AdjustPatchLevel(PatchClassProcessor patchClassProcessor)
  {
    m_RemoveAll.Invoke(patchMethodsRef(patchClassProcessor), SingleParam.Get((Predicate<object>)Predicate));
    return;

    static bool Predicate(object attributePatch)
    {
      var method = infoRef(attributePatch).method;
      var attribute = method.GetCustomAttribute<PatchLevelAttribute>();
      var level = attribute?.level ??
                  method.DeclaringType?.GetCustomAttribute<PatchLevelAttribute>()?.level ?? Level.Mandatory;
      return OutOfRange(level);
    }
  }

  internal static bool CheckClassPatchLevel(Type type)
  {
    var attribute = type.GetCustomAttribute<PatchLevelAttribute>();
    if (attribute is null) return true;
    return !OutOfRange(attribute.level);
  }

  internal static void DynamicPatchAllNow(Level patchLevel)
  {
    if (CurrentPatchLevel < patchLevel)
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
      VMF_Log.Message($"Dynamic patches applied: {(patchCountAfter - patchCountBefore).ToString()} Total: {patchCountAfter.ToString()}");
    }
    else if (VehicleMapFramework.settings.dynamicUnpatchEnabled && CurrentPatchLevel != patchLevel)
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
      VMF_Log.Message($"Dynamic patches unapplied: {(patchCountBefore - patchCountAfter).ToString()} Total: {patchCountAfter.ToString()}");
    }
  }

  internal static void DynamicPatchAll(Level patchLevel)
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
        VMF_Log.Message($"Dynamic patches applied: {(patchCountAfter - patchCountBefore).ToString()} Total: {patchCountAfter.ToString()}");
      }, "VMF_ApplyingDynamicPatches", false, null, false);
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
        VMF_Log.Message($"Dynamic patches unapplied: {(patchCountBefore - patchCountAfter).ToString()} Total: {patchCountAfter.ToString()}");
      }, "VMF_UnpatchingDynamicPatches", false, null, false);
    }
  }

  internal static List<Type> PatchClassesInCategory(string category)
  {
    if (!PatchesInCategories.TryGetValue(category, out var list))
    {
      VMF_Log.Error($"Patches for category ({category}) not included in this mod");
      return [];
    }
    return list;
  }

  internal static void PatchCategory(string category)
  {
    if (!Categories.Contains(category))
    {
      Categories.Add(category);
    }
    
    var patches = PatchClassesInCategory(category)
      .Where(CheckClassPatchLevel);
    if (category == PatchCategories.VehicleFramework)
    {
      patches = patches.Where(t => t.GetCustomAttribute<VFVersionalPatchAttribute>() is not { } attr ||
                                   attr.Available);
    }

    patches.Select(t => Instance.CreateClassProcessor(t))
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
    PatchClassesInCategory(category)
      .Where(CheckClassPatchLevel)
      .Select(t => Instance.CreateClassProcessor(t))
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
    PatchClassesInCategory("")
      .Where(CheckClassPatchLevel)
      .Select(t => Instance.CreateClassProcessor(t))
      .Do(patchClass =>
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
    PatchClassesInCategory("")
      .Where(CheckClassPatchLevel)
      .Select(t => Instance.CreateClassProcessor(t))
      .Do(patchClass =>
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
}

public static class EarlyPatchCore
{
  public const string Category = "VehicleMapFramework.EarlyPatches";

  public static void EarlyPatch()
  {
    VMF_Harmony.PatchCategory(Category);
  }
}

[StaticConstructorOnStartupPriority(Priority.Normal)]
public static class Core
{
  static Core()
  {
    VMF_Harmony.PatchAllUncategorized();
    VMF_Harmony.PatchCategory(PatchCategories.VehicleFramework);
  }
}

[StaticConstructorOnStartupPriority(Priority.Last)]
public static class LatePatchCore
{
  public const string Category = "VehicleMapFramework.LatePatches";

  static LatePatchCore()
  {
    LongEventHandler.ExecuteWhenFinished(() =>
    {
      VMF_Harmony.PatchCategory(Category);

      var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?.Split('.');
      if (version != null)
      {
        var major = version.ElementAtOrDefault(0) ?? "0";
        var minor = version.ElementAtOrDefault(1) ?? "0";
        var build = version.ElementAtOrDefault(2) ?? "0";
        var revision = version.ElementAtOrDefault(3) ?? "0";
        var text = $"{major}.{minor}.{build} rev{revision}";
#if DEV
        text += " Dev";
#endif
        VMF_Log.Message(text);
      }

      VMF_Log.Message($"{VMF_Harmony.Instance.GetPatchedMethods().Count()} patches applied.");
    });
  }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class StaticConstructorOnStartupPriorityAttribute(int priority) : Attribute
{
  public readonly int priority = priority;
}