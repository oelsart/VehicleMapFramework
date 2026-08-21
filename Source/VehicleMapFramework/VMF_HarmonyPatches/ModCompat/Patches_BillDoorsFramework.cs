using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal static class Patches_BillDoorsFramework
{
  static Patches_BillDoorsFramework()
  {
    if (BillDoorsFramework)
    {
      VMF_Harmony.PatchCategory(PatchCategories.BillDoorsFramework);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.BillDoorsFramework)]
[HarmonyPatch("BillDoorsFramework.PlaceWorker_ShowVerbRadiusBySight", "AllowsPlacing")]
[PatchLevel(Level.Safe)]
[StaticConstructorOnStartup]
public static class Patch_PlaceWorker_ShowVerbRadiusBySight_AllowsPlacing
{

  private static IntVec3 locCache;

  private static readonly ConcurrentSet<IntVec3> cellCache;

  private static readonly ConcurrentSet<IntVec3> badCellCache;

  private static readonly Material redMat;

  private static readonly Material greenMat;

  static Patch_PlaceWorker_ShowVerbRadiusBySight_AllowsPlacing()
  {
    if (BillDoorsFramework)
    {
      redMat = DebugMatsSpectrum.Mat(0, false);
      redMat.color = redMat.color.ToTransparent(0.1f);
      greenMat = DebugMatsSpectrum.Mat(50, false);
      greenMat.color = greenMat.color.ToTransparent(0.1f);
      cellCache = [];
      badCellCache = [];
    }
  }

  public static bool Prefix(BuildableDef checkingDef, IntVec3 loc, Map map, ref AcceptanceReport __result)
  {
    __result = true;
    if (KeyBindingDefOf.ShowEyedropper.IsDown)
    {
      if (locCache != loc)
      {
        cellCache.Clear();
        badCellCache.Clear();
        if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
        {
          loc = loc.ToBaseMapCoord(vehicle);
          map = vehicle.Map;
        }
        foreach (var verbProperties in ((ThingDef)checkingDef).building.turretGunDef.Verbs)
        {
          locCache = loc;
          Parallel.ForEach(GenRadial.RadialCellsAround(loc, verbProperties.minRange, verbProperties.range),
            cell =>
            {
              if (GenSightOnVehicle.LineOfSight(loc, cell, map, false))
              {
                cellCache.Add(cell);
              }
              else
              {
                badCellCache.Add(cell);
              }
            });
        }
      }
      if (cellCache.Any())
      {
        GenDraw.DrawFieldEdges([.. cellCache.Keys]);
        foreach (var c in cellCache.Keys)
        {
          CellRenderer.RenderCell(c, greenMat);
        }
      }
      if (badCellCache.Any())
      {
        foreach (var c in badCellCache.Keys)
        {
          CellRenderer.RenderCell(c, redMat);
        }
      }
    }
    foreach (var verbProperties2 in ((ThingDef)checkingDef).building.turretGunDef.Verbs)
    {
      if (verbProperties2.range > 0f)
      {
        GenDraw.DrawRadiusRing(loc, verbProperties2.range);
      }
      if (verbProperties2.minRange > 0f)
      {
        GenDraw.DrawRadiusRing(loc, verbProperties2.minRange);
      }
    }
    return false;
  }
}
