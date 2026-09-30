using System;
using JetBrains.Annotations;
using Verse;

namespace VehicleMapFramework;

[MustDisposeResource]
internal readonly ref struct FocusMapScope
{
  private readonly sbyte tmpMapIndex = -1;
  
  public bool IsValid => tmpMapIndex != -1;

  private FocusMapScope(Map map)
  {
    if (map is null) throw new ArgumentNullException(nameof(map));
    tmpMapIndex = Current.Game.currentMapIndex;
    Current.Game.currentMapIndex = (sbyte)map.Index;
  }
  
  [MustDisposeResource]
  public static FocusMapScope FocusMapUnsafe(Map map)
  {
    return new FocusMapScope(map);
  }
  
  public void Dispose()
  {
    if (!IsValid) return;
    Current.Game.currentMapIndex = tmpMapIndex;
  }
}