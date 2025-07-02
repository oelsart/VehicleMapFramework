using Verse;

namespace VehicleInteriors;

public class GenStep_VehicleInterior : GenStep
{
    public override int SeedPart => 654618698;

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
