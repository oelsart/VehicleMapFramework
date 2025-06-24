using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class CompVehicleSeat : CompBuildableUpgrades
    {
        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (this.parent.IsOnVehicleMapOf(out var vehicle) && selPawn.CanReach(this.parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, this.parent.Map, out var exitSpot, out var enterSpot))
            {
                foreach (var handler in vehicle.handlers)
                {
                    if (handler.AreSlotsAvailable && this.handlerUniqueIDs.Any(h => h.id == handler.uniqueID))
                    {
                        VehicleReservationManager cachedMapComponent = vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>();
                        string key = "VF_EnterVehicle";
                        NamedArgument arg = vehicle.LabelShort;
                        NamedArgument arg2 = handler.role.label;
                        int slots = handler.role.Slots;
                        int count = handler.thingOwner.Count;
                        VehicleHandlerReservation reservation = cachedMapComponent?.GetReservation<VehicleHandlerReservation>(vehicle);
                        FloatMenuOption floatMenuOption2 = new FloatMenuOption(key.Translate(arg, arg2, (slots - (count + ((reservation != null) ? new int?(reservation.ClaimantsOnHandler(handler)) : null)).GetValueOrDefault()).ToString()), delegate ()
                        {
                            if (handler == null)
                            {
                                Messages.Message("VF_HandlerNotEnoughRoom".Translate(selPawn, vehicle), MessageTypeDefOf.RejectInput, false);
                                return;
                            }
                            Job job = new Job(VMF_DefOf.VMF_BoardAcrossMaps, this.parent).SetSpotsToJobAcrossMaps(selPawn, exitSpot, enterSpot);
                            vehicle.GiveLoadJob(selPawn, handler);
                            selPawn.jobs.TryTakeOrderedJob(job, new JobTag?(JobTag.DraftedOrder), false);
                            if (!selPawn.Spawned)
                            {
                                return;
                            }
                            vehicle.Map?.GetCachedMapComponent<VehicleReservationManager>().Reserve<VehicleRoleHandler, VehicleHandlerReservation>(vehicle, selPawn, selPawn.CurJob, handler);
                        }, MenuOptionPriority.Default, null, null, 0f, null, null, true, 0);
                        yield return floatMenuOption2;
                    }
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }
            
            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {
                bool exitBlocked = !this.parent.OccupiedRect().ExpandedBy(1).EdgeCells.NotNullAndAny((IntVec3 cell) => cell.Walkable(this.parent.Map));
                foreach (var keyIDPair in this.handlerUniqueIDs)
                {
                    var handler = vehicle.handlers.FirstOrDefault(h => h.uniqueID == keyIDPair.id);
                    if (handler != null)
                    {
                        foreach (var pawn in handler.thingOwner)
                        {
                            if (vehicle.Drafted && handler.role.HandlingTypes.HasFlag(HandlingType.Movement) && vehicle.Spawned) continue;

                            Command_ActionPawnDrawer command_Action_PawnDrawer = new Command_ActionPawnDrawer
                            {
                                defaultLabel = "VF_DisembarkSinglePawn".Translate(pawn.LabelShort),
                                groupable = false,
                                pawn = pawn,
                                action = delegate ()
                                {
                                    var caravan = pawn.GetCaravan();
                                    caravan?.RemovePawn(pawn);
                                    if (Find.WorldPawns.Contains(pawn))
                                    {
                                        Find.WorldPawns.RemovePawn(pawn);
                                    }
                                    vehicle.DisembarkPawn(pawn);
                                }
                            };
                            if (exitBlocked)
                            {
                                command_Action_PawnDrawer.Disable("VF_DisembarkNoExit".Translate());
                            }
                            yield return command_Action_PawnDrawer;
                        }
                    }
                }

                foreach (var gizmo in vehicle.AllComps.Where(c => c is CompTogglableOverlays).SelectMany(c => c.CompGetGizmosExtra()))
                {
                    yield return gizmo;
                }
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {
                vehicle.CompVehicleTurrets?.RecacheTurretPermissions();
                vehicle.RecachePawnCount();
                this.handlersToDraw = vehicle.handlers.Where(h => this.handlerUniqueIDs.Any(i => h.uniqueID == i.id))
                    .Select(h => (h, base.Props.upgrades.SelectMany(u => (u as VehicleUpgrade).roles).FirstOrDefault(r => r?.key == h.role.key)));
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map);
            this.handlersToDraw = null;
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (!VehicleInteriors.settings.drawPlanet && this.parent.IsOnVehicleMapOf(out var vehicle) && !vehicle.Spawned && !this.handlersToDraw.NullOrEmpty())
            {
                foreach (var handler in this.handlersToDraw)
                {
                    if (handler.Item1.role.PawnRenderer != null)
                    {
                        foreach (Pawn pawn in handler.Item1.thingOwner)
                        {
                            Vector3 drawLoc = this.parent.DrawPos + handler.Item2.pawnRenderer.DrawOffsetFor(this.parent.BaseRotation());
                            Rot4 value = handler.Item1.role.PawnRenderer.RotFor(this.parent.BaseRotation());
                            pawn.Drawer.renderer.RenderPawnAt(drawLoc, new Rot4?(value), false);
                        }
                    }
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            if (VehicleInteriors.settings.weightFactor == 0f) return null;

            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {
                var str = base.CompInspectStringExtra();
                var stat = vehicle.GetStatValue(VMF_DefOf.MaximumPayload);

                return str + $"{VMF_DefOf.MaximumPayload.LabelCap}:" +
                    $" {(VehicleMapUtility.VehicleMapMass(vehicle) * VehicleInteriors.settings.weightFactor).ToStringEnsureThreshold(2, 0)} /" +
                    $" {stat.ToStringEnsureThreshold(2, 0)} {"kg".Translate()}";
            }
            return null;
        }

        private IEnumerable<(VehicleRoleHandler, VehicleUpgrade.RoleUpgrade)> handlersToDraw;
    }
}
