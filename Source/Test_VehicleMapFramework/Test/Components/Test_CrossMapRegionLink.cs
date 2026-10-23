using System.Runtime.CompilerServices;
using DevTools.Testing;
using Verse;

namespace VehicleMapFramework.Test_DevTools;

[TestFixture(TestType.MainMenu)]
internal sealed class Test_CrossMapRegionLink
{
  [Test]
  private void AddLink_ConnectsRegionsBidirectionally()
  {
    var links = CreateLinks();
    var regionA = CreateRegion(1);
    var regionB = CreateRegion(2);

    links.AddLink(regionA, regionB);

    AssertConnectedIds(links, regionA, 2);
    AssertConnectedIds(links, regionB, 1);
  }

  [Test]
  private void AddLink_IgnoresNullRegions()
  {
    var links = CreateLinks();
    var region = CreateRegion(1);

    links.AddLink(null, region);
    links.AddLink(region, null);
    links.AddLink(null, null);

    AssertConnectedIds(links, region);
  }

  [Test]
  private void RemoveLink_RemovesRegionsBidirectionally()
  {
    var links = CreateLinks();
    var regionA = CreateRegion(1);
    var regionB = CreateRegion(2);

    links.AddLink(regionA, regionB);
    links.RemoveLink(regionA, regionB);

    AssertConnectedIds(links, regionA);
    AssertConnectedIds(links, regionB);
  }

  [Test]
  private void RemoveLink_IgnoresMissingOrNullRegions()
  {
    var links = CreateLinks();
    var regionA = CreateRegion(1);
    var regionB = CreateRegion(2);
    var regionC = CreateRegion(3);

    links.AddLink(regionA, regionB);

    links.RemoveLink(regionA, regionC);
    links.RemoveLink(null, regionB);
    links.RemoveLink(regionA, null);
    links.RemoveLink(null, null);

    AssertConnectedIds(links, regionA, 2);
    AssertConnectedIds(links, regionB, 1);
    AssertConnectedIds(links, regionC);
  }

  [Test]
  private void AddLink_ShiftsExistingRangesWhenInsertedBeforeThem()
  {
    var links = CreateLinks();
    var lowA = CreateRegion(1);
    var lowB = CreateRegion(2);
    var highA = CreateRegion(10);
    var highB = CreateRegion(11);

    links.AddLink(highA, highB);
    links.AddLink(lowA, lowB);

    AssertConnectedIds(links, lowA, 2);
    AssertConnectedIds(links, lowB, 1);
    AssertConnectedIds(links, highA, 11);
    AssertConnectedIds(links, highB, 10);
  }

  [Test]
  private void RemoveLink_ShiftsFollowingRangesAfterRemoval()
  {
    var links = CreateLinks();
    var lowA = CreateRegion(1);
    var lowB = CreateRegion(2);
    var highA = CreateRegion(10);
    var highB = CreateRegion(11);

    links.AddLink(lowA, lowB);
    links.AddLink(highA, highB);

    links.RemoveLink(lowA, lowB);

    AssertConnectedIds(links, lowA);
    AssertConnectedIds(links, lowB);
    AssertConnectedIds(links, highA, 11);
    AssertConnectedIds(links, highB, 10);
  }

  [Test]
  private void AddLink_ExpandsRangeStorageForLargeRegionIds()
  {
    var links = CreateLinks();
    var regionA = CreateRegion(128);
    var regionB = CreateRegion(129);

    links.AddLink(regionA, regionB);

    AssertConnectedIds(links, regionA, 129);
    AssertConnectedIds(links, regionB, 128);
  }

  [Test]
  private void AddLink_ExpandsFlatLinkStorageForManyLinks()
  {
    var links = CreateLinks();
    var source = CreateRegion(1);

    var expectedIds = new List<int>();
    for (var i = 0; i < 80; i++)
    {
      var target = CreateRegion(100 + i);
      links.AddLink(source, target);
      expectedIds.Add(target.id);
    }

    AssertConnectedIds(links, source, [.. expectedIds]);
  }

  private static CrossMapRegionLinks CreateLinks()
  {
    return new CrossMapRegionLinks(null);
  }

  private static Region CreateRegion(int id)
  {
    var region = (Region)RuntimeHelpers.GetUninitializedObject(typeof(Region));
    region.id = id;
    return region;
  }

  private static void AssertConnectedIds(CrossMapRegionLinks links, Region source, params int[] expectedIds)
  {
    var actual = links.GetConnectedRegions(source);
    Expect.AreEqual(expectedIds.Length, actual.Length,
      $"Expected {expectedIds.Length} connected regions for region {source.id}, but got {actual.Length}. " +
      $"Expected: [{string.Join(", ", expectedIds)}], Actual: [{FormatIds(actual)}]");

    for (var i = 0; i < expectedIds.Length; i++)
    {
      Expect.AreEqual(expectedIds[i], actual[i]?.id,
        $"Connected region mismatch at index {i} for region {source.id}. " +
        $"Expected: [{string.Join(", ", expectedIds)}], Actual: [{FormatIds(actual)}]");
    }
  }

  private static string FormatIds(ReadOnlySpan<Region> regions)
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