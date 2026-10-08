using System;
using System.Runtime.CompilerServices;
using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework;

public class VehicleMapParentsComponent : WorldComponent
{
  private static readonly MapParent_Vehicle[] cachedMapParentVehicle = new MapParent_Vehicle[128];
  private static sbyte[] uniqueIdToMapIndex = new sbyte[128];

  public VehicleMapParentsComponent(World world) : base(world)
  {
    Command_FocusVehicleMap.FocusLockedVehicle = null;
    Command_FocusVehicleMap.FocusedVehicle = null;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static MapParent_Vehicle GetCachedVehicle(Map map)
  {
    if (map is null) return null;

    var index = GetMapIndex(map);
    if (index >= 0 && index < cachedMapParentVehicle.Length)
    {
      return cachedMapParentVehicle[index];
    }
    return null;
  }

  public static void SetCache(Map map)
  {
    SetMapIndex(map);
    SetCachedVehicle(map);
  }

  private static void SetCachedVehicle(Map map)
  {
    if (MultiFloors.Active && MultiFloors.GroundMap(map) is { } groundMap)
    {
      map = groundMap;
    }

    var parent = map.Parent as MapParent_Vehicle;
    var index = GetMapIndex(map);
    if (index >= 0 && index < cachedMapParentVehicle.Length)
    {
      cachedMapParentVehicle[index] = parent;
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static int GetMapIndex(Map map)
  {
    var id = map.uniqueID;
    if (id >= 0 && id < uniqueIdToMapIndex.Length)
    {
      return uniqueIdToMapIndex[id];
    }
    return -1;
  }

  private static void SetMapIndex(Map map)
  {
    var id = map.uniqueID;
    if (id >= uniqueIdToMapIndex.Length)
    {
      var newSize = uniqueIdToMapIndex.Length;
      while (id >= newSize)
      {
        newSize *= 2;
      }
      Array.Resize(ref uniqueIdToMapIndex, newSize);
    }

    uniqueIdToMapIndex[id] = (sbyte)map.Index;
  }
  
  public override void FinalizeInit(bool fromLoad)
  {
    Clear();
  }

  public static void MapRemoved()
  {
    Clear();
    foreach (var map in Find.Maps)
    {
      SetCache(map);
    }
  }

  private static void Clear()
  {
    Array.Clear(cachedMapParentVehicle, 0, cachedMapParentVehicle.Length);
    for (var i = 0; i < uniqueIdToMapIndex.Length; i++)
    {
      uniqueIdToMapIndex[i] = -1;
    }
  }
}
