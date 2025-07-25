using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;



public class VehicleMapSettings : ModSettings
{
    internal static Default DefaultSettings = new();

    public bool drawPlanet = DefaultSettings.drawPlanet;

    public float weightFactor = DefaultSettings.weightFactor;

    public bool autoGetOffPlayer = DefaultSettings.autoGetOffPlayer;

    public bool autoGetOffNonPlayer = DefaultSettings.autoGetOffNonPlayer;

    public bool drawVehicleMapGrid = DefaultSettings.drawVehicleMapGrid;

    public bool roofedPatch = DefaultSettings.roofedPatch;

    public bool debugToolPatches = DefaultSettings.debugToolPatches;

    public bool dynamicPatchEnabled = DefaultSettings.dynamicPatchEnabled;

    public bool dynamicUnpatchEnabled = DefaultSettings.dynamicUnpatchEnabled;

    public Level dynamicPatchLevel = DefaultSettings.dynamicPatchLevel;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref drawPlanet, "drawPlanet", DefaultSettings.drawPlanet);
        Scribe_Values.Look(ref weightFactor, "weightFactor", DefaultSettings.weightFactor);
        Scribe_Values.Look(ref drawVehicleMapGrid, "drawVehicleMapGrid", DefaultSettings.drawVehicleMapGrid);
        Scribe_Values.Look(ref roofedPatch, "roofedPatch", DefaultSettings.roofedPatch);
        Scribe_Values.Look(ref debugToolPatches, "debugToolPatches", DefaultSettings.debugToolPatches);
        Scribe_Values.Look(ref dynamicPatchEnabled, "dynamicPatchEnabled", DefaultSettings.dynamicPatchEnabled);
        Scribe_Values.Look(ref dynamicUnpatchEnabled, "dynamicUnpatchEnabled", DefaultSettings.dynamicUnpatchEnabled);
        Scribe_Values.Look(ref dynamicPatchLevel, "dynamicPatchLevel", DefaultSettings.dynamicPatchLevel);
    }

    internal class Default
    {
        public readonly bool drawPlanet = true;

        public readonly float weightFactor = 1f;

        public readonly bool autoGetOffPlayer = false;

        public readonly bool autoGetOffNonPlayer = true;

        public readonly bool drawVehicleMapGrid = false;

        public readonly bool roofedPatch = false;

        public readonly bool debugToolPatches = false;

        public readonly bool dynamicPatchEnabled = false;

        public readonly bool dynamicUnpatchEnabled = false;

        public readonly Level dynamicPatchLevel = Level.Safe;
    }
}