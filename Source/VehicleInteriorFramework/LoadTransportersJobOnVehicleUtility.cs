using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class LoadTransportersJobOnVehicleUtility
    {
        public static ThingCount FindThingToLoad(Pawn p, CompTransporter transporter, out TargetInfo exitSpot, out TargetInfo enterSpot)
        {
            LoadTransportersJobOnVehicleUtility.neededThings.Clear();
            List<TransferableOneWay> leftToLoad = transporter.leftToLoad;
            LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.Clear();
            if (leftToLoad != null)
            {
                IReadOnlyList<Pawn> allPawnsSpawned = transporter.Map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < allPawnsSpawned.Count; i++)
                {
                    if (allPawnsSpawned[i] != p && allPawnsSpawned[i].CurJobDef == JobDefOf.HaulToTransporter)
                    {
                        JobDriver_HaulToTransporter jobDriver_HaulToTransporter = (JobDriver_HaulToTransporter)allPawnsSpawned[i].jobs.curDriver;
                        if (jobDriver_HaulToTransporter.Container == transporter.parent)
                        {
                            TransferableOneWay transferableOneWay = TransferableUtility.TransferableMatchingDesperate(jobDriver_HaulToTransporter.ThingToCarry, leftToLoad, TransferAsOneMode.PodsOrCaravanPacking);
                            if (transferableOneWay != null)
                            {
                                int num = 0;
                                if (LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.TryGetValue(transferableOneWay, out num))
                                {
                                    LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading[transferableOneWay] = num + jobDriver_HaulToTransporter.initialCount;
                                }
                                else
                                {
                                    LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.Add(transferableOneWay, jobDriver_HaulToTransporter.initialCount);
                                }
                            }
                        }
                    }
                }
                for (int j = 0; j < leftToLoad.Count; j++)
                {
                    TransferableOneWay transferableOneWay2 = leftToLoad[j];
                    if (!LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.TryGetValue(leftToLoad[j], out int num2))
                    {
                        num2 = 0;
                    }
                    if (transferableOneWay2.CountToTransfer - num2 > 0)
                    {
                        for (int k = 0; k < transferableOneWay2.things.Count; k++)
                        {
                            LoadTransportersJobOnVehicleUtility.neededThings.Add(transferableOneWay2.things[k]);
                        }
                    }
                }
            }
            if (!LoadTransportersJobOnVehicleUtility.neededThings.Any<Thing>())
            {
                LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.Clear();
                exitSpot = null;
                enterSpot = null;
                return default;
            }
            Thing thing = GenClosestOnVehicle.ClosestThingReachable(p.Position, p.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.Touch, TraverseParms.For(p, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, (Thing x) => LoadTransportersJobOnVehicleUtility.neededThings.Contains(x) && p.CanReserve(x, 1, -1, null, false) && !x.IsForbidden(p) && p.carryTracker.AvailableStackSpace(x.def) > 0, null, 0, -1, false, RegionType.Set_Passable, false, false, out exitSpot, out enterSpot);
            if (thing == null)
            {
                foreach (Thing thing2 in LoadTransportersJobOnVehicleUtility.neededThings)
                {
                    if (thing2 is Pawn pawn && pawn.Spawned && ((!pawn.IsFreeColonist && !pawn.IsColonyMech) || pawn.Downed) && !pawn.inventory.UnloadEverything && p.CanReserveAndReach(thing.Map, pawn, PathEndMode.Touch, Danger.Deadly, 1, -1, null, false, out exitSpot, out enterSpot))
                    {
                        LoadTransportersJobOnVehicleUtility.neededThings.Clear();
                        LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.Clear();
                        return new ThingCount(pawn, 1, false);
                    }
                }
            }
            LoadTransportersJobOnVehicleUtility.neededThings.Clear();
            if (thing != null)
            {
                TransferableOneWay transferableOneWay3 = null;
                for (int l = 0; l < leftToLoad.Count; l++)
                {
                    if (leftToLoad[l].things.Contains(thing))
                    {
                        transferableOneWay3 = leftToLoad[l];
                        break;
                    }
                }
                int num3;
                if (!LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.TryGetValue(transferableOneWay3, out num3))
                {
                    num3 = 0;
                }
                LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.Clear();
                return new ThingCount(thing, Mathf.Min(transferableOneWay3.CountToTransfer - num3, thing.stackCount), false);
            }
            LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.Clear();
            return default(ThingCount);
        }

        private static readonly HashSet<Thing> neededThings = new HashSet<Thing>();

        private static readonly Dictionary<TransferableOneWay, int> tmpAlreadyLoading = new Dictionary<TransferableOneWay, int>();
    }
}
