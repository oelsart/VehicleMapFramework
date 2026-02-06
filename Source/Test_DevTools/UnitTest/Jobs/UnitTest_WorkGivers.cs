using DevTools.Testing;
using RimWorld;
using UnityEngine.Assertions;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Vehicles.UnitTesting;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.Test_Logics;

[UnitTest(TestType.Playing)]
internal sealed class UnitTest_WorkGivers
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
            using var testGroup = new Test.Group($"BeforePatching: {test.WorkGiverDef.defName}");
            try
            {
                test.SetUp();
            }
            catch (Exception ex)
            {
                Assert.IsNull(ex, $"SetUp: {ex}");
            }
            try
            {
                test.ExecuteStep1();
            }
            catch (Exception ex)
            {
                Assert.IsNull(ex, $"ExecuteStep1: {ex}");
            }
            ClearPawnState(pawn);
        }
        
        VMF_Harmony.DynamicPatchAllNow(Level.All);
        TestUtils.ForceSpawn(vehicleGroup.vehicle);
        foreach (var test in workGiverTests)
        {
            using var testGroup = new Test.Group($"AfterPatching: {test.WorkGiverDef.defName}");
            try
            {
                test.ExecuteStep2();
            }
            catch (Exception ex)
            {
                Assert.IsNull(ex, $"ExecuteStep2: {ex}");
            }
            try
            {
                test.TearDown();
            }
            catch (Exception ex)
            {
                Assert.IsNull(ex, $"TearDown: {ex}");
            }
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