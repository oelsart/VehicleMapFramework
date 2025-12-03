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
public sealed class UnitTest_WorkGiversCrossMap
{
    [Test]
    private void TestCrossMapWorkGivers()
    {
        using var vehicleGroup = VehicleGroup.CreateBasicVehicleGroup(new VehicleGroup.MockSettings
        {
            vehicleDef = DefDatabase<VehicleDef>.GetNamed("MV_Crawler"),
            drivers = 1
        });
        var workGiverTests = typeof(CrossMapWorkGiverTestBase).AllSubclassesNonAbstract()
            .Select(type => Activator.CreateInstance(type, vehicleGroup)).Cast<CrossMapWorkGiverTestBase>().ToArray();
        var pawn = vehicleGroup.pawns[0];
        var vehicle = (VehiclePawnWithMap)vehicleGroup.vehicle;
        TestUtility.MakePawnPerfect(pawn);
        GenSpawn.Spawn(pawn, vehicle.VehicleMap.Center, vehicle.VehicleMap);

        using var dynamicPatchEnabler = new DynamicPatchEnabler();
        VMF_Harmony.DynamicPatchAllNow(Level.All);
        TestUtils.ForceSpawn(vehicle);
        vehicle.Map.weatherManager.curWeather = WeatherDefOf.Clear;
        foreach (var test in workGiverTests)
        {
            using var testGroup = new Test.Group($"CrossMap: {test.WorkGiverDef.defName}");
            try
            {
                test.SetUp();
                test.Execute();
                test.TearDown();
            }
            catch (Exception ex)
            {
                Assert.IsNull(ex, ex.ToString());
            }
        }
    }
}