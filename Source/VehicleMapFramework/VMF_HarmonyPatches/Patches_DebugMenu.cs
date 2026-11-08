using HarmonyLib;
using JetBrains.Annotations;
using LudeonTK;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_DebugTools
{
  static Patches_DebugTools()
  {
    if (VehicleMapFramework.Settings.debugToolPatches)
    {
      ApplyPatches();
    }
  }

  public static void ApplyPatches(bool unpatch = false)
  {
    var m_DebugToolOnGUI = AccessTools.Method(typeof(DebugTool), nameof(DebugTool.DebugToolOnGUI));
    var m_Prefix = ((Delegate)Prefix).Method;
    var m_Finalizer = ((Delegate)Finalizer).Method;
    if (unpatch)
    {
      VMF_Harmony.Instance.Unpatch(m_DebugToolOnGUI, m_Prefix);
      VMF_Harmony.Instance.Unpatch(m_DebugToolOnGUI, m_Finalizer);
    }
    else
    {
      VMF_Harmony.Instance.Patch(m_DebugToolOnGUI, prefix: m_Prefix, finalizer: m_Finalizer);
    }
  }

  private static void Prefix([MustDisposeResource] ref FocusMapScope __state)
  {
    if (Find.CurrentMap is null ||
        !UI.MouseMapPosition().TryGetVehicleMap(Find.CurrentMap, out var vehicle))
      return;

    __state = FocusMapScope.FocusMapUnsafe(vehicle.CurrentLevel);
  }
  
  private static void Finalizer(FocusMapScope __state) => __state.Dispose();
}