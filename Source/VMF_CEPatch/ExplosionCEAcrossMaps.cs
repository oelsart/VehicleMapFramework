﻿using CombatExtended;
using HarmonyLib;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using VehicleInteriors;
using Verse;

namespace VMF_CEPatch
{
    public class ExplosionCEAcrossMaps : ExplosionCE
    {
        public override IEnumerable<IntVec3> ExplosionCellsToHit
        {
            get
            {
                bool flag = base.Position.InBounds(base.Map) && base.Position.Roofed(base.Map);
                bool flag2 = height >= 2f;
                List<IntVec3> list = SimplePool<List<IntVec3>>.Get();
                list.Clear();
                List<IntVec3> list2 = SimplePool<List<IntVec3>>.Get();
                list2.Clear();
                int num = GenRadial.NumCellsInRadius(radius);
                for (int i = 0; i < num; i++)
                {
                    IntVec3 intVec = base.Position + GenRadial.RadialPattern[i];
                    if (!intVec.InBounds(base.Map))
                    {
                        continue;
                    }

                    if (flag2)
                    {
                        if ((!flag && GenSightOnVehicle.LineOfSight(base.Position, intVec, base.Map, skipFirstCell: false, null, 0, 0)) || !intVec.Roofed(base.Map))
                        {
                            list.Add(intVec);
                        }
                    }
                    else
                    {
                        if (!GenSightOnVehicle.LineOfSight(base.Position, intVec, base.Map, skipFirstCell: true))
                        {
                            continue;
                        }

                        if (needLOSToCell1.HasValue || needLOSToCell2.HasValue)
                        {
                            bool flag3 = needLOSToCell1.HasValue && GenSight.LineOfSight(needLOSToCell1.Value, intVec, base.Map, skipFirstCell: false, null, 0, 0);
                            bool flag4 = needLOSToCell2.HasValue && GenSight.LineOfSight(needLOSToCell2.Value, intVec, base.Map, skipFirstCell: false, null, 0, 0);
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
                    if (!item.Walkable(base.Map))
                    {
                        continue;
                    }

                    for (int j = 0; j < 4; j++)
                    {
                        IntVec3 intVec2 = item + GenAdj.CardinalDirections[j];
                        if (intVec2.InHorDistOf(base.Position, radius) && intVec2.InBounds(base.Map) && !intVec2.Standable(base.Map) && intVec2.GetEdifice(base.Map) != null && !list.Contains(intVec2) && list2.Contains(intVec2))
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
            var vehicles = base.Position.GetRoom(base.Map)?.ContainedThings<VehiclePawnWithMap>();
            if (vehicles.NullOrEmpty()) return;

            var map = base.Map;
            var pos = base.Position;
            try
            {
                //VehicleMapでは爆発のマージはしない
                foreach (var vehicle in vehicles)
                {
                    this.cellsToAffectOnVehicles[vehicle] = SimplePool<List<IntVec3>>.Get();
                    this.cellsToAffectOnVehicles[vehicle].Clear();
                    this.VirtualMapTransfer(vehicle.VehicleMap, pos.ToVehicleMapCoord(vehicle));
                    this.cellsToAffectOnVehicles[vehicle].AddRange(this.ExplosionCellsToHit);

                    if (applyDamageToExplosionCellsNeighbors)
                    {
                        AddCellsNeighbors(this, this.cellsToAffectOnVehicles[vehicle]);
                    }

                    damType.Worker.ExplosionStart(this, this.cellsToAffectOnVehicles[vehicle]);
                    this.cellsToAffectOnVehicles[vehicle].Sort((IntVec3 a, IntVec3 b) => ((int)GetCellAffectTick(this, b)).CompareTo(GetCellAffectTick(this, a)));
                    RegionTraverser.BreadthFirstTraverse(base.Position, base.Map, (Region from, Region to) => true, delegate (Region x)
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
            while (!this.toBeMerged && num >= 0 && ticksGame >= (int)GetCellAffectTick(this, cellsToAffect(this)[num]))
            {
                try
                {
                    AffectCell(this, cellsToAffect(this)[num]);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat(new object[]
                    {
                        "Explosion could not affect cell ",
                        cellsToAffect(this)[num],
                        ": ",
                        ex
                    }));
                }
                cellsToAffect(this).RemoveAt(num);
                num--;
            }
            var map = base.Map;
            var pos = base.Position;
            try
            {
                foreach (var vehicle in this.cellsToAffectOnVehicles.Keys)
                {
                    if (vehicle?.VehicleMap == null || !vehicle.Spawned) continue;

                    this.VirtualMapTransfer(vehicle.VehicleMap, pos.ToVehicleMapCoord(vehicle));
                    num = this.cellsToAffectOnVehicles[vehicle].Count - 1;
                    while (num >= 0 && ticksGame >= (int)GetCellAffectTick(this, this.cellsToAffectOnVehicles[vehicle][num]) && !vehicle.VehicleMap.Disposed)
                    {
                        try
                        {
                            AffectCell(this, this.cellsToAffectOnVehicles[vehicle][num]);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(string.Concat(new object[]
                            {
                        "Explosion could not affect cell ",
                        this.cellsToAffectOnVehicles[vehicle][num],
                        ": ",
                        ex
                            }));
                        }
                        this.cellsToAffectOnVehicles[vehicle].RemoveAt(num);
                        num--;
                    }
                }
            }
            finally
            {
                this.VirtualMapTransfer(map, pos);

                if (this.toBeMerged || (!cellsToAffect(this).Any<IntVec3>() && !this.cellsToAffectOnVehicles.Any(v => v.Value.Any<IntVec3>())))
                {
                    this.Destroy(DestroyMode.Vanish);
                }
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            this.cellsToAffectOnVehicles = SimplePool<Dictionary<VehiclePawnWithMap, List<IntVec3>>>.Get();
            this.cellsToAffectOnVehicles.Clear();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            for (var i = 0; i < this.cellsToAffectOnVehicles.Count; i++)
            {
                var key = this.cellsToAffectOnVehicles.ElementAt(i).Key;
                this.cellsToAffectOnVehicles[key].Clear();
                SimplePool<List<IntVec3>>.Return(this.cellsToAffectOnVehicles[key]);
                this.cellsToAffectOnVehicles[key] = null;
            }

            this.cellsToAffectOnVehicles.Clear();
            SimplePool<Dictionary<VehiclePawnWithMap, List<IntVec3>>>.Return(cellsToAffectOnVehicles);
            this.cellsToAffectOnVehicles = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_NestedCollections.Look(ref this.cellsToAffectOnVehicles, "cellsToAffectOnVehicles", LookMode.Reference, LookMode.Value);
        }

        private Dictionary<VehiclePawnWithMap, List<IntVec3>> cellsToAffectOnVehicles;

        private static readonly AccessTools.FieldRef<Explosion, List<IntVec3>> cellsToAffect = AccessTools.FieldRefAccess<Explosion, List<IntVec3>>("cellsToAffect");

        private static readonly FastInvokeHandler AddCellsNeighbors = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "AddCellsNeighbors"));

        private static readonly FastInvokeHandler AffectCell = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "AffectCell"));

        private static readonly FastInvokeHandler GetCellAffectTick = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "GetCellAffectTick"));
    }
}
