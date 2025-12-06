using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;



public class VehicleMapSettings : ModSettings
{
    public bool drawPlanet = Default.drawPlanet;

    public float weightFactor = Default.weightFactor;

    public bool autoGetOffPlayer = Default.autoGetOffPlayer;

    public bool autoGetOffNonPlayer = Default.autoGetOffNonPlayer;

    public bool drawVehicleMapGrid = Default.drawVehicleMapGrid;
    
    public bool includeMapThings = Default.includeMapThings;

    public bool roofedPatch = Default.roofedPatch;

    public bool debugToolPatches = Default.debugToolPatches;

    public bool dynamicPatchEnabled = Default.dynamicPatchEnabled;

    public bool dynamicUnpatchEnabled = Default.dynamicUnpatchEnabled;

    public Level dynamicPatchLevel = Default.dynamicPatchLevel;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref drawPlanet, "drawPlanet", Default.drawPlanet);
        Scribe_Values.Look(ref weightFactor, "weightFactor", Default.weightFactor);
        Scribe_Values.Look(ref drawVehicleMapGrid, "drawVehicleMapGrid", Default.drawVehicleMapGrid);
        Scribe_Values.Look(ref includeMapThings, "includeMapThings", Default.includeMapThings);
        Scribe_Values.Look(ref roofedPatch, "roofedPatch", Default.roofedPatch);
        Scribe_Values.Look(ref debugToolPatches, "debugToolPatches", Default.debugToolPatches);
        Scribe_Values.Look(ref dynamicPatchEnabled, "dynamicPatchEnabled", Default.dynamicPatchEnabled);
        Scribe_Values.Look(ref dynamicUnpatchEnabled, "dynamicUnpatchEnabled", Default.dynamicUnpatchEnabled);
        Scribe_Values.Look(ref dynamicPatchLevel, "dynamicPatchLevel", Default.dynamicPatchLevel);
    }

    internal class Default
    {
        public const bool drawPlanet = true;

        public const float weightFactor = 1f;

        public const bool autoGetOffPlayer = false;

        public const bool autoGetOffNonPlayer = true;

        public const bool drawVehicleMapGrid = false;
        
        public const bool includeMapThings = false;

        public const bool roofedPatch = false;

        public const bool debugToolPatches = false;

        public const bool dynamicPatchEnabled = false;

        public const bool dynamicUnpatchEnabled = false;

        public const Level dynamicPatchLevel = Level.Safe;
    }
}