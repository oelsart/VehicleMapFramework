using RimWorld;
using SmashTools;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class RespawnWithJobsUtility
{
    public static void DeSpawnWithoutJobClear(this Pawn pawn, DestroyMode mode = DestroyMode.Vanish)
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

    public static void DeSpawnWithoutJobClearVehicle(this VehiclePawn vehicle, DestroyMode mode = DestroyMode.Vanish)
    {
        vehicle.vehiclePather?.StopDead();
        vehicle.Map.GetDetachedMapComponent<VehiclePositionManager>().ReleaseClaimed(vehicle);
        var cachedMapComponent = vehicle.Map.GetCachedMapComponent<VehicleReservationManager>();
        cachedMapComponent.ClearReservedFor(vehicle);
        cachedMapComponent.RemoveAllListerFor(vehicle);
        vehicle.cargoToLoad.Clear();
        vehicle.Map.GetCachedMapComponent<ListerVehiclesRepairable>().NotifyVehicleDespawned(vehicle);
        vehicle.EventRegistry[VehicleEventDefOf.Despawned].ExecuteEvents();
        vehicle.DeSpawnWithoutJobClear(mode);
        vehicle.SoundCleanup();
    }

    public static void SpawnSetupWithoutJobClear(this Pawn pawn, Map map, bool respawningAfterLoad)
    {
        if (pawn.Dead)
        {
            Log.Warning("Tried to spawn Dead Pawn " + pawn.ToStringSafe() + ". Replacing with corpse.");
            var corpse = (Corpse)ThingMaker.MakeThing(pawn.RaceProps.corpseDef);
            corpse.InnerPawn = pawn;
            GenSpawn.Spawn(corpse, pawn.Position, map);
            return;
        }
        if (pawn.def == null || pawn.kindDef == null)
        {
            Log.Warning("Tried to spawn pawn without def " + pawn.ToStringSafe() + ".");
            return;
        }
        pawn.SpawnSetup(map, respawningAfterLoad);
        if (Find.WorldPawns.Contains(pawn))
        {
            Find.WorldPawns.RemovePawn(pawn);
        }
        //PawnComponentsUtility.AddComponentsForSpawn(pawn);
        if (!PawnUtility.InValidState(pawn))
        {
            VMF_Log.Warning("The pawn has recovered from an invalid state caused by map transition. Please report it with the log to the mod author.\n" +
                $"State: pawn.health: {pawn.health != null}, pawn.stances: {pawn.stances != null}, pawn.mindState: {pawn.mindState != null}, pawn.needs: {pawn.needs != null}, pawn.ageTracker: {pawn.ageTracker != null}");
            pawn.health ??= new Pawn_HealthTracker(pawn);
            pawn.stances ??= new Pawn_StanceTracker(pawn);
            pawn.mindState ??= new Pawn_MindState(pawn);
            pawn.needs ??= new Pawn_NeedsTracker(pawn);
            pawn.ageTracker ??= new Pawn_AgeTracker(pawn);
            //Log.Error("Pawn " + pawn.ToStringSafe<Pawn>() + " spawned in invalid state. Destroying...");
            //try
            //{
            //    pawn.DeSpawn(DestroyMode.Vanish);
            //}
            //catch (Exception ex)
            //{
            //    Log.Error(string.Concat(
            //    [
            //        "Tried to despawn ",
            //        pawn.ToStringSafe<Pawn>(),
            //        " because of the previous error but couldn't: ",
            //        ex
            //    ]));
            //}
            //Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
            //return;
        }
        pawn.Drawer.Notify_Spawned();
        pawn.rotationTracker.Notify_Spawned();
        if (!respawningAfterLoad)
        {
            pawn.pather.ResetToCurrentPosition();
        }
        pawn.Map.mapPawns.RegisterPawn(pawn);
        pawn.Map.autoSlaughterManager.Notify_PawnSpawned();
        pawn.relations?.everSeenByPlayer = true;
        AddictionUtility.CheckDrugAddictionTeachOpportunity(pawn);
        pawn.needs?.mood?.recentMemory?.Notify_Spawned(respawningAfterLoad);
        pawn.equipment?.Notify_PawnSpawned();
        pawn.health?.Notify_Spawned();
        pawn.mechanitor?.Notify_PawnSpawned(respawningAfterLoad);
        pawn.mutant?.Notify_Spawned(respawningAfterLoad);
        pawn.infectionVectors?.NotifySpawned(respawningAfterLoad);
        if (pawn.Faction == Faction.OfPlayer)
        {
            pawn.Ideo?.RecacheColonistBelieverCount();
        }
        if (!respawningAfterLoad)
        {
            if ((pawn.Faction == Faction.OfPlayer || pawn.IsPlayerControlled) && pawn.Position.Fogged(map))
            {
                FloodFillerFog.FloodUnfog(pawn.Position, map);
            }
            Find.GameEnder.CheckOrUpdateGameOver();
            if (pawn.Faction == Faction.OfPlayer)
            {
                Find.StoryWatcher.statsRecord.UpdateGreatestPopulation();
                Find.World.StoryState.RecordPopulationIncrease();
            }
            if (!pawn.IsMutant)
            {
                PawnDiedOrDownedThoughtsUtility.RemoveDiedThoughts(pawn);
            }
            if (pawn.IsQuestLodger())
            {
                for (var i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
                {
                    if (pawn.health.hediffSet.hediffs[i].def.removeOnQuestLodgers)
                    {
                        pawn.health.RemoveHediff(pawn.health.hediffSet.hediffs[i]);
                    }
                }
            }
        }
        //if (pawn.RaceProps.soundAmbience != null)
        //{
        //    LongEventHandler.ExecuteWhenFinished(delegate
        //    {
        //        pawn.sustainerAmbient = pawn.RaceProps.soundAmbience.TrySpawnSustainer(SoundInfo.InMap(pawn, MaintenanceType.PerTick));
        //    });
        //}
        //if (pawn.RaceProps.soundMoving != null)
        //{
        //    LongEventHandler.ExecuteWhenFinished(delegate
        //    {
        //        pawn.sustainerMoving = pawn.RaceProps.soundMoving.TrySpawnSustainer(SoundInfo.InMap(pawn, MaintenanceType.PerTick));
        //    });
        //}
        if (pawn.Ideo is { hidden: true })
        {
            pawn.Ideo.hidden = false;
        }
    }
}
