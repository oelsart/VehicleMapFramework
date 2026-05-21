using DevTools.Testing;
using RimWorld;
using VehicleMapFramework.VMF_HarmonyPatches;
using Vehicles;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

[TestFixture(TestType.Playing)]
public sealed class Test_WorkGiversCrossMap
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
            var fixture = new NestedTestFixture(test.GetType(), test.WorkGiverDef.defName, vehicleGroup);
            fixture.RunIndependent();
        }
    }
}