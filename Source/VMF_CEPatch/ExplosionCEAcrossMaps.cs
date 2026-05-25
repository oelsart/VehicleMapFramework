using System;
using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using HarmonyLib;
using SmashTools;
using Verse;

namespace VehicleMapFramework;

[Obsolete("Changed to the patch for vanilla Explosion.")]
public class ExplosionCEAcrossMaps : ExplosionCE
{

  private static readonly AccessTools.FieldRef<Explosion, List<IntVec3>> cellsToAffect = AccessTools.FieldRefAccess<Explosion, List<IntVec3>>("cellsToAffect");

  private static readonly FastInvokeHandler AddCellsNeighbors = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "AddCellsNeighbors"));

  private static readonly FastInvokeHandler AffectCell = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "AffectCell"));

  private static readonly FastInvokeHandler GetCellAffectTick = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "GetCellAffectTick"));

  private Dictionary<VehiclePawnWithMap, List<IntVec3>> cellsToAffectOnVehicles;

  public override IEnumerable<IntVec3> ExplosionCellsToHit
  {
    get
    {
      var flag = Position.InBounds(Map) && Position.Roofed(Map);
      var flag2 = height >= 2f;
      var list = SimplePool<List<IntVec3>>.Get();
      list.Clear();
      var list2 = SimplePool<List<IntVec3>>.Get();
      list2.Clear();
      var num = GenRadial.NumCellsInRadius(radius);
      for (var i = 0; i < num; i++)
      {
        var intVec = Position + GenRadial.RadialPattern[i];
        if (!intVec.InBounds(Map))
        {
          continue;
        }

        if (flag2)
        {
          if (!flag && GenSightOnVehicle.LineOfSight(Position, intVec, Map, false) || !intVec.Roofed(Map))
          {
            list.Add(intVec);
          }
        }
        else
        {
          if (!GenSightOnVehicle.LineOfSight(Position, intVec, Map, true))
          {
            continue;
          }

          if (needLOSToCell1.HasValue || needLOSToCell2.HasValue)
          {
            var flag3 = needLOSToCell1.HasValue && GenSight.LineOfSight(needLOSToCell1.Value, intVec, Map, false);
            var flag4 = needLOSToCell2.HasValue && GenSight.LineOfSight(needLOSToCell2.Value, intVec, Map, false);
            if (!flag3 && !flag4)
            {
              continue;
            }
          }

          list.Add(intVec);
        }
      }

      foreach (var item in list)
      {
        if (!item.Walkable(Map))
        {
          continue;
        }

        for (var j = 0; j < 4; j++)
        {
          var intVec2 = item + GenAdj.CardinalDirections[j];
          if (intVec2.InHorDistOf(Position, radius) && intVec2.InBounds(Map) && !intVec2.Standable(Map) && intVec2.GetEdifice(Map) != null && !list.Contains(intVec2) && list2.Contains(intVec2))
          {
            list2.Add(intVec2);
          }
        }
      }

      var result = list.Concat(list2).ToArray();
      list.Clear();
      list2.Clear();
      SimplePool<List<IntVec3>>.Return(list);
      SimplePool<List<IntVec3>>.Return(list2);
      return result;
    }
  }

  public void StartExplosionCEOnVehicle()
  {
    var vehicles = Position.GetRoom(Map)?.ContainedThings<VehiclePawnWithMap>().ToList();
    if (vehicles.NullOrEmpty()) return;

    var map = Map;
    var pos = Position;
    try
    {
      //VehicleMapでは爆発のマージはしない
      foreach (var vehicle in vehicles!)
      {
        cellsToAffectOnVehicles[vehicle] = SimplePool<List<IntVec3>>.Get();
        cellsToAffectOnVehicles[vehicle].Clear();
        this.VirtualMapTransfer(vehicle.VehicleMap, pos.ToVehicleMapCoord(vehicle));
        cellsToAffectOnVehicles[vehicle].AddRange(ExplosionCellsToHit);

        if (applyDamageToExplosionCellsNeighbors)
        {
          AddCellsNeighbors(this, cellsToAffectOnVehicles[vehicle]);
        }

        damType.Worker.ExplosionStart(this, cellsToAffectOnVehicles[vehicle]);
        cellsToAffectOnVehicles[vehicle].Sort((a, b) => ((int)GetCellAffectTick(this, b)).CompareTo(GetCellAffectTick(this, a)));
        RegionTraverser.BreadthFirstTraverse(Position,
          Map,
          (_, _) => true,
          delegate(Region x)
          {
            var list = x.ListerThings.ThingsInGroup(ThingRequestGroup.Pawn);
            for (var num2 = list.Count - 1; num2 >= 0; num2--)
            {
              ((Pawn)list[num2]).mindState.Notify_Explosion(this);
            }

            return false;
          },
          25);
      }
    }
    finally
    {
      this.VirtualMapTransfer(map, pos);
    }
  }

  public override void Tick()
  {
    var ticksGame = Find.TickManager.TicksGame;
    var num = cellsToAffect(this).Count - 1;
    while (!toBeMerged && num >= 0 && ticksGame >= (int)GetCellAffectTick(this, cellsToAffect(this)[num]))
    {
      try
      {
        AffectCell(this, cellsToAffect(this)[num]);
      }
      catch (Exception ex)
      {
        Log.Error(string.Concat("Explosion could not affect cell ", cellsToAffect(this)[num], ": ", ex));
      }
      cellsToAffect(this).RemoveAt(num);
      num--;
    }
    var map = Map;
    var pos = Position;
    try
    {
      foreach (var vehicle in cellsToAffectOnVehicles.Keys)
      {
        if (vehicle?.VehicleMap == null || !vehicle.Spawned) continue;

        this.VirtualMapTransfer(vehicle.VehicleMap, pos.ToVehicleMapCoord(vehicle));
        num = cellsToAffectOnVehicles[vehicle].Count - 1;
        while (num >= 0 && ticksGame >= (int)GetCellAffectTick(this, cellsToAffectOnVehicles[vehicle][num]) && !vehicle.VehicleMap.Disposed)
        {
          try
          {
            AffectCell(this, cellsToAffectOnVehicles[vehicle][num]);
          }
          catch (Exception ex)
          {
            Log.Error(string.Concat("Explosion could not affect cell ", cellsToAffectOnVehicles[vehicle][num], ": ", ex));
          }
          cellsToAffectOnVehicles[vehicle].RemoveAt(num);
          num--;
        }
      }
    }
    finally
    {
      this.VirtualMapTransfer(map, pos);

      if (toBeMerged || !cellsToAffect(this).Any() && !cellsToAffectOnVehicles.Any(v => v.Value.Any()))
      {
        Destroy();
      }
    }
  }

  public override void SpawnSetup(Map map, bool respawningAfterLoad)
  {
    base.SpawnSetup(map, respawningAfterLoad);
    cellsToAffectOnVehicles = SimplePool<Dictionary<VehiclePawnWithMap, List<IntVec3>>>.Get();
    cellsToAffectOnVehicles.Clear();
  }

  public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
  {
    base.DeSpawn(mode);
    for (var i = 0; i < cellsToAffectOnVehicles.Count; i++)
    {
      var key = cellsToAffectOnVehicles.ElementAt(i).Key;
      cellsToAffectOnVehicles[key].Clear();
      SimplePool<List<IntVec3>>.Return(cellsToAffectOnVehicles[key]);
      cellsToAffectOnVehicles[key] = null;
    }

    cellsToAffectOnVehicles.Clear();
    SimplePool<Dictionary<VehiclePawnWithMap, List<IntVec3>>>.Return(cellsToAffectOnVehicles);
    cellsToAffectOnVehicles = null;
  }

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_NestedCollections.Look(ref cellsToAffectOnVehicles, "cellsToAffectOnVehicles", LookMode.Reference, LookMode.Value);
  }
}
