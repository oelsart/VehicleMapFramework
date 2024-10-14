using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class DeSpawnUtility
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
            if (Find.Selector.IsSelected(pawn))
            {
                Find.Selector.Deselect(pawn);
                Find.MainButtonsRoot.tabs.Notify_SelectedObjectDespawned();
            }
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
            map.physicalInteractionReservationManager.ReleaseAllForTarget(pawn);
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
            if (pawn_PathFollower != null)
            {
                pawn_PathFollower.StopDead();
            }
            Pawn_RopeTracker pawn_RopeTracker = pawn.roping;
            if (pawn_RopeTracker != null)
            {
                pawn_RopeTracker.Notify_DeSpawned();
            }
            pawn.mindState.droppedWeapon = null;
            Pawn_NeedsTracker pawn_NeedsTracker = pawn.needs;
            if (pawn_NeedsTracker != null)
            {
                Need_Mood mood = pawn_NeedsTracker.mood;
                if (mood != null)
                {
                    mood.thoughts.situational.Notify_SituationalThoughtsDirty();
                }
            }
            Pawn_MeleeVerbs pawn_MeleeVerbs = pawn.meleeVerbs;
            if (pawn_MeleeVerbs != null)
            {
                pawn_MeleeVerbs.Notify_PawnDespawned();
            }
            Pawn_MechanitorTracker pawn_MechanitorTracker = pawn.mechanitor;
            if (pawn_MechanitorTracker != null)
            {
                pawn_MechanitorTracker.Notify_DeSpawned(mode);
            }
            pawn.ClearAllReservations(false);
            if (map != null)
            {
                map.mapPawns.DeRegisterPawn(pawn);
                map.autoSlaughterManager.Notify_PawnDespawned();
            }
            PawnComponentsUtility.RemoveComponentsOnDespawned(pawn);
        }
    }
}
