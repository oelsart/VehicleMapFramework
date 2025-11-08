using RimWorld;
using Verse;

namespace VehicleMapFramework.Test_Logics;

public class GenStep_Empty : GenStep
{
    public override int SeedPart => 0;

    public override void Generate(Map map, GenStepParams parms)
    {
        var terrainGrid = map.terrainGrid;
        foreach (var c in map.AllCells)
        {
            if (c.InBounds(map))
            {
                terrainGrid.SetTerrain(c, TerrainDefOf.Sand);
            }
        }
    }
}