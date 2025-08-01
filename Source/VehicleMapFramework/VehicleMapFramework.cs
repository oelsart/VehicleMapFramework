﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VehicleMapFramework.Settings;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;

public class VehicleMapFramework : Mod
{
    public static VehicleMapFramework mod;

    public static VehicleMapSettings settings;

    private static List<TabRecord> tabs = [];

    public static AssetBundle Bundle
    {
        get
        {
            if (bundleInt == null)
            {
                bundleInt = mod.Content.assetBundles.loadedAssetBundles[0];
            }
            return bundleInt;
        }
    }

    private static AssetBundle bundleInt;

    public VehicleMapFramework(ModContentPack content) : base(content)
    {
        mod = this;
        settings = GetSettings<VehicleMapSettings>();
        EarlyPatchCore.EarlyPatch();
    }

    public void InitializeTabs()
    {
        tabs.Clear();
        var tabDrawers = typeof(SettingsTabDrawer).AllSubclassesNonAbstract()
            .Select(Activator.CreateInstance).Cast<SettingsTabDrawer>()
            .OrderBy(tab => tab.Index).ToList();
        CurrentTab = tabDrawers[0];
        tabs.AddRange(tabDrawers.Select(tab => new TabRecord(tab.Label, () => CurrentTab = tab, () => CurrentTab == tab)));
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
        TabDrawer.DrawTabs(rect, tabs);
        CurrentTab.Draw(rect.ContractedBy(10f));
    }

    public override void WriteSettings()
    {
        base.WriteSettings();

        Level level;
        if (settings.dynamicPatchEnabled && !VehicleMapParentsComponent.CachedParentVehicle.Any(p => p.Value != null))
        {
            level = settings.dynamicPatchLevel;
        }
        else
        {
            level = Level.All;
        }
        if (VMF_Harmony.CurrentPatchLevel != level)
        {
            VMF_Harmony.DynamicPatchAll(level);
        }

        var m_Roofed = AccessTools.Method(typeof(RoofGrid), nameof(RoofGrid.Roofed), [typeof(IntVec3)]);
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
    }

    public override string SettingsCategory()
    {
        return "Vehicle Map Framework";
    }
}
