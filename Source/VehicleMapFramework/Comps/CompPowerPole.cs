using System;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;
using static VehicleMapFramework.ModCompat.PowerPoles;

namespace VehicleMapFramework;

public class CompPowerPole : CompPowerNetLink
{
    private Vector3 prevDrawPos;
    
    protected override float Radius => CableMaxDistance();

    protected override float MaxPowerPush => 5000f;

    protected override float PowerLossFactor => 1f;

    protected override PowerTransferMode Mode => PowerTransferMode.Transmit;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (respawningAfterLoad)
        {
            FrameDelay.DelayOne(RegeneratePoints, prevDrawPos);
        }
    }

    public override void CompTick()
    {
        var drawPos = parent.DrawPos;
        if (prevDrawPos != drawPos)
        {
            RegeneratePoints(drawPos);
        }

        if (parent.IsHashIntervalTick(250))
        {
            parent.TickRare();
        }
        
        base.CompTick();
    }

    private void RegeneratePoints(Vector3 drawPos)
    {
        prevDrawPos = drawPos;

        if (parent.def.thingClass.SameOrSubclassOf(Building_LongDistanceCabled))
        {
            foreach (var building in Patch_Building_LongDistancePower_GetAllLinked.GetAllLinked((Building)parent, false))
            {
                if (building.def.thingClass.SameOrSubclassOf(Building_LongDistanceCabled))
                {
                    GeneratePointsAsync(parent, Params<ValueTuple<object, object>>.Get((parent, building)));
                    GeneratePointsAsync(building, Params<ValueTuple<object, object>>.Get((building, parent)));
                }
            }
        }
    }
    
    protected override bool TryFindConnection(out CompPowerNetLink linkTo)
    {
        linkTo = null;
        return false;
    }

    public override void Disconnect()
    {
        var linkedTo = LinkedTo;
        if (linkedTo is not null &&
            parent.def.thingClass.SameOrSubclassOf(Building_LongDistancePower) &&
            linkedTo.def.thingClass.SameOrSubclassOf(Building_LongDistancePower) &&
            (bool)IsLinkedTo(parent, SingleParam.Get(linkedTo)))
        {
            Delay.AfterNSeconds(0.5f, () =>
            {
                TryRemoveLink(parent, SingleParam.Get(linkedTo));
                if (parent.def.thingClass.SameOrSubclassOf(Building_LongDistanceCabled) &&
                    linkedTo.def.thingClass.SameOrSubclassOf(Building_LongDistanceCabled))
                {
                    connectionToPoints(parent).Remove(linkedTo);
                    connectionToPoints(linkedTo).Remove(parent);
                }
            });
        }
        base.Disconnect();
    }
}