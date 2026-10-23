using DevTools.Testing;
using UnityEngine.Assertions;
using Vehicles.Tests;
using Verse;

namespace VehicleMapFramework.Test_DevTools;

[TestFixture(TestType.Playing)]
internal sealed class Test_CompZipline
{
  [Test]
  private void ZiplineEnd_CrossMapRegionLink()
  {
    var map = Find.CurrentMap;
    Assert.IsNotNull(map);

    var links = Find.World?.GetComponent<CrossMapRegionLinks>();
    Assert.IsNotNull(links, "CrossMapRegionLinks world component is null.");

    using var group = CreateBasicMapVehicleGroup(new VehicleGroup.MockSettings
    {
      size = new IntVec2(4, 8)
    });
    var mapVehicle = (VehiclePawnWithMap)group.vehicle;
    GenSpawn.Spawn(mapVehicle, FromRUCorner(map, 10), map);

    ZiplineEnd endA = null;
    ZiplineEnd endB = null;

    try
    {
      var cellA = map.Center;
      var cellB = mapVehicle.VehicleMap.Center;

      endA = MakeZiplineEnd();
      endB = MakeZiplineEnd();

      endB.launchVerb = new Verb_LaunchZipline
      {
        caster = endA
      };

      GenSpawn.Spawn(endA, cellA, map);
      GenSpawn.Spawn(endB, cellB, map);

      var regionA = endA.GetRegion();
      var regionB = endB.GetRegion();

      Assert.IsNotNull(regionA, $"Spawned zipline end A has no region. Cell: {cellA}");
      Assert.IsNotNull(regionB, $"Spawned zipline end B has no region. Cell: {cellB}");

      AssertContainsConnectedRegion(links, regionA, regionB);
      AssertContainsConnectedRegion(links, regionB, regionA);

      endB.Destroy();

      AssertDoesNotContainConnectedRegion(links, regionA, regionB);
      AssertDoesNotContainConnectedRegion(links, regionB, regionA);
    }
    finally
    {
      DestroyIfSpawned(endB);
      DestroyIfSpawned(endA);
    }
  }

  private static ZiplineEnd MakeZiplineEnd()
  {
    var thing = ThingMaker.MakeThing(VMF_DefOf.VMF_ZiplineEnd);
    if (thing is not ZiplineEnd ziplineEnd)
    {
      Test.Fail($"VMF_DefOf.ZiplineEnd produced unexpected thing type: {thing?.GetType().FullName ?? "null"}");
      return null;
    }

    return ziplineEnd;
  }

  private static void DestroyIfSpawned(Thing thing)
  {
    if (thing is { Spawned: true })
    {
      thing.Destroy();
    }
  }

  private static void AssertContainsConnectedRegion(CrossMapRegionLinks links, Region source, Region expected)
  {
    var connectedRegions = links.GetConnectedRegions(source);
    for (var i = 0; i < connectedRegions.Length; i++)
    {
      if (ReferenceEquals(connectedRegions[i], expected))
      {
        return;
      }
    }

    Test.Fail(
      $"Expected region {source.id} to be connected to region {expected.id}, " +
      $"but connected regions were [{FormatRegionIds(connectedRegions)}].");
  }

  private static void AssertDoesNotContainConnectedRegion(CrossMapRegionLinks links, Region source, Region unexpected)
  {
    var connectedRegions = links.GetConnectedRegions(source);
    for (var i = 0; i < connectedRegions.Length; i++)
    {
      if (ReferenceEquals(connectedRegions[i], unexpected))
      {
        Test.Fail(
          $"Expected region {source.id} not to be connected to region {unexpected.id}, " +
          $"but connected regions were [{FormatRegionIds(connectedRegions)}].");
      }
    }
  }

  private static string FormatRegionIds(ReadOnlySpan<Region> regions)
  {
    if (regions.Length == 0)
    {
      return string.Empty;
    }

    var ids = new int[regions.Length];
    for (var i = 0; i < regions.Length; i++)
    {
      ids[i] = regions[i]?.id ?? -1;
    }

    return string.Join(", ", ids);
  }
}