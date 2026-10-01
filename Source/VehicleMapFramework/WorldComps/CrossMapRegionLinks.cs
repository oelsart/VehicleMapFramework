using System;
using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework;

public class CrossMapRegionLinks(World world) : WorldComponent(world)
{
  private struct RegionRange
  {
    public int start;
    public int count;
  }

  private Region[] flatLinks = new Region[64];
  private int totalLinkCount;
  private RegionRange[] ranges = new RegionRange[64];

  public ReadOnlySpan<Region> GetConnectedRegions(Region region)
  {
    if (region == null) return ReadOnlySpan<Region>.Empty;

    var id = region.id;
    if ((uint)id >= (uint)ranges.Length) return ReadOnlySpan<Region>.Empty;

    var range = ranges[id];
    return range.count == 0 ? ReadOnlySpan<Region>.Empty : new ReadOnlySpan<Region>(flatLinks, range.start, range.count);
  }

  /// <summary>
  /// regionAとregionBの間にクロスマップリンクを登録
  /// </summary>
  public void AddLink(Region regionA, Region regionB)
  {
    if (regionA == null || regionB == null) return;

    AddLinkInternal(regionA, regionB);
    AddLinkInternal(regionB, regionA);
  }

  private void AddLinkInternal(Region source, Region target)
  {
    var id = source.id;
    EnsureRangeCapacity(id);

    ref var range = ref ranges[id];

    // 既に存在する範囲の後ろに追加
    var insertIndex = range.start + range.count;

    EnsureFlatLinkCapacity(totalLinkCount + 1);

    // 挿入位置より後ろの要素を1つずつ後ろにずらす
    if (insertIndex < totalLinkCount)
    {
      Array.Copy(flatLinks, insertIndex, flatLinks, insertIndex + 1, totalLinkCount - insertIndex);

      for (var i = 0; i < ranges.Length; i++)
      {
        if (ranges[i].count > 0 && ranges[i].start >= insertIndex)
        {
          ranges[i].start++;
        }
      }
    }

    flatLinks[insertIndex] = target;
    if (range.count == 0)
    {
      range.start = insertIndex;
    }

    range.count++;

    totalLinkCount++;
  }
  
  /// <summary>
  /// regionAとregionBの間のクロスマップリンクを削除
  /// </summary>
  public void RemoveLink(Region regionA, Region regionB)
  {
    if (regionA == null || regionB == null) return;

    RemoveLinkInternal(regionA, regionB);
    RemoveLinkInternal(regionB, regionA);
  }

  private void RemoveLinkInternal(Region source, Region target)
  {
    var sourceId = source.id;
    if ((uint)sourceId >= (uint)ranges.Length) return;

    ref var range = ref ranges[sourceId];
    if (range.count == 0) return;

    var removeIndex = -1;
    var end = range.start + range.count;
    for (var i = range.start; i < end; i++)
    {
      if (flatLinks[i].id == target.id)
      {
        removeIndex = i;
        break;
      }
    }

    // リンクが存在しない場合は何もしない
    if (removeIndex == -1) return;

    var elementsToShift = totalLinkCount - (removeIndex + 1);
    if (elementsToShift > 0)
    {
      Array.Copy(flatLinks, removeIndex + 1, flatLinks, removeIndex, elementsToShift);
    }

    // 末尾になった箇所をクリア（参照を解除してGC対象にする）
    flatLinks[totalLinkCount - 1] = null;

    for (var i = 0; i < ranges.Length; i++)
    {
      if (ranges[i].count > 0 && ranges[i].start > removeIndex)
      {
        ranges[i].start--;
      }
    }

    range.count--;
    if (range.count == 0)
    {
      range.start = 0;
    }

    totalLinkCount--;
  }

  private void EnsureRangeCapacity(int minId)
  {
    if (minId >= ranges.Length)
    {
      var newSize = Math.Max(ranges.Length * 2, minId + 1);
      Array.Resize(ref ranges, newSize);
    }
  }

  private void EnsureFlatLinkCapacity(int requiredSize)
  {
    if (requiredSize > flatLinks.Length)
    {
      var newSize = flatLinks.Length * 2;
      Array.Resize(ref flatLinks, newSize);
    }
  }
}