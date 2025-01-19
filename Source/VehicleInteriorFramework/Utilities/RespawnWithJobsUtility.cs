using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VehicleInteriors
{
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
            Map map = pawn.Map;
            map.overlayDrawer.DisposeHandle(pawn);
            RegionListersUpdater.DeregisterInRegions(pawn, map);
            map.spawnedThings.Remove(pawn);
            map.listerThings.Remove(pawn);
            map.thingGrid.Deregister(pawn, false);
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
            //if (Find.Selector.IsSelected(pawn))
            //{
            //    Find.Selector.Deselect(pawn);
            //    Find.MainButtonsRoot.tabs.Notify_SelectedObjectDespawned();
            //}
            pawn.DirtyMapMesh(map);
            if (pawn.def.drawerType != DrawerType.MapMeshOnly)
            {
                map.dynamicDrawManager.DeRegisterDrawable(pawn);
            }
            Region validRegionAt_NoRebuild = map.regionGrid.GetValidRegionAt_NoRebuild(pawn.Position);
            if (validRegionAt_NoRebuild != null)
            {
                Room room = validRegionAt_NoRebuild.Room;
                if (room != null)
                {
                    room.Notify_ContainedThingSpawnedOrDespawned(pawn);
                }
            }
            Find.TickManager.DeRegisterAllTickabilityFor(pawn);
            pawn.ForceSetStateToUnspawned();
            map.attackTargetsCache.Notify_ThingDespawned(pawn);
            //map.physicalInteractionReservationManager.ReleaseAllForTarget(pawn);
            IHaulEnroute thing;
            if ((thing = (pawn as IHaulEnroute)) != null)
            {
                map.enrouteManager.Notify_ContainerDespawned(thing);
            }
            StealAIDebugDrawer.Notify_ThingChanged(pawn);
            IHaulDestination haulDestination;
            if ((haulDestination = (pawn as IHaulDestination)) != null)
            {
                map.haulDestinationManager.RemoveHaulDestination(haulDestination);
            }
            IHaulSource source;
            if ((source = (pawn as IHaulSource)) != null)
            {
                map.haulDestinationManager.RemoveHaulSource(source);
            }
            if (pawn is IThingHolder && Find.ColonistBar != null)
            {
                Find.ColonistBar.MarkColonistsDirty();
            }
            if (pawn.def.category == ThingCategory.Item)
            {
                SlotGroup slotGroup = pawn.Position.GetSlotGroup(map);
                if (slotGroup != null && slotGroup.parent != null)
                {
                    slotGroup.parent.Notify_LostThing(pawn);
                }
            }
            QuestUtility.SendQuestTargetSignals(pawn.questTags, "Despawned", pawn.Named("SUBJECT"));
            pawn.spawnedTick = -1;

            if (pawn.AllComps != null)
            {
                for (int i = 0; i < pawn.AllComps.Count; i++)
                {
                    pawn.AllComps[i].PostDeSpawn(map);
                }
            }

            Pawn_PathFollower pawn_PathFollower = pawn.pather;
            pawn_PathFollower?.StopDead();
            Pawn_RopeTracker pawn_RopeTracker = pawn.roping;
            //pawn_RopeTracker?.Notify_DeSpawned();
            pawn.mindState.droppedWeapon = null;
            Pawn_NeedsTracker pawn_NeedsTracker = pawn.needs;
            if (pawn_NeedsTracker != null)
            {
                Need_Mood mood = pawn_NeedsTracker.mood;
                mood?.thoughts.situational.Notify_SituationalThoughtsDirty();
            }
            Pawn_MeleeVerbs pawn_MeleeVerbs = pawn.meleeVerbs;
            pawn_MeleeVerbs?.Notify_PawnDespawned();
            Pawn_MechanitorTracker pawn_MechanitorTracker = pawn.mechanitor;
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
            vehicle.Map.GetCachedMapComponent<VehiclePositionManager>().ReleaseClaimed(vehicle);
            VehicleReservationManager cachedMapComponent = vehicle.Map.GetCachedMapComponent<VehicleReservationManager>();
            cachedMapComponent.ClearReservedFor(vehicle);
            cachedMapComponent.RemoveAllListerFor(vehicle);
            vehicle.cargoToLoad.Clear();
            vehicle.Map.GetCachedMapComponent<ListerVehiclesRepairable>().Notify_VehicleDespawned(vehicle);
            vehicle.EventRegistry[VehicleEventDefOf.Despawned].ExecuteEvents();
            vehicle.DeSpawnWithoutJobClear(mode);
            vehicle.SoundCleanup();
        }

        public static void SpawnSetupWithoutJobClear(this Pawn pawn, Map map, bool respawningAfterLoad)
        {
            if (pawn.Dead)
            {
                Log.Warning("Tried to spawn Dead Pawn " + pawn.ToStringSafe<Pawn>() + ". Replacing with corpse.");
                Corpse corpse = (Corpse)ThingMaker.MakeThing(pawn.RaceProps.corpseDef, null);
                corpse.InnerPawn = pawn;
                GenSpawn.Spawn(corpse, pawn.Position, map, WipeMode.Vanish);
                return;
            }
            if (pawn.def == null || pawn.kindDef == null)
            {
                Log.Warning("Tried to spawn pawn without def " + pawn.ToStringSafe<Pawn>() + ".");
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
                Log.Error("Pawn " + pawn.ToStringSafe<Pawn>() + " spawned in invalid state. Destroying...");
                try
                {
                    pawn.DeSpawn(DestroyMode.Vanish);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat(new object[]
                    {
                        "Tried to despawn ",
                        pawn.ToStringSafe<Pawn>(),
                        " because of the previous error but couldn't: ",
                        ex
                    }));
                }
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
                return;
            }
            pawn.Drawer.Notify_Spawned();
            pawn.rotationTracker.Notify_Spawned();
            if (!respawningAfterLoad)
            {
                pawn.pather.ResetToCurrentPosition();
            }
            pawn.Map.mapPawns.RegisterPawn(pawn);
            pawn.Map.autoSlaughterManager.Notify_PawnSpawned();
            if (pawn.relations != null)
            {
                pawn.relations.everSeenByPlayer = true;
            }
            AddictionUtility.CheckDrugAddictionTeachOpportunity(pawn);
            Pawn_NeedsTracker pawn_NeedsTracker = pawn.needs;
            if (pawn_NeedsTracker != null)
            {
                Need_Mood mood = pawn_NeedsTracker.mood;
                if (mood != null)
                {
                    PawnRecentMemory recentMemory = mood.recentMemory;
                    recentMemory?.Notify_Spawned(respawningAfterLoad);
                }
            }
            Pawn_EquipmentTracker pawn_EquipmentTracker = pawn.equipment;
            pawn_EquipmentTracker?.Notify_PawnSpawned();
            Pawn_HealthTracker pawn_HealthTracker = pawn.health;
            pawn_HealthTracker?.Notify_Spawned();
            Pawn_MechanitorTracker pawn_MechanitorTracker = pawn.mechanitor;
            pawn_MechanitorTracker?.Notify_PawnSpawned(respawningAfterLoad);
            Pawn_MutantTracker pawn_MutantTracker = pawn.mutant;
            pawn_MutantTracker?.Notify_Spawned(respawningAfterLoad);
            Pawn_InfectionVectorTracker pawn_InfectionVectorTracker = pawn.infectionVectors;
            pawn_InfectionVectorTracker?.NotifySpawned(respawningAfterLoad);
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
                    for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
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
            if (pawn.Ideo != null && pawn.Ideo.hidden)
            {
                pawn.Ideo.hidden = false;
            }
        }
    }
}
