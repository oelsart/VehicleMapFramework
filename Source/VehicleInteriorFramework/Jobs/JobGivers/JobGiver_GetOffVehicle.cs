﻿using RimWorld;
using System.Linq;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class JobGiver_GetOffVehicle : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn) => 0f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            var guest = pawn.Faction != Faction.OfPlayer;
            if (pawn.IsOnVehicleMapOf(out var vehicle) && vehicle.Spawned && (vehicle.AutoGetOff || guest))
            {
                var exitSpots = vehicle.interiorMap.listerBuildings.allBuildingsColonist.Where(b => b.HasComp<CompVehicleEnterSpot>());
                var hostile = pawn.HostileTo(Faction.OfPlayer);
                TargetInfo spot = exitSpots.FirstOrDefault(e =>
                {
                    var baseMap = e.BaseMap();
                    var basePos = e.PositionOnBaseMap();
                    var faceCell = e.BaseFullRotationOfThing().FacingCell;
                    faceCell.y = 0;
                    var dist = 1;
                    while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                    {
                        dist++;
                    }
                    var cell = (basePos - faceCell * dist);
                    return pawn.CanReach(e, PathEndMode.OnCell, Danger.Deadly, hostile, hostile, TraverseMode.ByPawn) && cell.Walkable(baseMap);
                });
                if (!spot.IsValid)
                {
                    spot = new TargetInfo(CellRect.WholeMap(vehicle.interiorMap).EdgeCells.OrderBy(c => (pawn.Position - c).LengthHorizontalSquared).FirstOrDefault(c =>
                    {
                        var baseMap = vehicle.Map;
                        var basePos = c.OrigToVehicleMap(vehicle);
                        var faceCell = c.BaseFullDirectionToInsideMap(vehicle.interiorMap).FacingCell;
                        faceCell.y = 0;
                        var dist = 1;
                        while ((basePos - faceCell * dist).GetThingList(baseMap).Contains(vehicle))
                        {
                            dist++;
                        }
                        var cell = (basePos - faceCell * dist);
                        return pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly, hostile, hostile, TraverseMode.ByPawn) && cell.Walkable(baseMap);
                    }), vehicle.interiorMap);
                }
                if (spot.IsValid)
                {
                    var job = JobMaker.MakeJob(VIF_DefOf.VIF_GotoAcrossMaps);
                    var driver = job.GetCachedDriver(pawn) as JobDriverAcrossMaps;
                    driver.SetSpots(spot);
                    return job;
                }
            }
            return null;
        }
    }
}