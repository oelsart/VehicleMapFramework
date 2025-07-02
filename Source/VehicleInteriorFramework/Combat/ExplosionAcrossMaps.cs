using HarmonyLib;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VehicleInteriors;

public class ExplosionAcrossMaps : Explosion
{
    public override void StartExplosion(SoundDef explosionSound, List<Thing> ignoredThings)
    {
        base.StartExplosion(explosionSound, ignoredThings);

        var vehicles = base.Position.GetRoom(base.Map)?.ContainedThings<VehiclePawnWithMap>();
        if (vehicles.NullOrEmpty()) return;

        var map = base.Map;
        var pos = base.Position;
        try
        {
            foreach (var vehicle in vehicles)
            {
                cellsToAffectOnVehicles[vehicle] = SimplePool<List<IntVec3>>.Get();
                cellsToAffectOnVehicles[vehicle].Clear();
                this.VirtualMapTransfer(vehicle.VehicleMap, pos.ToVehicleMapCoord(vehicle));
                if (!base.overrideCells.NullOrEmpty())
                {
                    foreach (var c in base.overrideCells)
                    {
                        cellsToAffectOnVehicles[vehicle].Add(c.ToVehicleMapCoord(vehicle));
                    }
                }
                else
                {
                    cellsToAffectOnVehicles[vehicle].AddRange(damType.Worker.ExplosionCellsToHit(this));
                }

                if (applyDamageToExplosionCellsNeighbors)
                {
                    AddCellsNeighbors(this, cellsToAffectOnVehicles[vehicle]);
                }

                vehicle.VehicleMap.listerThings.AllThings.ForEach(t => t.Notify_Explosion(this));
            }
        }
        finally
        {
            this.VirtualMapTransfer(map, pos);
        }
    }

    protected override void Tick()
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

        var map = base.Map;
        var pos = base.Position;
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

            if (!cellsToAffect(this).Any<IntVec3>() && !cellsToAffectOnVehicles.Any(v => v.Value.Any<IntVec3>()))
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

    private Dictionary<VehiclePawnWithMap, List<IntVec3>> cellsToAffectOnVehicles = [];

    private static readonly AccessTools.FieldRef<Explosion, List<IntVec3>> cellsToAffect = AccessTools.FieldRefAccess<Explosion, List<IntVec3>>("cellsToAffect");

    private static readonly FastInvokeHandler AddCellsNeighbors = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "AddCellsNeighbors"));

    private static readonly FastInvokeHandler AffectCell = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "AffectCell"));

    private static readonly FastInvokeHandler GetCellAffectTick = MethodInvoker.GetHandler(AccessTools.Method(typeof(Explosion), "GetCellAffectTick"));
}
