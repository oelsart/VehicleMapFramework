using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class StudyUtilityOnVehicle
    {
        public static void TargetHoldingPlatformForEntity(Pawn carrier, Thing entity, TargetInfo exitSpot, TargetInfo enterSpot, bool transferBetweenPlatforms, Thing sourcePlatform)
        {
            bool ValidateTarget(LocalTargetInfo t) => t.HasThing && t.Thing.TryGetComp<CompEntityHolder>(out var comp) && comp.HeldPawn == null && (carrier == null || carrier.CanReserveAndReach(t.Thing.Map, t, PathEndMode.Touch, Danger.Some, 1, -1, null, false, out _, out _));
            bool CanReserveForTransfer(LocalTargetInfo t) => transferBetweenPlatforms || (t.HasThing && carrier.CanReserve(t, t.Thing.Map, 1, -1, null, false));
            Find.Targeter.BeginTargeting(TargetingParameters.ForBuilding(null), (LocalTargetInfo t) =>
            {
            if (carrier != null && !CanReserveForTransfer(t))
            {
                Messages.Message("MessageHolderReserved".Translate(t.Thing.Label), MessageTypeDefOf.RejectInput, true);
                return;
            }
            var enumerator = Find.CurrentMap.BaseMapAndVehicleMaps().SelectMany(m => m.listerThings.ThingsInGroup(ThingRequestGroup.EntityHolder));
                {
                    foreach (var thing in enumerator)
                    {
                        Building_HoldingPlatform building_HoldingPlatform;
                        if ((building_HoldingPlatform = (thing as Building_HoldingPlatform)) != null && entity != building_HoldingPlatform.HeldPawn)
                        {
                            Pawn heldPawn = building_HoldingPlatform.HeldPawn;
                            CompHoldingPlatformTarget compHoldingPlatformTarget;
                            if ((compHoldingPlatformTarget = ((heldPawn != null) ? heldPawn.TryGetComp<CompHoldingPlatformTarget>() : null)) != null && compHoldingPlatformTarget.targetHolder == t.Thing)
                            {
                                Messages.Message("MessageHolderReserved".Translate(t.Thing.Label), MessageTypeDefOf.RejectInput, true);
                                return;
                            }
                        }
                    }
                }
                CompHoldingPlatformTarget compHoldingPlatformTarget2 = entity.TryGetComp<CompHoldingPlatformTarget>();
                if (compHoldingPlatformTarget2 != null)
                {
                    compHoldingPlatformTarget2.targetHolder = t.Thing;
                }
                if (carrier != null)
                {
                    if (t.HasThing && ReachabilityUtilityOnVehicle.CanReach(entity.Map, entity.Position, t, PathEndMode.ClosestTouch, TraverseParms.For(carrier), t.Thing.Map, out var exitSpot2, out var enterSpot2))
                    {
                        Job job = transferBetweenPlatforms ? JobMaker.MakeJob(VMF_DefOf.VMF_TransferBetweenEntityHoldersAcrossMaps, sourcePlatform, t, entity) : JobMaker.MakeJob(VMF_DefOf.VMF_CarryToEntityHolderAcrossMaps, t, entity);
                        job.count = 1;
                        var driver = job.GetCachedDriver(carrier) as JobDriverAcrossMaps;
                        driver.SetSpots(exitSpot, enterSpot, exitSpot2, enterSpot2);
                        carrier.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.Misc), false);
                    }

                }
                if (t.Thing != null && !t.Thing.SafelyContains(entity))
                {
                    Messages.Message("MessageTargetBelowMinimumContainmentStrength".Translate(t.Thing.Label, entity.Label), MessageTypeDefOf.ThreatSmall, true);
                }
            },
            (LocalTargetInfo t) =>
            {
                if (ValidateTarget(t))
                {
                    GenDraw.DrawTargetHighlight(t);
                }
            },
            new Func<LocalTargetInfo, bool>(ValidateTarget),
            null, null, BaseContent.ClearTex, true,
            (LocalTargetInfo t) =>
            {
                Thing thing = t.Thing;
                CompEntityHolder compEntityHolder = (thing != null) ? thing.TryGetComp<CompEntityHolder>() : null;
                TaggedString taggedString;
                if (compEntityHolder == null)
                {
                    taggedString = "ChooseEntityHolder".Translate().CapitalizeFirst() + "...";
                    Widgets.MouseAttachedLabel(taggedString, 0f, 0f);
                    return;
                }
                Pawn pawn = null;
                Building_HoldingPlatform p;
                Pawn pawn2;
                if (carrier != null)
                {
                    pawn = t.Thing.Map.reservationManager.FirstRespectedReserver(t.Thing, carrier, null);
                }

                else if ((p = (t.Thing as Building_HoldingPlatform)) != null && StudyUtility.AlreadyReserved(p, out pawn2))
                {
                    pawn = pawn2;
                }
                if (pawn != null)
                {
                    taggedString = string.Format("{0}: {1}", "EntityHolderReservedBy".Translate(), pawn.LabelShortCap);
                }
                else
                {
                    taggedString = "FloatMenuContainmentStrength".Translate() + ": " + StatDefOf.ContainmentStrength.Worker.ValueToString(compEntityHolder.ContainmentStrength, false, ToStringNumberSense.Absolute);
                    taggedString += "\n" + ("FloatMenuContainmentRequires".Translate(entity).CapitalizeFirst() + ": " + StatDefOf.MinimumContainmentStrength.Worker.ValueToString(entity.GetStatValue(StatDefOf.MinimumContainmentStrength, true, -1), false, ToStringNumberSense.Absolute)).Colorize(t.Thing.SafelyContains(entity) ? Color.white : Color.red);
                }
                Widgets.MouseAttachedLabel(taggedString, 0f, 0f);
            },
            (LocalTargetInfo t) =>
            {
                var baseMap = entity.MapHeldBaseMap();
                var buildings = baseMap.listerBuildings.AllBuildingsColonistOfGroup(ThingRequestGroup.EntityHolder)
                .Concat(VehiclePawnWithMapCache.AllVehiclesOn(baseMap).Where(v => v.AllowsHaulOut).SelectMany(v => v.VehicleMap.listerBuildings.AllBuildingsColonistOfGroup(ThingRequestGroup.EntityHolder)));
                foreach (Building building in buildings)
                {
                    if (ValidateTarget(building) && (carrier == null || CanReserveForTransfer(building)))
                    {
                        GenDraw.DrawArrowPointingAt(building.DrawPos, false);
                    }
                }
            });
        }
    }
}
