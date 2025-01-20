using CombatExtended;
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
        public void StartExplosionCEOnVehicle(SoundDef explosionSound, List<Thing> ignoredThings)
        {
            var vehicles = base.Position.GetRoom(base.Map).ContainedThings<VehiclePawnWithMap>();

            var map = base.Map;
            var pos = base.Position;
            try
            {
                //VehicleMapでは爆発のマージはしない
                foreach (var vehicle in vehicles)
                {
                    this.cellsToAffectOnVehicles[vehicle] = SimplePool<List<IntVec3>>.Get();
                    this.VirtualMapTransfer(vehicle.VehicleMap, pos.VehicleMapToOrig(vehicle));
                    this.cellsToAffectOnVehicles[vehicle].AddRange(base.ExplosionCellsToHit);

                    if (applyDamageToExplosionCellsNeighbors)
                    {
                        AddCellsNeighbors(this, this.cellsToAffectOnVehicles[vehicle]);
                    }

                    damType.Worker.ExplosionStart(this, this.cellsToAffectOnVehicles[vehicle]);
                    this.cellsToAffectOnVehicles[vehicle].Sort((IntVec3 a, IntVec3 b) => ((int)GetCellAffectTick(b)).CompareTo(GetCellAffectTick(a)));
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
            while (!this.toBeMerged && num >= 0 && ticksGame >= (int)GetCellAffectTick(cellsToAffect(this)[num]))
            {
                try
                {
                    AffectCell(cellsToAffect(this)[num]);
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
                    this.VirtualMapTransfer(vehicle.VehicleMap, pos.VehicleMapToOrig(vehicle));
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

                if (this.toBeMerged || (!cellsToAffect(this).Any<IntVec3>() && !this.cellsToAffectOnVehicles.Any(v => v.Value.Any<IntVec3>())))
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
