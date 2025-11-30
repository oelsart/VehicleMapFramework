using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Vehicles;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace VehicleMapFramework;

public class CompBuildableContainer : CompTransporter
{
    private readonly AccessTools.FieldRef<CompTransporter, bool> notifiedCantLoadMore = AccessTools.FieldRefAccess<CompTransporter, bool>("notifiedCantLoadMore");

    private bool gatherFromBaseMap;

    public VehiclePawnWithMap Vehicle => parent.IsOnVehicleMapOf(out var vehicle) ? vehicle : null;

    public new bool AnyPawnCanLoadAnythingNow
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

            var allPawnsSpawned = parent.BaseMap().mapPawns.AllPawnsSpawned;
            for (var i = 0; i < allPawnsSpawned.Count; i++)
            {
                if (allPawnsSpawned[i].CurJobDef == JobDefOf.HaulToTransporter)
                {
                    var transporter = ((JobDriver_HaulToTransporter)allPawnsSpawned[i].jobs.curDriver).Transporter;
                    if (transporter != null && transporter.groupID == groupID)
                    {
                        return true;
                    }
                }

                if (allPawnsSpawned[i].CurJobDef == JobDefOf.EnterTransporter)
                {
                    var transporter2 = ((JobDriver_EnterTransporter)allPawnsSpawned[i].jobs.curDriver).Transporter;
                    if (transporter2 != null && transporter2.groupID == groupID)
                    {
                        return true;
                    }
                }
            }

            var list = TransportersInGroup(parent.Map);
            if (list == null)
            {
                return false;
            }

            for (var j = 0; j < allPawnsSpawned.Count; j++)
            {
                if (allPawnsSpawned[j].mindState.duty != null && allPawnsSpawned[j].mindState.duty.transportersGroup == groupID)
                {
                    var compTransporter = JobGiver_EnterTransporter.FindMyTransporter(list, allPawnsSpawned[j]);
                    if (compTransporter != null && allPawnsSpawned[j].CanReach(compTransporter.parent,
                            PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn, compTransporter.Map))
                    {
                        return true;
                    }
                }
            }

            for (var k = 0; k < allPawnsSpawned.Count; k++)
            {
                if (!allPawnsSpawned[k].IsColonist)
                {
                    continue;
                }

                for (var l = 0; l < list.Count; l++)
                {
                    if (LoadTransportersJobUtility.HasJobOnTransporter(allPawnsSpawned[k], list[l]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public bool GatherFromBaseMap => gatherFromBaseMap;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        Delay.AfterNTicks(1, () =>
        {
            if (parent.IsOnVehicleMapOf(out var vehicle))
            {
                var oldContainer = innerContainer;
                innerContainer = vehicle.inventory.innerContainer;
                if (oldContainer != null)
                {
                    innerContainer.TryAddRangeOrTransfer(oldContainer);
                }
                massCapacityOverride = vehicle.GetStatValue(VehicleStatDefOf.CargoCapacity);
                vehicle.ContainerComps.Add(this);
            }
            else
            {
                innerContainer = new ThingOwner<Thing>(this);
                massCapacityOverride = 0f;
            }
        });
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        if (map.IsVehicleMapOf(out var vehicle)) vehicle.ContainerComps.Remove(this);
        if (CancelLoad(map) && Shuttle == null)
        {
            Messages.Message(
                Props.max1PerGroup
                    ? "MessageTransporterSingleLoadCanceled_TransporterDestroyed".Translate()
                    : "MessageTransportersLoadCanceled_TransporterDestroyed".Translate(),
                MessageTypeDefOf.NegativeEvent);
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (Vehicle is null) yield break;

        if (!leftToLoad.NullOrEmpty())
        {
            Command_Action command_Action = new()
            {
                defaultLabel = "DesignatorCancel".Translate(),
                icon = Vehicle.VehicleDef.CancelCargoIcon,
                action = delegate
                {
                    leftToLoad.Clear();
                    groupID = -1;
                }
            };
            yield return command_Action;
        }
        Command_Action command_Action2 = new()
        {
            defaultLabel = "VF_LoadCargo".Translate(),
            icon = Vehicle.VehicleDef.LoadCargoIcon,
            action = delegate
            {
                Find.WindowStack.Add(new Dialog_LoadCargoToBuildableContainer(this));
            }
        };
        yield return command_Action2;

        Command_Toggle command_Toggle = new()
        {
            defaultLabel = "VMF_GatherFromBaseMap".Translate(),
            icon = Vehicle.VehicleDef.LoadCargoIcon,
            isActive = () => gatherFromBaseMap,
            toggleAction = () =>
            {
                gatherFromBaseMap = !gatherFromBaseMap;
            }
        };
        yield return command_Toggle;
    }

    public new void Notify_ThingAdded(Thing t)
    {
        SubtractFromToLoadList(t, t.stackCount, false);
        if (parent.Spawned && Props.pawnLoadedSound != null && t is Pawn)
        {
            Props.pawnLoadedSound.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
        }

        QuestUtility.SendQuestTargetSignals(parent.questTags, "ThingAdded", t.Named("SUBJECT"));
        if (leftToLoad.NullOrEmpty())
        {
            groupID = -1;
        }
    }

    public new void Notify_ThingAddedAndMergedWith(Thing t, int mergedCount)
    {
        SubtractFromToLoadList(t, mergedCount, false);
        if (leftToLoad.NullOrEmpty())
        {
            groupID = -1;
        }
    }

    public override void PostExposeData()
    {
        Scribe_Values.Look(ref groupID, "groupID");
        if (!parent.IsOnVehicleMapOf(out var vehicle) || innerContainer != vehicle?.inventory?.innerContainer)
        {
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
        }
        Scribe_Collections.Look(ref leftToLoad, "leftToLoad", LookMode.Deep);
        Scribe_Values.Look(ref notifiedCantLoadMore(this), "notifiedCantLoadMore");
        Scribe_Values.Look(ref massCapacityOverride, "massCapacityOverride");
        Scribe_Values.Look(ref gatherFromBaseMap, "gatherFromBaseMap");
    }
}
