using CombatExtended;
using HarmonyLib;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleMapFramework;

public class ExplosionCEAcrossMaps : ExplosionCE
{
    public override IEnumerable<IntVec3> ExplosionCellsToHit
    {
        get
        {
            bool flag = Position.InBounds(Map) && Position.Roofed(Map);
            bool flag2 = height >= 2f;
            List<IntVec3> list = SimplePool<List<IntVec3>>.Get();
            list.Clear();
            List<IntVec3> list2 = SimplePool<List<IntVec3>>.Get();
            list2.Clear();
            int num = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < num; i++)
            {
                IntVec3 intVec = Position + GenRadial.RadialPattern[i];
                if (!intVec.InBounds(Map))
                {
                    continue;
                }

                if (flag2)
                {
                    if ((!flag && GenSightOnVehicle.LineOfSight(Position, intVec, Map, skipFirstCell: false, null, 0, 0)) || !intVec.Roofed(Map))
                    {
                        list.Add(intVec);
                    }
                }
                else
                {
                    if (!GenSightOnVehicle.LineOfSight(Position, intVec, Map, skipFirstCell: true))
                    {
                        continue;
                    }

                    if (needLOSToCell1.HasValue || needLOSToCell2.HasValue)
                    {
                        bool flag3 = needLOSToCell1.HasValue && GenSight.LineOfSight(needLOSToCell1.Value, intVec, Map, skipFirstCell: false, null, 0, 0);
                        bool flag4 = needLOSToCell2.HasValue && GenSight.LineOfSight(needLOSToCell2.Value, intVec, Map, skipFirstCell: false, null, 0, 0);
                        if (!flag3 && !flag4)
                        {
                            continue;
                        }
                    }

                    list.Add(intVec);
                }
            }

            foreach (IntVec3 item in list)
            {
                if (!item.Walkable(Map))
                {
                    continue;
                }

                for (int j = 0; j < 4; j++)
                {
                    IntVec3 intVec2 = item + GenAdj.CardinalDirections[j];
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
        var vehicles = Position.GetRoom(Map)?.ContainedThings<VehiclePawnWithMap>();
        if (vehicles.NullOrEmpty()) return;

        var map = Map;
        var pos = Position;
        try
        {
            //VehicleMapでは爆発のマージはしない
            foreach (var vehicle in vehicles)
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
                RegionTraverser.BreadthFirstTraverse(Position, Map, (from, to) => true, delegate (Region x)
                {
                    List<Thing> list = x.ListerThings.ThingsInGroup(ThingRequestGroup.Pawn);
                    for (int num2 = list.Count - 1; num2 >= 0; num2--)
                    {
                        ((Pawn)list[num2]).mindState.Notify_Explosion(this);
                    }

                    return false;
                }, 25);
            }
        }
        finally
        {
            this.VirtualMapTransfer(map, pos);
        }
    }

    public override void Tick()
    {
        int ticksGame = Find.TickManager.TicksGame;
        int num = cellsToAffect(this).Count - 1;
        while (!toBeMerged && num >= 0 && ticksGame >= (int)GetCellAffectTick(this, cellsToAffect(this)[num]))
        {
            try
            {
                AffectCell(this, cellsToAffect(this)[num]);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat(
                [
                    "Explosion could not affect cell ",
                    cellsToAffect(this)[num],
                    ": ",
                    ex
                ]));
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
                        Log.Error(string.Concat(
                        [
                    "Explosion could not affect cell ",
                    cellsToAffectOnVehicles[vehicle][num],
                    ": ",
                    ex
                        ]));
                    }
                    cellsToAffectOnVehicles[vehicle].RemoveAt(num);
                    num--;
                }
            }
        }
        finally
        {
            this.VirtualMapTransfer(map, pos);

            if (toBeMerged || (!cellsToAffect(this).Any() && !cellsToAffectOnVehicles.Any(v => v.Value.Any())))
            {
                Destroy(DestroyMode.Vanish);
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

    private Dictionary<VehiclePawnWithMap, List<IntVec3>> cellsToAffectOnVehicles;

    private static readonly AccessTools.FieldRef<Explosion, List<IntVec3>> cellsToAffect = AccessTools.FieldRefAccess<Explosion, List<IntVec3>>("cellsToAffect");

    private static readonly FastInvokeHandler AddCellsNeighbors = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "AddCellsNeighbors"));

    private static readonly FastInvokeHandler AffectCell = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "AffectCell"));

    private static readonly FastInvokeHandler GetCellAffectTick = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "GetCellAffectTick"));
}
