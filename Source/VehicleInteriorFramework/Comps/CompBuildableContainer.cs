using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VehicleInteriors
{
    public class CompBuildableContainer : CompTransporter
    {
        public VehiclePawnWithMap Vehicle
        {
            get
            {
                if (this.parent.IsOnVehicleMapOf(out var vehicle))
                {
                    return vehicle;
                }
                Log.Error("[VehicleMapFramework] Container is not on any vehicle map");
                return null;
            }
        }

        new public bool AnyPawnCanLoadAnythingNow
        {
            get
            {
                if (!AnythingLeftToLoad)
                {
                    return false;
                }

                if (!parent.Spawned)
                {
                    return false;
                }

                IReadOnlyList<Pawn> allPawnsSpawned = parent.BaseMap().mapPawns.AllPawnsSpawned;
                for (int i = 0; i < allPawnsSpawned.Count; i++)
                {
                    if (allPawnsSpawned[i].CurJobDef == JobDefOf.HaulToTransporter)
                    {
                        CompTransporter transporter = ((JobDriver_HaulToTransporter)allPawnsSpawned[i].jobs.curDriver).Transporter;
                        if (transporter != null && transporter.groupID == groupID)
                        {
                            return true;
                        }
                    }

                    if (allPawnsSpawned[i].CurJobDef == JobDefOf.EnterTransporter)
                    {
                        CompTransporter transporter2 = ((JobDriver_EnterTransporter)allPawnsSpawned[i].jobs.curDriver).Transporter;
                        if (transporter2 != null && transporter2.groupID == groupID)
                        {
                            return true;
                        }
                    }
                }

                List<CompTransporter> list = TransportersInGroup(parent.Map);
                if (list == null)
                {
                    return false;
                }

                for (int j = 0; j < allPawnsSpawned.Count; j++)
                {
                    if (allPawnsSpawned[j].mindState.duty != null && allPawnsSpawned[j].mindState.duty.transportersGroup == groupID)
                    {
                        CompTransporter compTransporter = JobGiver_EnterTransporter.FindMyTransporter(list, allPawnsSpawned[j]);
                        if (compTransporter != null && allPawnsSpawned[j].CanReach(compTransporter.parent, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, compTransporter.Map, out _, out _))
                        {
                            return true;
                        }
                    }
                }

                for (int k = 0; k < allPawnsSpawned.Count; k++)
                {
                    if (!allPawnsSpawned[k].IsColonist)
                    {
                        continue;
                    }

                    for (int l = 0; l < list.Count; l++)
                    {
                        if (LoadTransportersJobOnVehicleUtility.HasJobOnTransporter(allPawnsSpawned[k], list[l]))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }
        public bool GatherFromBaseMap => this.gatherFromBaseMap;

        public override void CompTick()
        {
            if (this.parent.IsHashIntervalTick(60) && this.parent.Spawned && this.LoadingInProgressOrReadyToLaunch && this.AnyInGroupHasAnythingLeftToLoad && !this.AnyInGroupNotifiedCantLoadMore && !this.AnyPawnCanLoadAnythingNow && (this.Shuttle == null || !this.Shuttle.Autoload))
            {
                this.notifiedCantLoadMore(this) = true;
                //Messages.Message("MessageCantLoadMoreIntoTransporters".Translate(this.FirstThingLeftToLoadInGroup.LabelNoCount, Faction.OfPlayer.def.pawnsPlural, this.FirstThingLeftToLoadInGroup), this.parent, MessageTypeDefOf.CautionInput, true);
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (this.parent.IsOnVehicleMapOf(out var vehicle))
            {
                this.innerContainer = vehicle.inventory.innerContainer;
                this.massCapacityOverride = vehicle.GetStatValue(VehicleStatDefOf.CargoCapacity);
            }
            else
            {
                this.innerContainer = new ThingOwner<Thing>(this);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (CancelLoad(map) && Shuttle == null)
            {
                if (Props.max1PerGroup)
                {
                    Messages.Message("MessageTransporterSingleLoadCanceled_TransporterDestroyed".Translate(), MessageTypeDefOf.NegativeEvent);
                }
                else
                {
                    Messages.Message("MessageTransportersLoadCanceled_TransporterDestroyed".Translate(), MessageTypeDefOf.NegativeEvent);
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!this.leftToLoad.NullOrEmpty<TransferableOneWay>())
            {
                Command_Action command_Action = new Command_Action
                {
                    defaultLabel = "DesignatorCancel".Translate(),
                    icon = this.Vehicle.VehicleDef.CancelCargoIcon,
                    action = delegate ()
                    {
                        //this.Map.GetCachedMapComponent<VehicleReservationManager>().RemoveLister(this.Vehicle, "LoadVehicle");
                        this.leftToLoad.Clear();
                        this.groupID = -1;
                    }
                };
                yield return command_Action;
            }
            Command_Action command_Action2 = new Command_Action
            {
                defaultLabel = "VF_LoadCargo".Translate(),
                icon = this.Vehicle.VehicleDef.LoadCargoIcon,
                action = delegate ()
                {
                    Find.WindowStack.Add(new Dialog_LoadCargoToBuildableContainer(this));
                }
            };
            yield return command_Action2;

            Command_Toggle command_Toggle = new Command_Toggle
            {
                defaultLabel = "VMF_GatherFromBaseMap".Translate(),
                icon = this.Vehicle.VehicleDef.LoadCargoIcon,
                isActive = () => this.gatherFromBaseMap,
                toggleAction = () =>
                {
                    this.gatherFromBaseMap = !this.gatherFromBaseMap;
                }
            };
            yield return command_Toggle;
        }

        new public void Notify_ThingAdded(Thing t)
        {
            SubtractFromToLoadList(t, t.stackCount, false);
            if (parent.Spawned && Props.pawnLoadedSound != null && t is Pawn)
            {
                Props.pawnLoadedSound.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
            }

            QuestUtility.SendQuestTargetSignals(parent.questTags, "ThingAdded", t.Named("SUBJECT"));
            if (this.leftToLoad.NullOrEmpty())
            {
                this.groupID = -1;
            }
        }

        new public void Notify_ThingAddedAndMergedWith(Thing t, int mergedCount)
        {
            this.SubtractFromToLoadList(t, mergedCount, false);
            if (this.leftToLoad.NullOrEmpty())
            {
                this.groupID = -1;
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref this.groupID, "groupID", 0, false);
            Scribe_Collections.Look(ref this.leftToLoad, "leftToLoad", LookMode.Deep, Array.Empty<object>());
            Scribe_Values.Look(ref this.notifiedCantLoadMore(this), "notifiedCantLoadMore", false, false);
            Scribe_Values.Look(ref this.massCapacityOverride, "massCapacityOverride", 0f, false);
            Scribe_Values.Look(ref this.gatherFromBaseMap, "gatherFromBaseMap", false);
        }

        private AccessTools.FieldRef<CompTransporter, bool> notifiedCantLoadMore = AccessTools.FieldRefAccess<CompTransporter, bool>("notifiedCantLoadMore");

        private bool gatherFromBaseMap;
    }
}
