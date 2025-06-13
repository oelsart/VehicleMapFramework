using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using VehicleInteriors.Settings;
using VehicleInteriors.VMF_HarmonyPatches;
using Vehicles;
using Verse;
using VMF_PUAHPatch;

namespace VehicleInteriors
{
    public class VehicleInteriors : Mod
    {
        public static VehicleInteriors mod;

        public static VehicleMapSettings settings;

        private static List<TabRecord> tabs = new List<TabRecord>();

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

        public void InitializeTabs()
        {
            VehicleInteriors.tabs.Clear();
            var mainTab = new SettingsTab_Main();
            VehicleInteriors.tabs.Add(new TabRecord("VMF_Settings.Tab.Main".Translate(), () =>
            {
                CurrentTab = mainTab;
            }, () => CurrentTab == mainTab));
            var PUAHTab = new SettingsTab_VMF_PUAHPatch();
            VehicleInteriors.tabs.Add(new TabRecord("VMF_Settings.Tab.PUAHPatch".Translate(), () =>
            {
                CurrentTab = PUAHTab;
            }, () => CurrentTab == PUAHTab));

            CurrentTab = mainTab;
        }

        internal static SettingsTabDrawer CurrentTab { get; set; }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (CurrentTab == null)
            {
                InitializeTabs();
            }

            base.DoSettingsWindowContents(inRect);
            var rect = new Rect(inRect.x, inRect.y + TabDrawer.TabHeight, inRect.width, inRect.height - TabDrawer.TabHeight);
            Widgets.DrawMenuSection(rect);
            TabDrawer.DrawTabs(rect, VehicleInteriors.tabs);
            CurrentTab.Draw(rect.ContractedBy(10f));
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            VMF_PUAHMod.mod.WriteSettings();

            MethodInfoCache.CachedMethodInfo = new MethodInfoCache();
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
                VMF_Harmony.Instance.Patch(m_Roofed, postfix: m_Postfix);
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

            var m_SetSpotsToJobAcrossMaps = JobAcrossMapsUtilityPatch.TargetMethod();
            if (VMF_Harmony.Instance.GetPatchedMethods().Contains(m_SetSpotsToJobAcrossMaps))
            {
                if (!VMF_PUAHMod.settings.patchEnabled)
                {
                    VMF_CompatibilityPatchMod.ApplyPatches(unpatch: true);
                }
            }
            else if (VMF_PUAHMod.settings.patchEnabled)
            {
                VMF_CompatibilityPatchMod.ApplyPatches();
            }
            MethodInfoCache.CachedMethodInfo = null;
        }

        public override string SettingsCategory()
        {
            return "Vehicle Map Framework";
        }
    }
}
