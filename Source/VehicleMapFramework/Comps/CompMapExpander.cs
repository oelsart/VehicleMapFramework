using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class CompMapExpander : ThingComp
{

  private static readonly List<IntVec3> tmpCells = new(8);

  public static bool debugDraw;

  private bool? cachedIsBridge;

  private bool? cachedIsOnlyBridge;
  private bool validCellsDirty;

  private bool[] ValidCells
  {
    get
    {
      if (validCellsDirty)
      {
        var adjacentCells = GenAdj.AdjacentCellsAround;
        for (var i = 0; i < 8; i++)
        {
          field[i] = false;
          var intVec = parent.Position + adjacentCells[i];
          if (ValidCell(intVec))
          {
            field[i] = true;
          }
        }
      }
      return field;
    }
  } = new bool[8];

  public bool IsOnlyBridge
  {
    get
    {
      if (!IsBridge) return false;

      cachedIsOnlyBridge ??= IsOnlyBridgeStatus();
      return cachedIsOnlyBridge.Value;

      bool IsOnlyBridgeStatus()
      {
        if (!parent.Spawned) return false;

        var validCells = ValidCells;
        tmpCells.Clear();
        for (var i = 0; i < 8; i++)
        {
          if (validCells[i])
          {
            tmpCells.Add(parent.Position + GenAdj.AdjacentCellsAround[i]);
          }
        }

        var result = true;
        var first = tmpCells.PopFront();
        parent.Map.floodFiller.FloodFill(first,
          c => ValidCell(c) && c != parent.Position,
          c =>
          {
            if (tmpCells.Contains(c))
            {
              tmpCells.Remove(c);
              if (tmpCells.Empty())
              {
                result = false;
                return true;
              }
            }
            return false;
          });
        return result;
      }
    }
  }

  public bool IsBridge
  {
    get
    {
      cachedIsBridge ??= IsBridgeStatus();
      return cachedIsBridge.Value;

      bool IsBridgeStatus()
      {
        if (!parent.Spawned) return false;

        var validCells = ValidCells;
        var validState = validCells[^1];
        var firstBlockFound = false;
        for (var i = 0; i < 8; i++)
        {
          if (validCells[i])
          {
            if (!validState)
            {
              if (firstBlockFound)
              {
                return true;
              }
              firstBlockFound = true;
              validState = true;
            }
          }
          else if (validState)
          {
            validState = false;
          }
        }
        return false;
      }
    }
  }

  private bool ValidCell(IntVec3 c)
  {
    return c.InBounds(parent.Map) && c.GetTerrain(parent.Map) != VMF_DefOf.VMF_ImpassableFloor;
  }

  public override void PostSpawnSetup(bool respawningAfterLoad)
  {
    if (respawningAfterLoad)
    {
      FrameDelay.DelayOne(Process, this);
      return;
    }
    Process(this);
    return;

    static void Process(CompMapExpander comp)
    {
      if (comp.parent.IsOnVehicleMapOf(out var vehicle))
      {
        foreach (var c in comp.parent.OccupiedRect())
        {
          comp.parent.Map.terrainGrid.SetTerrain(c, VMF_DefOf.VMF_VehicleFloor);
        }
        vehicle.MapExpanderComps.Add(comp);
        comp.DirtySelfAndAdjacentComps(comp.parent.Map);
        vehicle.impassableCellsDirty = true;
        vehicle.resizeRequest = true;
        CrossMapReachabilityCache.ClearCacheFor(vehicle.VehicleMap);
      }
    }
  }

  public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
  {
    var occupiedRect = parent.OccupiedRect();
    if (map.IsVehicleMapOf(out var vehicle))
    {
      foreach (var c in occupiedRect)
      {
        map.terrainGrid.SetTerrain(c, VMF_DefOf.VMF_ImpassableFloor);
      }
      vehicle.MapExpanderComps.Remove(this);
      if (IsBridge)
      {
        vehicle.MapExpanderComps.ForEach(c => c.cachedIsOnlyBridge = null);
      }
      DirtySelfAndAdjacentComps(map);
      vehicle.impassableCellsDirty = true;
      vehicle.resizeRequest = true;
      CrossMapReachabilityCache.ClearCacheFor(vehicle.VehicleMap);
    }
    
    foreach (var intVec in occupiedRect)
    {
      var thingList = map.thingGrid.ThingsListAtFast(intVec);
      for (var i = thingList.Count - 1; i >= 0; i--)
      {
        var thing = thingList[i];
        if (thing is Pawn) continue;

        if (thing.def.Minifiable)
        {
          thing.Uninstall();
        }
        else
        {
          thing.Destroy(DestroyMode.Deconstruct);
        }
      }
    }

  }

  private void DirtySelfAndAdjacentComps(Map map)
  {
    validCellsDirty = true;
    cachedIsBridge = null;
    cachedIsOnlyBridge = null;
    foreach (var intVec in GenAdj.CellsAdjacent8Way(parent).Where(c => c.InBounds(map)))
    {
      foreach (var thing in map.thingGrid.ThingsListAtFast(intVec))
      {
        if (!thing.TryGetComp<CompMapExpander>(out var comp))
          continue;
        comp.validCellsDirty = true;
        comp.cachedIsBridge = null;
        comp.cachedIsOnlyBridge = null;
        break;
      }
    }
  }

  public static void DebugDraw(List<CompMapExpander> comps)
  {
    if (!debugDraw || !VehicleMapUtility.FocusedOnVehicleMap(out var vehicle))
      return;
    var quat = vehicle.FullAngleQuat;
    foreach (var comp in comps)
    {
      if (!comp.IsBridge)
        continue;
      var mat = DebugMatsSpectrum.Mat(comp.IsOnlyBridge ? 10 : 30, true);
      var vector = comp.parent.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays).ToBaseMapCoord();
      Graphics.DrawMesh(MeshPool.plane10, vector, quat, mat, 0);
    }
  }
}
