using HarmonyLib;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleInteriors
{
    public class ExplosionAcrossMaps : Explosion
    {
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            foreach(var pair in this.cellsToAffectOnVehicles)
            {
                SimplePool<List<IntVec3>>.Return(pair.Value);
            }
        }

        public override void StartExplosion(SoundDef explosionSound, List<Thing> ignoredThings)
        {
            base.StartExplosion(explosionSound, ignoredThings);

            var vehicles = base.Position.GetRoom(base.Map).ContainedThings<VehiclePawnWithMap>();

            var map = base.Map;
            var pos = base.Position;
            try
            {
                foreach (var vehicle in vehicles)
                {
                    this.cellsToAffectOnVehicles[vehicle] = SimplePool<List<IntVec3>>.Get();
                    this.VirtualMapTransfer(vehicle.VehicleMap, pos.ToVehicleMapCoord(vehicle));
                    if (!base.overrideCells.NullOrEmpty())
                    {
                        foreach (var c in base.overrideCells)
                        {
                            this.cellsToAffectOnVehicles[vehicle].Add(c.ToVehicleMapCoord(vehicle));
                        }
                    }
                    else
                    {
                        this.cellsToAffectOnVehicles[vehicle].AddRange(damType.Worker.ExplosionCellsToHit(this));
                    }

                    if (applyDamageToExplosionCellsNeighbors)
                    {
                        AddCellsNeighbors(this, this.cellsToAffectOnVehicles[vehicle]);
                    }

                    vehicle.VehicleMap.listerThings.AllThings.ForEach(t => t.Notify_Explosion(this));
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
            while (num >= 0 && ticksGame >= (int)GetCellAffectTick(this, cellsToAffect(this)[num]))
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
                    if (vehicle?.VehicleMap?.Disposed ?? true) continue; //既にVehicleが破壊された時など

                    this.VirtualMapTransfer(vehicle.VehicleMap, pos.ToVehicleMapCoord(vehicle));
                    num = this.cellsToAffectOnVehicles[vehicle].Count - 1;
                    while (num >= 0 && ticksGame >= (int)GetCellAffectTick(this, this.cellsToAffectOnVehicles[vehicle][num]))
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

                if (!cellsToAffect(this).Any<IntVec3>() && !this.cellsToAffectOnVehicles.Any(v => v.Value.Any<IntVec3>()))
                {
                    this.Destroy(DestroyMode.Vanish);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_NestedCollections.Look(ref this.cellsToAffectOnVehicles, "cellsToAffectOnVehicles", LookMode.Reference, LookMode.Value);
        }

        private Dictionary<VehiclePawnWithMap, List<IntVec3>> cellsToAffectOnVehicles = new Dictionary<VehiclePawnWithMap, List<IntVec3>>();

        private static readonly AccessTools.FieldRef<Explosion, List<IntVec3>> cellsToAffect = AccessTools.FieldRefAccess<Explosion, List<IntVec3>>("cellsToAffect");

        private static readonly FastInvokeHandler AddCellsNeighbors = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "AddCellsNeighbors"));

        private static readonly FastInvokeHandler AffectCell = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "AffectCell"));

        private static readonly FastInvokeHandler GetCellAffectTick = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "GetCellAffectTick"));
    }
}
