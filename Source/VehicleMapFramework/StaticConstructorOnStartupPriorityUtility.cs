using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public static class StaticConstructorOnStartupPriorityUtility
{
  static StaticConstructorOnStartupPriorityUtility()
  {
    var types = GenTypes.AllTypesWithAttribute<StaticConstructorOnStartupPriorityAttribute>();
    types.SortByDescending(t => t.GetCustomAttribute<StaticConstructorOnStartupPriorityAttribute>().priority);

    foreach (var type in types)
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
