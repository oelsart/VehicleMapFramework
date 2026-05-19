using DevTools.Testing;
using RimWorld;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Vehicles.Testing;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.Test_Logics;

[TestFixture(TestType.Playing)]
internal sealed class Test_WorkGivers
{
    [Test]
    private void TestWorkGivers()
    {
        using var vehicleGroup = VehicleGroup.CreateBasicVehicleGroup(new VehicleGroup.MockSettings
        {
            vehicleDef = DefDatabase<VehicleDef>.GetNamed("MV_Crawler"),
            drivers = 1
        });
        var workGiverTests = typeof(WorkGiverTestBase).AllSubclassesNonAbstract()
            .Select(type => Activator.CreateInstance(type, vehicleGroup)).Cast<WorkGiverTestBase>().ToArray();
        vehicleGroup.SpawnPawns();
        var pawn = vehicleGroup.pawns[0];
        pawn.Map.weatherManager.curWeather = WeatherDefOf.Clear;
        MakePawnPerfect(pawn);
        Expect.IsTrue(EvacuateFromTestArea(pawn), "Evacuate from test area.");

        using var dynamicPatchEnabler = new DynamicPatchEnabler();
        VMF_Harmony.DynamicPatchAllNow(Level.Sensitive);
        foreach (var test in workGiverTests)
        {
            var fixture = new NestedTestFixture(test.BeforePatchingType, $"BeforePatch: {test.WorkGiverDef.defName}", test);
            fixture.RunIndependent();
            ClearPawnState(pawn);
        }
        
        VMF_Harmony.DynamicPatchAllNow(Level.All);
        TestUtils.ForceSpawn(vehicleGroup.vehicle);
        foreach (var test in workGiverTests)
        {
            var fixture = new NestedTestFixture(test.AfterPatchingType, $"AfterPatch: {test.WorkGiverDef.defName}", test);
            fixture.RunIndependent();
        }
    }
    
    internal static void ClearPawnState(Pawn pawn)
    {
        pawn.jobs?.EndCurrentJob(JobCondition.Succeeded, false, false);
        pawn.jobs?.ClearQueuedJobs(false);
        pawn.ClearAllReservations(false);
        pawn.pather?.StopDead();
        pawn.RemoveTargetInfo();
    }
}