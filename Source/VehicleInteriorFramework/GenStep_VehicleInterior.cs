using RimWorld;
using Verse;

namespace VehicleInteriors
{
    public class GenStep_VehicleInterior : GenStep
    {
        public override int SeedPart => 654618698;

        public override void Generate(Map map, GenStepParams parms)
        {
            var terrainGrid = map.terrainGrid;
            foreach (var c in map.AllCells)
            {
                terrainGrid.SetTerrain(c, TerrainDefOf.MetalTile);
            }
        }
    }
}
