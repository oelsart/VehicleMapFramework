using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[AttributeUsage(AttributeTargets.Class)]
internal partial class VFVersionalPatchAttribute : Attribute
{
  public VFVersionalPatchAttribute(string version, ComparisonType comparison = ComparisonType.Equal)
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

  public bool Available { get; }

  private Version TargetVersion { get; }

  private static Version CurrentVersion { get; } =
    Version.Parse(MyRegex().Replace(VehicleMod.metaData?.ModVersion ?? "", ""));

  [GeneratedRegex(@"[^\d.]")]
  private static partial Regex MyRegex();
}