using Verse;

namespace VehicleInteriors
{
    public class VehicleMapSettings : ModSettings
    {
        internal static Default DefaultSettings = new Default();

        public bool drawPlanet = DefaultSettings.drawPlanet;

        public float weightFactor = DefaultSettings.weightFactor;

        public bool threadingPathCost = DefaultSettings.threadingPathCost;

        public int minAreaForThreading = DefaultSettings.minAreaForThreading;

        public bool roofedPatch = DefaultSettings.roofedPatch;

        public bool drawVehicleMapGrid = DefaultSettings.drawVehicleMapGrid;

        public bool debugToolPatches = DefaultSettings.debugToolPatches;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref this.drawPlanet, "drawPlanet", DefaultSettings.drawPlanet);
            Scribe_Values.Look(ref this.weightFactor, "weightFactor", DefaultSettings.weightFactor);
            Scribe_Values.Look(ref this.threadingPathCost, "threadingPathCost", DefaultSettings.threadingPathCost);
            Scribe_Values.Look(ref this.minAreaForThreading, "minAreaForThreading", DefaultSettings.minAreaForThreading);
            Scribe_Values.Look(ref this.roofedPatch, "roofedPatch", DefaultSettings.roofedPatch);
            Scribe_Values.Look(ref this.drawVehicleMapGrid, "drawVehicleMapGrid", DefaultSettings.drawVehicleMapGrid);
            Scribe_Values.Look(ref this.debugToolPatches, "debugToolPatches", DefaultSettings.debugToolPatches);
        }

        internal class Default
        {
            public readonly bool drawPlanet = true;

            public readonly float weightFactor = 1f;

            public readonly bool threadingPathCost = true;

            public readonly int minAreaForThreading = 150;

            public readonly bool roofedPatch = false;

            public readonly bool drawVehicleMapGrid = false;

            public readonly bool debugToolPatches = false;
        }
    }
}