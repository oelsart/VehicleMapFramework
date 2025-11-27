using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class GenStep_SimpleTerrain : GenStep
{
    public override int SeedPart => 5546282;

    public override void Generate(Map map, GenStepParams parms)
    {
        var terrainGrid = map.terrainGrid;
        foreach (var c in map.AllCells)
        {
            var naturalTerrainAt = MapGenUtility.GetNaturalTerrainAt(c, map);
            terrainGrid.SetTerrain(c, naturalTerrainAt);
        }
    }
}