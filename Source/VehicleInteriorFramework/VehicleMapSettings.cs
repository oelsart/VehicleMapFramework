using Verse;

namespace VehicleInteriors
{
    public class VehicleMapSettings : ModSettings
    {
        public bool drawPlanet = true;

        public float weightFactor = 1f;

        public bool threadingPathCost = true;

        public int minAreaForThreading = 150;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref this.drawPlanet, "drawPlanet", true);
            Scribe_Values.Look(ref this.weightFactor, "weightFactor", 1f);
            Scribe_Values.Look(ref this.threadingPathCost, "threadingPathCost", true);
            Scribe_Values.Look(ref this.minAreaForThreading, "minAreaForThreading", 150);
        }
    }
}