using RimWorld;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal class Test_Researcher(VehicleGroup group) : CrossMapWorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("Research");

    private Thing bench;

    public override void SetUp()
    {
        base.SetUp();
        Find.ResearchManager.SetCurrentProject(ResearchProjectDefOf.CarpetMaking);
        var map = GroundMap;
        bench = ThingMaker.MakeThing(ThingDefOf.SimpleResearchBench, ThingDefOf.WoodLog);
        GenSpawn.Spawn(bench, FromRUCorner(map, 4), map);
    }

    public override void TearDown()
    {
        Find.ResearchManager.StopProject(ResearchProjectDefOf.CarpetMaking);
        bench.Destroy();
        base.TearDown();
    }
}
