using DevTools.Testing;
using UnityEngine;
using Vehicles;
using Vehicles.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

[TestFixture(TestType.Playing)]
public class Test_VehicleMapRenderTexture
{
    [Test]
    public void CacheExpiration_Ticks()
    {
        var cache = new VehicleMapUIRenderer.CachedMapTexture(null, false, GenTicks.TicksGame);
        Expect.IsFalse(cache.Expired, "It is not expired immediately after creation.");
        const int duration = VehicleMapUIRenderer.CachedMapTexture.CacheDurationTicks;
        for (var i = 0; i < duration + 1; i++)
        {
            Find.TickManager.DoSingleTick();
        }
        Expect.IsTrue(cache.Expired, $"It expires after {duration} ticks or more have elapsed.");
    }

    [Test]
    public void CacheExpiration_Time()
    {
        var originalProvider = VehicleMapUIRenderer.TimeProvider;
        try
        {
            var mockTime = 100f;
            // ReSharper disable once AccessToModifiedClosure
            VehicleMapUIRenderer.TimeProvider = () => mockTime;

            var cache = new VehicleMapUIRenderer.CachedMapTexture(null, false, mockTime);
            Expect.IsFalse(cache.Expired, "It is not expired immediately after creation.");

            const float duration = VehicleMapUIRenderer.CachedMapTexture.CacheDurationTime;
            mockTime += duration + 0.1f;
            
            Expect.IsTrue(cache.Expired, $"It expires after {duration:F1} seconds or more have elapsed.");
        }
        finally
        {
            // 他のテストに影響を与えないよう元のProviderに戻す
            VehicleMapUIRenderer.TimeProvider = originalProvider;
        }
    }

    [Test]
    public void GetRenderTextures()
    {
        using var group = VehicleGroup.CreateBasicVehicleGroup(new VehicleGroup.MockSettings
        {
            vehicleDef = DefDatabase<VehicleDef>.GetNamed("MV_Glypto"), drivers = 1
        });
        var vehiclePawnWithMap = (VehiclePawnWithMap)group.vehicle;
        var texture = VehicleMapUIRenderer.GetVehicleMapTexture(vehiclePawnWithMap, Rot4.North, new Vector2Int(128, 128));
        Expect.IsNotNull(texture, "GetVehicleMapTexture should return a non-null texture.");
        var texture2 = VehicleMapUIRenderer.GetVehicleMapTexture(vehiclePawnWithMap, Rot4.North, new Vector2Int(128, 128));
        Expect.AreEqual(texture2, texture, "Cache hit for GetVehicleMapTexture.");
        var texture3 = VehicleMapUIRenderer.GetVehicleMapTexture(vehiclePawnWithMap, Rot4.North, new Vector2Int(256, 256));
        Expect.AreNotEqual(texture3, texture, "GetRenderTextures should return a different texture for a different size.");

        var overlay = vehiclePawnWithMap.DrawTracker.overlayRenderer.Overlays.FirstOrDefault();
        Expect.IsNotNull(overlay, $"The vehicle ({vehiclePawnWithMap.VehicleDef.defName}) should have at least one overlay.");
        var texture4 = VehicleMapUIRenderer.GetOverlayWithVehicleMapTexture(vehiclePawnWithMap, overlay, Rot4.North,
            new Vector2Int(128, 128), vehiclePawnWithMap.VehicleMap.BoundsRect());
        Expect.IsNotNull(texture4, "GetOverlayWithVehicleMapTexture should return a non-null texture.");
    }
}