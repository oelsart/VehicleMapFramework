using HarmonyLib;
using SmashTools;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using VehicleInteriors.VMF_HarmonyPatches;
using Verse;

namespace VehicleInteriors
{
    public class VehicleInteriors : Mod
    {
        public static VehicleInteriors mod;

        public static VehicleMapSettings settings;

        public static AssetBundle Bundle
        {
            get
            {
                if (bundleInt == null)
                {
                    bundleInt = AssetBundle.LoadFromFile($@"{VehicleInteriors.mod.Content.RootDir}\Common\AssetBundles\{PlatformInfo}");
                }
                return bundleInt;
            }
        }

        private static AssetBundle bundleInt;

        private static string PlatformInfo
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return "StandaloneWindows64";
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return "StandaloneLinux64";
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return "StandaloneOSX";
                }
                Log.Error($"[VehicleMapFramework] {RuntimeInformation.OSDescription} is not supported platform. Please let the mod author know the OS info.");
                return null;
            }
        }

        public VehicleInteriors(ModContentPack content) : base(content)
        {
            VehicleInteriors.mod = this;
            VehicleInteriors.settings = base.GetSettings<VehicleMapSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.CheckboxLabeled("VMF_Settings.DrawPlanet".Translate(), ref settings.drawPlanet);
            listingStandard.CheckboxLabeled("VMF_Settings.RoofedPatch".Translate(), ref settings.roofedPatch);
            listingStandard.SliderLabeled("VMF_Settings.WeightFactor".Translate(), null, null, ref settings.weightFactor, 0f, 3f);
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("VMF_Settings.ThreadingPathCost".Translate(), ref settings.threadingPathCost);
            if (settings.threadingPathCost)
            {
                listingStandard.SliderLabeled("VMF_Settings.MinAreaForThreading".Translate(), null, null, ref settings.minAreaForThreading, 0, 2500, 1, "0", "2500");
            }
            listingStandard.CheckboxLabeled("VMF_Settings.AsyncClosestThing".Translate(), ref settings.asyncClosestThing);
            if (settings.asyncClosestThing)
            {
                listingStandard.SliderLabeled("VMF_Settings.MinSearchSetCount".Translate(), null, null, ref settings.minSearchSetCount, 0, 640, 1, "0", "640");
            }
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("(Debug) Draw vehicle map grid", ref settings.drawVehicleMapGrid);
            listingStandard.CheckboxLabeled("(Debug) Enable debug tool patches", ref settings.debugToolPatches);
            listingStandard.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            var m_Roofed = AccessTools.Method(typeof(RoofGrid), nameof(RoofGrid.Roofed), new Type[] { typeof(IntVec3) });
            if (VMF_Harmony.Instance.GetPatchedMethods().Contains(m_Roofed))
            {
                if (!settings.roofedPatch)
                {
                    var m_Postfix = AccessTools.Method(typeof(Patch_RoofGrid_Roofed), nameof(Patch_RoofGrid_Roofed.Postfix));
                    VMF_Harmony.Instance.Unpatch(m_Roofed, m_Postfix);
                }
            }
            else if (settings.roofedPatch)
            {
                var m_Postfix = AccessTools.Method(typeof(Patch_RoofGrid_Roofed), nameof(Patch_RoofGrid_Roofed.Postfix));
                VMF_Harmony.Instance.Patch(m_Roofed, m_Postfix);
            }

            var m_GenericRectTool = AccessTools.Method(typeof(DebugToolsGeneral), nameof(DebugToolsGeneral.GenericRectTool));
            if (VMF_Harmony.Instance.GetPatchedMethods().Contains(m_GenericRectTool))
            {
                if (!settings.debugToolPatches)
                {
                    Patches_DebugTools.ApplyPatches(unpatch: true);
                }
            }
            else if (settings.debugToolPatches)
            {
                Patches_DebugTools.ApplyPatches();
            }
        }

        public override string SettingsCategory()
        {
            return "Vehicle Map Framework";
        }
    }
}
