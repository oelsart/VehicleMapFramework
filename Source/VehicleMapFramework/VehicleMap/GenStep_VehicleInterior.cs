using Verse;

namespace VehicleMapFramework;

public class GenStep_VehicleInterior : GenStep
{
    public override int SeedPart => 6546854;

    public override void Generate(Map map, GenStepParams parms)
    {
        var terrainGrid = map.terrainGrid;
        foreach (IntVec3 c in map.AllCells)
        {
            if (c.InBounds(map))
            {
                terrainGrid.SetTerrain(c, VMF_DefOf.VMF_VehicleFloor);
            }
        }
    }
}
