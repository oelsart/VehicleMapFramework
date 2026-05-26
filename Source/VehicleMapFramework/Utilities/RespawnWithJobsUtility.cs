using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SmashTools;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public static class RespawnWithJobsUtility
{
    private static readonly AccessTools.FieldRef<LordJob_Ritual, List<RitualStage>> stages =
        AccessTools.FieldRefAccess<LordJob_Ritual, List<RitualStage>>("stages");
    
    private static readonly AccessTools.FieldRef<LordJob_Ritual, List<RitualStagePositions>> ritualStagePositions =
        AccessTools.FieldRefAccess<LordJob_Ritual, List<RitualStagePositions>>("ritualStagePositions");
    
    private static readonly AccessTools.FieldRef<LordJob_Joinable_Gathering, IntVec3> spot =
        AccessTools.FieldRefAccess<LordJob_Joinable_Gathering, IntVec3>("spot");
    
    extension(Pawn pawn)
    {
        public void DeSpawnWithoutJobClear(DestroyMode mode = DestroyMode.Vanish)
        {
            if (pawn.Destroyed)
            {
                Log.Error("Tried to despawn " + pawn.ToStringSafe<Thing>() + " which is already destroyed.");
                return;
            }
            if (!pawn.Spawned)
            {
                Log.Error("Tried to despawn " + pawn.ToStringSafe<Thing>() + " which is not spawned.");
                return;
            }
            var map = pawn.Map;
            map.overlayDrawer.DisposeHandle(pawn);
            RegionListersUpdater.DeregisterInRegions(pawn, map);
            map.spawnedThings.Remove(pawn);
            map.listerThings.Remove(pawn);
            map.thingGrid.Deregister(pawn);
            map.coverGrid.DeRegister(pawn);
            if (pawn.def.receivesSignals)
            {
                Find.SignalManager.DeregisterReceiver(pawn);
            }
            map.tooltipGiverList.Notify_ThingDespawned(pawn);
            if (pawn.def.CanAffectLinker)
            {
                map.linkGrid.Notify_LinkerCreatedOrDestroyed(pawn);
                map.mapDrawer.MapMeshDirty(pawn.Position, MapMeshFlagDefOf.Things, true, false);
            }
            if (Find.Selector.IsSelected(pawn) && map.IsVehicleMapOf(out var vehicle) && !vehicle.Spawned)
            {
                Find.Selector.Deselect(pawn);
                Find.MainButtonsRoot.tabs.Notify_SelectedObjectDespawned();
            }
            pawn.DirtyMapMesh(map);
            if (pawn.def.drawerType != DrawerType.MapMeshOnly)
            {
                map.dynamicDrawManager.DeRegisterDrawable(pawn);
            }
            var validRegionAt_NoRebuild = map.regionGrid.GetValidRegionAt_NoRebuild(pawn.Position);
            if (validRegionAt_NoRebuild != null)
            {
                var room = validRegionAt_NoRebuild.Room;
                room?.Notify_ContainedThingSpawnedOrDespawned(pawn);
            }
            Find.TickManager.DeRegisterAllTickabilityFor(pawn);
            pawn.ForceSetStateToUnspawned();
            map.attackTargetsCache.Notify_ThingDespawned(pawn);
            //map.physicalInteractionReservationManager.ReleaseAllForTarget(pawn);
            if (pawn is IHaulEnroute thing)
            {
                map.enrouteManager.Notify_ContainerDespawned(thing);
            }
            StealAIDebugDrawer.Notify_ThingChanged(pawn);
            if (pawn is IHaulDestination haulDestination)
            {
                map.haulDestinationManager.RemoveHaulDestination(haulDestination);
            }
            if (pawn is IHaulSource source)
            {
                map.haulDestinationManager.RemoveHaulSource(source);
            }
            if (Find.ColonistBar != null)
            {
                Find.ColonistBar.MarkColonistsDirty();
            }
            if (pawn.def.category == ThingCategory.Item)
            {
                var slotGroup = pawn.Position.GetSlotGroup(map);
                if (slotGroup is { parent: not null })
                {
                    slotGroup.parent.Notify_LostThing(pawn);
                }
            }
            QuestUtility.SendQuestTargetSignals(pawn.questTags, "Despawned", pawn.Named("SUBJECT"));
            pawn.spawnedTick = -1;

            if (pawn.AllComps != null)
            {
                for (var i = 0; i < pawn.AllComps.Count; i++)
                {
                    pawn.AllComps[i].PostDeSpawn(map);
                }
            }

            var pawn_PathFollower = pawn.pather;
            pawn_PathFollower?.StopDead();
            //pawn_RopeTracker?.Notify_DeSpawned();
            pawn.mindState.droppedWeapon = null;
            var pawn_NeedsTracker = pawn.needs;
            if (pawn_NeedsTracker != null)
            {
                var mood = pawn_NeedsTracker.mood;
                mood?.thoughts.situational.Notify_SituationalThoughtsDirty();
            }
            var pawn_MeleeVerbs = pawn.meleeVerbs;
            pawn_MeleeVerbs?.Notify_PawnDespawned();
            var pawn_MechanitorTracker = pawn.mechanitor;
            pawn_MechanitorTracker?.Notify_DeSpawned(mode);
            //pawn.ClearAllReservations(false);
            if (map != null)
            {
                map.mapPawns.DeRegisterPawn(pawn);
                map.autoSlaughterManager.Notify_PawnDespawned();
            }
            //PawnComponentsUtility.RemoveComponentsOnDespawned(pawn);

            //Designationの掃除をしておかないとdesignationManagerに登録されたままになってしまう
            if (pawn.IsCarrying())
            {
                map.designationManager.RemoveAllDesignationsOn(pawn.carryTracker.CarriedThing);
            }
        }
    }
    
    extension(VehiclePawn vehicle)
    {
        public void DeSpawnWithoutJobClearVehicle(DestroyMode mode = DestroyMode.Vanish)
        {
            vehicle.vehiclePather?.StopDead();
            vehicle.Map.GetDetachedMapComponent<VehiclePositionManager>().ReleaseClaimed(vehicle);
            var cachedMapComponent = vehicle.Map.GetCachedMapComponent<VehicleReservationManager>();
            cachedMapComponent.ClearReservedFor(vehicle);
            cachedMapComponent.RemoveAllListerFor(vehicle);
            vehicle.cargoToLoad.Clear();
            vehicle.Map.GetCachedMapComponent<ListerVehiclesRepairable>().NotifyVehicleDespawned(vehicle);
            vehicle.EventRegistry[VehicleEventDefOf.Despawned].ExecuteEvents();
            if (!vehicle.AllComps.NullOrEmpty())
            {
                for (var i = 0; i < vehicle.AllComps.Count; i++)
                {
                    if (vehicle.AllComps[i] is VehicleComp vehicleComp)
                    {
                        vehicleComp.OnDeSpawn();
                    }
                }
            }
            vehicle.DeSpawnWithoutJobClear(mode);
            vehicle.SoundCleanup();
        }
    }
}