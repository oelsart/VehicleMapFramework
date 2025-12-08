using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class UnitTest_PairtFloor(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("PaintFloor");

    private TerrainDef terrain;
    
    private IntVec3 cell;

    private Thing dye;
    
    public override void SetUp()
    {
        base.SetUp();
        cell = FromRUCorner(GroundMap, 3);
        terrain = cell.GetTerrain(GroundMap);
        GroundMap.terrainGrid.SetTerrain(cell, TerrainDefOf.WoodPlankFloor);
        var designation = new Designation(cell, DesignationDefOf.PaintFloor)
        {
            colorDef = ColorDefOf.PlanGray
        };
        GroundMap.designationManager.AddDesignation(designation);
        dye = GenSpawn.Spawn(ThingDefOf.Dye, Pawn.Position, VehicleMap);
        dye.stackCount = 75;
    }

    public override void TearDown()
    {
        GroundMap.terrainGrid.SetTerrain(cell, terrain);
        GroundMap.designationManager.TryRemoveDesignation(cell, DesignationDefOf.PaintFloor);
        dye.Destroy();
        dye = null;
        base.TearDown();
    }
}