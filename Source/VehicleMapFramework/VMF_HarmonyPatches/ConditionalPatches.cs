using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

internal class VfVersionalPatchAttribute : Attribute
{

  internal const string LatestRelease = "1.6.2144";

  public VfVersionalPatchAttribute(string version, ComparisonType comparison = ComparisonType.Equal)
  {
    TargetVersion = Version.Parse(version);
    Available = comparison switch
    {
      ComparisonType.LessThan => CurrentVersion < TargetVersion,
      ComparisonType.LessThanOrEqual => CurrentVersion <= TargetVersion,
      ComparisonType.Equal => CurrentVersion == TargetVersion,
      ComparisonType.GreaterThan => CurrentVersion > TargetVersion,
      ComparisonType.GreaterThanOrEqual => CurrentVersion >= TargetVersion,
      ComparisonType.NotEqual => CurrentVersion != TargetVersion,
      _ => false
    };
  }

  public bool Available { get; init; }

  public Version TargetVersion { get; init; }

  public static Version CurrentVersion { get; } = Version.Parse(VehicleMod.metaData.ModVersion);
}

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class ConditionalPatches
{
  static ConditionalPatches()
  {
    // This class is just a placeholder for conditional patches.
    var method = AccessTools.Method(typeof(VehicleOrientationController), "VehicleCanStandAt");
    if (method is not null)
    {
      VMF_Harmony.Instance.Patch(method,
        AccessTools.Method(typeof(Patch_VehicleOrientationController_VehicleCanStandAt),
          nameof(Patch_VehicleOrientationController_VehicleCanStandAt.Prefix)),
        finalizer: AccessTools.Method(typeof(Patch_VehicleOrientationController_VehicleCanStandAt),
          nameof(Patch_VehicleOrientationController_VehicleCanStandAt.Finalizer)));
    }
  }

  internal static void DebugError(string methodName)
  {
    VMF_Log.DebugError($"The method {methodName} targeted for patching was not found. This should mean the removal of the stubs targeted for patching.");
  }
}

// 引数を変更するPRを出したので、変更を吸収するよう備えておく
[VfVersionalPatch(VfVersionalPatchAttribute.LatestRelease, ComparisonType.LessThanOrEqual)]
[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehicleGhostUtility_DrawGhostOverlays
{
  private static MethodBase TargetMethod()
  {
    var type = typeof(VehicleGhostUtility);
    const string name = nameof(VehicleGhostUtility.DrawGhostOverlays);
    List<Type> args =
    [
      typeof(IntVec3), typeof(Rot8), typeof(VehicleDef), typeof(Graphic), typeof(Color), typeof(AltitudeLayer),
      typeof(Thing)
    ];
    var method = AccessTools.Method(type, name, args.ToArray());
    if (method is null)
    {
      args.AddRange([typeof(Rot8?), typeof(float)]);
      method = AccessTools.Method(type, name, args.ToArray());
    }
    return method;
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenThing_TrueCenter2));
    codes.InsertAfter(
      CodeInstruction.LoadArgument(6),
      CodeInstruction.Call(typeof(Patch_VehicleGhostUtility_DrawGhostVehicleDef), nameof(Patch_VehicleGhostUtility_DrawGhostVehicleDef.ToTargetMapCoord)));
    return codes.Instructions();
  }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch]
public static class Patch_VehiclePath_DrawPath
{
  private static MethodBase TargetMethod()
  {
#if DEV
        var type = GenTypes.GetTypeInAnyAssembly("SmashTools.Burst.Ext_Path", "SmashTools.Burst");
        var method = AccessTools.Method(type, "DrawPath");
        if (method is not null) return method;
#endif
    return AccessTools.Method(typeof(VehiclePath), nameof(VehiclePath.DrawPath));
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return Patch_PawnPath_DrawPath.Transpiler(instructions, generator);
  }
}

// [HarmonyPatchCategory(PatchCategories.VehicleFramework)]
// [HarmonyPatch(typeof(VehicleOrientationController), "VehicleCanStandAt")]
// [PatchLevel(Level.Cautious)]
public static class Patch_VehicleOrientationController_VehicleCanStandAt
{
  public static void Prefix(VehiclePawn vehicle, ref VirtualTeleporter? __state)
  {
    if (vehicle.TryGetTargetMap(out var map) && vehicle.Map != map)
      __state = new VirtualTeleporter(vehicle, map);
  }

  public static void Finalizer(VirtualTeleporter? __state)
  {
    __state?.Dispose();
  }
}

[HarmonyPatchCategory(PatchCategories.VehicleFramework)]
[HarmonyPatch(typeof(VehicleGhostUtility), nameof(VehicleGhostUtility.DrawGhostVehicleDef))]
[PatchLevel(Level.Sensitive)]
public static class Patch_VehicleGhostUtility_DrawGhostVehicleDef
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    codes.MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_GenThing_TrueCenter2));
    codes.InsertAfter(
      CodeInstruction.LoadArgument(5),
      CodeInstruction.Call(typeof(Patch_VehicleGhostUtility_DrawGhostVehicleDef), nameof(ToTargetMapCoord)));
    return codes.Instructions();
  }

  public static Vector3 ToTargetMapCoord(Vector3 original, Thing thing)
  {
    return thing.TryGetTargetMap(out var map) ? original.ToBaseMapCoord(map).WithY(original.y) : original;
  }
}
