using DevTools.Testing;
using RimWorld;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Vehicles.UnitTesting;
using Verse;

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
        MakePawnPerfect(pawn);
        GenSpawn.Spawn(pawn, vehicle.VehicleMap.Center, vehicle.VehicleMap);

        using var dynamicPatchEnabler = new DynamicPatchEnabler();
        VMF_Harmony.DynamicPatchAllNow(Level.All);
        TestUtils.ForceSpawn(vehicle);
        vehicle.Map.weatherManager.curWeather = WeatherDefOf.Clear;
        foreach (var test in workGiverTests)
        {
            using var testGroup = new Test.Group($"CrossMap: {test.WorkGiverDef?.defName}");
            try
            {
                test.SetUp();
            }
            catch (Exception ex)
            {
                Expect.IsNull(ex, $"SetUp: {ex}");
            }

            try
            {
                test.Execute();
            }
            catch (Exception ex)
            {
                Expect.IsNull(ex, $"Execute: {ex}");
            }

            try
            {
                test.TearDown();
            }
            catch (Exception ex)
            {
                Expect.IsNull(ex, $"TearDown: {ex}");
            }
        }
    }
}