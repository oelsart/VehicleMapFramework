using DevTools.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

[TestFixture(TestType.Playing)]
internal sealed class Test_VehicleMapParentsComponent
{
  [Test]
  public void ValidateCache()
  {
    var map = Find.CurrentMap;
    Expect.AreEqual(VehicleMapParentsComponent.GetMapIndex(map), map.Index);
    Expect.IsNull(VehicleMapParentsComponent.GetCachedVehicle(map));

    var crawler = (VehiclePawnWithMap)DefaultVehicleGroup.vehicle;
    Expect.AreEqual(VehicleMapParentsComponent.GetMapIndex(crawler.VehicleMap), crawler.VehicleMap.Index);
    Expect.IsNotNull(VehicleMapParentsComponent.GetCachedVehicle(crawler.VehicleMap));

    var crawler2 = (VehiclePawnWithMap)DefaultVehicleGroup.vehicle;
    Expect.AreEqual(VehicleMapParentsComponent.GetMapIndex(crawler2.VehicleMap), crawler2.VehicleMap.Index);
    Expect.IsNotNull(VehicleMapParentsComponent.GetCachedVehicle(crawler2.VehicleMap));
    crawler.Destroy();
    
    Expect.AreEqual(VehicleMapParentsComponent.GetMapIndex(crawler2.VehicleMap), crawler2.VehicleMap.Index);
    Expect.IsNotNull(VehicleMapParentsComponent.GetCachedVehicle(crawler2.VehicleMap));
    crawler2.Destroy();

    Expect.AreEqual(VehicleMapParentsComponent.GetMapIndex(map), map.Index);
    Expect.IsNull(VehicleMapParentsComponent.GetCachedVehicle(map));
  }
}