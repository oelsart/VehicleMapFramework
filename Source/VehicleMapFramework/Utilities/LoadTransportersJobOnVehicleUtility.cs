using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class LoadTransportersJobOnVehicleUtility
{
    public static ThingCount FindThingToLoad(Pawn p, CompTransporter transporter, bool gatherFromBaseMap)
    {
        LoadTransportersJobOnVehicleUtility.neededThings.Clear();
        List<TransferableOneWay> leftToLoad = transporter.leftToLoad;
        LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.Clear();
        if (leftToLoad != null)
        {
            IReadOnlyList<Pawn> allPawnsSpawned = transporter.Map.BaseMap().mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                if (allPawnsSpawned[i] != p && allPawnsSpawned[i].CurJobDef == VMF_DefOf.VMF_HaulToTransporterAcrossMaps)
                {
                    JobDriver_HaulToTransporterAcrossMaps jobDriver_HaulToTransporter = (JobDriver_HaulToTransporterAcrossMaps)allPawnsSpawned[i].jobs.curDriver;
                    if (jobDriver_HaulToTransporter.Container == transporter.parent)
                    {
                        TransferableOneWay transferableOneWay = TransferableUtility.TransferableMatchingDesperate(jobDriver_HaulToTransporter.ThingToCarry, leftToLoad, TransferAsOneMode.PodsOrCaravanPacking);
                        if (transferableOneWay != null)
                        {
                            if (LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.TryGetValue(transferableOneWay, out int num))
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
            return default;
        }
        Thing thing;
        if (gatherFromBaseMap)
        {
            thing = GenClosestCrossMap.ClosestThingReachable(p.Position, p.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.Touch, TraverseParms.For(p, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, x => LoadTransportersJobOnVehicleUtility.neededThings.Contains(x) && p.CanReserve(x, 1, -1, null, false) && !x.IsForbidden(p) && p.carryTracker.AvailableStackSpace(x.def) > 0, null, 0, -1, false, RegionType.Set_Passable, false, false, out _, out _);
        }
        else
        {
            TargetInfo exitSpot2 = TargetInfo.Invalid;
            TargetInfo enterSpot2 = TargetInfo.Invalid;
            thing = Patch_GenClosest_ClosestThingReachable.ClosestThingReachableOriginal(transporter.parent.Position, transporter.parent.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.Touch, TraverseParms.For(p, Danger.Deadly, TraverseMode.ByPawn, false, false, false), 9999f, x => LoadTransportersJobOnVehicleUtility.neededThings.Contains(x) && p.CanReserve(x, 1, -1, null, false) && !x.IsForbidden(p) && p.carryTracker.AvailableStackSpace(x.def) > 0 && p.CanReach(x, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, x.Map, out exitSpot2, out enterSpot2), null, 0, -1, false, RegionType.Set_Passable, false, false);
        }
        if (thing == null)
        {
            foreach (Thing thing2 in LoadTransportersJobOnVehicleUtility.neededThings)
            {
                if (thing2 is Pawn pawn && pawn.Spawned && ((!pawn.IsFreeColonist && !pawn.IsColonyMech) || pawn.Downed) && !pawn.inventory.UnloadEverything && p.CanReserveAndReach(thing.Map, pawn, PathEndMode.Touch, Danger.Deadly, 1, -1, null, false, out _, out _))
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
            if (!LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.TryGetValue(transferableOneWay3, out int num3))
            {
                num3 = 0;
            }
            LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.Clear();
            return new ThingCount(thing, Mathf.Min(transferableOneWay3.CountToTransfer - num3, thing.stackCount), false);
        }
        LoadTransportersJobOnVehicleUtility.tmpAlreadyLoading.Clear();
        return default;
    }

    public static Job JobOnTransporter(Pawn p, CompTransporter transporter)
    {
        _ = p;
        Job job = JobMaker.MakeJob(VMF_DefOf.VMF_HaulToTransporterAcrossMaps, LocalTargetInfo.Invalid, transporter.parent);
        job.ignoreForbidden = true;
        return job;
    }

    public static bool HasJobOnTransporter(Pawn pawn, CompTransporter transporter)
    {
        if (transporter.parent.IsForbidden(pawn))
        {
            return false;
        }

        if (!transporter.AnythingLeftToLoad)
        {
            return false;
        }

        if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
        {
            return false;
        }

        if (!pawn.CanReach(transporter.parent, PathEndMode.Touch, pawn.NormalMaxDanger(), false, false, TraverseMode.ByPawn, transporter.parent.Map, out _, out _))
        {
            return false;
        }

        if (LoadTransportersJobOnVehicleUtility.FindThingToLoad(pawn, transporter, transporter is not CompBuildableContainer container || container.GatherFromBaseMap).Thing == null)
        {
            return false;
        }

        return true;
    }

    private static readonly HashSet<Thing> neededThings = [];

    private static readonly Dictionary<TransferableOneWay, int> tmpAlreadyLoading = [];
}
