using JetBrains.Annotations;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;

public class VehicleMapSettings : ModSettings
{
    public bool drawPlanet = Default.drawPlanet;

    public ForceRotated forceRotated = Default.forceRotated;

    public float weightFactor = Default.weightFactor;

    public bool autoGetOffPlayer = Default.autoGetOffPlayer;

    public bool autoGetOffNonPlayer = Default.autoGetOffNonPlayer;
    
    public bool crossMapJobProtect = Default.crossMapJobProtect;

    public bool drawVehicleMapGrid = Default.drawVehicleMapGrid;
    
    public bool includeMapThings = Default.includeMapThings;
    
    public bool aStarTraverse = Default.aStarTraverse;

    public bool joyPatches = Default.joyPatches;

    public bool treatAsPlayerHome = Default.treatAsPlayerHome;

    public ShowVehiclesOnColonistBar colonistBarMode = Default.colonistBarMode;

    public bool roofedPatch = Default.roofedPatch;

    public bool debugToolPatches = Default.debugToolPatches;

    public bool dynamicPatchEnabled = Default.dynamicPatchEnabled;

    public bool dynamicUnpatchEnabled = Default.dynamicUnpatchEnabled;

    public Level dynamicPatchLevel = Default.dynamicPatchLevel;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref drawPlanet, nameof(drawPlanet), Default.drawPlanet);
        Scribe_Values.Look(ref forceRotated, nameof(forceRotated), Default.forceRotated);
        Scribe_Values.Look(ref weightFactor, nameof(weightFactor), Default.weightFactor);
        Scribe_Values.Look(ref autoGetOffPlayer, nameof(autoGetOffPlayer), Default.autoGetOffPlayer);
        Scribe_Values.Look(ref autoGetOffNonPlayer, nameof(autoGetOffNonPlayer), Default.autoGetOffNonPlayer);
        Scribe_Values.Look(ref crossMapJobProtect, nameof(crossMapJobProtect), Default.crossMapJobProtect);
        Scribe_Values.Look(ref drawVehicleMapGrid, nameof(drawVehicleMapGrid), Default.drawVehicleMapGrid);
        Scribe_Values.Look(ref includeMapThings, nameof(includeMapThings), Default.includeMapThings);
        Scribe_Values.Look(ref aStarTraverse, nameof(aStarTraverse), Default.aStarTraverse);
        Scribe_Values.Look(ref joyPatches, nameof(joyPatches), Default.joyPatches);
        Scribe_Values.Look(ref treatAsPlayerHome, nameof(treatAsPlayerHome), Default.treatAsPlayerHome);
        Scribe_Values.Look(ref colonistBarMode, nameof(colonistBarMode), Default.colonistBarMode);
        Scribe_Values.Look(ref roofedPatch, nameof(roofedPatch), Default.roofedPatch);
        Scribe_Values.Look(ref debugToolPatches, nameof(debugToolPatches), Default.debugToolPatches);
        Scribe_Values.Look(ref dynamicPatchEnabled, nameof(dynamicPatchEnabled), Default.dynamicPatchEnabled);
        Scribe_Values.Look(ref dynamicUnpatchEnabled, nameof(dynamicUnpatchEnabled), Default.dynamicUnpatchEnabled);
        Scribe_Values.Look(ref dynamicPatchLevel, nameof(dynamicPatchLevel), Default.dynamicPatchLevel);
    }

    internal class Default
    {
        public const bool drawPlanet = true;

        public const ForceRotated forceRotated = ForceRotated.None;

        public const float weightFactor = 1f;

        public const bool autoGetOffPlayer = false;

        public const bool autoGetOffNonPlayer = true;
        
        public const bool crossMapJobProtect = true;

        public const bool drawVehicleMapGrid = false;
        
        public const bool includeMapThings = true;
        
        public const bool aStarTraverse = false;

        public const bool joyPatches = false;

        public const bool treatAsPlayerHome = false;
        
        public const ShowVehiclesOnColonistBar colonistBarMode = ShowVehiclesOnColonistBar.MouseIsOver;

        public const bool roofedPatch = false;

        public const bool debugToolPatches = false;

        public const bool dynamicPatchEnabled = false;

        public const bool dynamicUnpatchEnabled = false;

        public const Level dynamicPatchLevel = Level.Safe;
    }

    public enum ShowVehiclesOnColonistBar
    {
      DontShow,
      MouseIsOver,
      Always
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public enum ForceRotated
    {
      None = -1,
      North,
      East,
      South,
      West,
      NorthEast,
      NorthWest,
      SouthEast,
      SouthWest
    }
}