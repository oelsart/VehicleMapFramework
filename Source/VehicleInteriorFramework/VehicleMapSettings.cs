using Verse;

namespace VehicleInteriors
{
    public class VehicleMapSettings : ModSettings
    {
        public bool drawPlanet = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref this.drawPlanet, "drawPlanet", true);
        }
    }
}
