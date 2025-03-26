using HarmonyLib;
using RimWorld;
using SmashTools;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class Patches_BillDoorsFramework
    {
        static Patches_BillDoorsFramework()
        {
            if (ModCompat.BillDoorsFramework)
            {
                VMF_Harmony.Instance.PatchCategory("VMF_Patches_BillDoorsFramework");
            }
        }
    }

    [HarmonyPatchCategory("VMF_Patches_BillDoorsFramework")]
    [HarmonyPatch("BillDoorsFramework.PlaceWorker_ShowVerbRadiusBySight", "AllowsPlacing")]
    [StaticConstructorOnStartup]
    public static class Patch_PlaceWorker_ShowVerbRadiusBySight_AllowsPlacing
    {
        public static bool Prefix(BuildableDef checkingDef, IntVec3 loc, Map map, ref AcceptanceReport __result)
        {
            __result = true;
            if (KeyBindingDefOf.ShowEyedropper.IsDown)
            {
                if (locCache != loc)
                {
                    cellCache.Clear();
                    badCellCache.Clear();
                    foreach (VerbProperties verbProperties in ((ThingDef)checkingDef).building.turretGunDef.Verbs)
                    {
                        locCache = loc;
                        if (map.IsVehicleMapOf(out var vehicle) && vehicle.Spawned)
                        {
                            loc = loc.ToBaseMapCoord(vehicle);
                            map = vehicle.Map;
                        }
                        Parallel.ForEach(GenRadial.RadialCellsAround(loc, verbProperties.minRange, verbProperties.range), cell =>
                        {
                            if (GenSightOnVehicle.LineOfSight(loc, cell, map))
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
                    GenDraw.DrawFieldEdges(cellCache.Keys.ToList());
                    foreach (IntVec3 c in cellCache.Keys)
                    {
                        CellRenderer.RenderCell(c, greenMat);
                    }
                }
                if (badCellCache.Any())
                {
                    foreach (IntVec3 c in badCellCache.Keys)
                    {
                        CellRenderer.RenderCell(c, redMat);
                    }
                }
            }
            foreach (VerbProperties verbProperties2 in ((ThingDef)checkingDef).building.turretGunDef.Verbs)
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

        private static IntVec3 locCache;

        private static ConcurrentSet<IntVec3> cellCache = new ConcurrentSet<IntVec3>();

        private static ConcurrentSet<IntVec3> badCellCache = new ConcurrentSet<IntVec3>();

        private static Material redMat;

        private static Material greenMat;

        static Patch_PlaceWorker_ShowVerbRadiusBySight_AllowsPlacing()
        {
            redMat = DebugMatsSpectrum.Mat(0, false);
            redMat.color = redMat.color.ToTransparent(0.1f);
            greenMat = DebugMatsSpectrum.Mat(50, false);
            greenMat.color = greenMat.color.ToTransparent(0.1f);
        }
    }
}
