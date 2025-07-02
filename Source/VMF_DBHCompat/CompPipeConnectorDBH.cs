using DubsBadHygiene;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors;

public class CompPipeConnectorDBH : CompPipe, IPipeConnector
{
    new public CompProperties_PipeConnectorDBH Props => (CompProperties_PipeConnectorDBH)props;

    public CompPipeConnector.PipeMod Mod => CompPipeConnector.PipeMod.DubsBadHygiene;

    public CompPipeConnector CompPipeConnector
    {
        get
        {
            if (compPipeConnector == null)
            {
                if (!parent.TryGetComp(out compPipeConnector))
                {
                    Log.Error($"[VehicleMapFramework] CompPipeConnector not found with {parent.LabelCap}.");
                }
            }
            return compPipeConnector;
        }
    }

    private CompPipeConnectorDBH PairComp
    {
        get
        {
            if (pairComp == null)
            {
                pairComp = CompPipeConnector.Pair?.parent.TryGetComp<CompPipeConnectorDBH>();
            }
            return pairComp;
        }
    }

    public IEnumerable<FloatMenuOption> FloatMenuOptions
    {
        get
        {
            yield return new FloatMenuOption(sewagePipeStuff.LabelCap, () =>
            {
                parent.DrawColor = new Color(200f, 200f, 200f);
                mode = PipeType.Sewage;
            });
            //yield return new FloatMenuOption(airPipe.LabelCap, () =>
            //{
            //    this.parent.DrawColor = new Color(110f, 110f, 110f);
            //    mode = PipeType.Air;
            //});
        }
    }

    public Texture GizmoIcon
    {
        get
        {
            if (mode == PipeType.Air)
            {
                return airPipe.uiIcon;
            }
            return sewagePipeStuff.uiIcon;
        }
    }

    public bool ConnectCondition(CompPipeConnector another)
    {
        return another.parent.TryGetComp<CompPipeConnectorDBH>(out var compPipeConnectorDBH) && mode == compPipeConnectorDBH.mode;
    }

    public void ConnectedTickAction()
    {
        if (PairComp != null)
        {
            pumpUp = pumpUp || pairComp.pumpUp;
            if (pumpUp)
            {
                var water = Mathf.Min(flowAmount * CompPipeConnector.ticksInterval, pipeNet.WaterStorage, pairComp.pipeNet.WaterTowers.Sum(w => w.space));
                pipeNet.PullWater(water, out _);
                pairComp.pipeNet.PushWater(water);
                var temp = pipeNet.HotWaterTanks.Empty() ? 0f : pipeNet.HotWaterTanks.Average(h => h.HeaterTemp);
                var pairTemp = pairComp.pipeNet.HotWaterTanks.Empty() ? 0f : pairComp.pipeNet.HotWaterTanks.Average(h => h.HeaterTemp);
                pairComp.pipeNet.HotWaterTanks.ForEach(h => h.HeaterTemp += (temp - pairTemp) * water / pairComp.pipeNet.WaterStorage);
            }
            else
            {
                var water = Mathf.Min(flowAmount * CompPipeConnector.ticksInterval, pipeNet.WaterTowers.Sum(w => w.space), pairComp.pipeNet.WaterStorage);
                pipeNet.PushWater(water);
                pairComp.pipeNet.PullWater(water, out _);
                var temp = pipeNet.HotWaterTanks.Empty() ? 0f : pipeNet.HotWaterTanks.Average(h => h.HeaterTemp);
                var pairTemp = pairComp.pipeNet.HotWaterTanks.Empty() ? 0f : pairComp.pipeNet.HotWaterTanks.Average(h => h.HeaterTemp);
                pipeNet.HotWaterTanks.ForEach(h => h.HeaterTemp += (pairTemp - temp) * water / pipeNet.WaterStorage);
            }
        }
    }

    public void DisconnectedAction()
    {
        pairComp = null;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        if (CompPipeConnector.selectedComp == this && mode != PipeType.Air)
        {
            yield return new Command_ToggleIcon
            {
                defaultLabel = "VMF_PumpUp".Translate(),
                labelTwo = "VMF_Drain".Translate(),
                icon = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/PumpUp"),
                iconTwo = ContentFinder<Texture2D>.Get("VehicleInteriors/UI/Drain"),
                isActive = () => pumpUp,
                toggleSound = SoundDefOf.Checkbox_TurnedOn,
                toggleAction = () =>
                {
                    pumpUp = !pumpUp;
                    if (PairComp != null)
                    {
                        pairComp.pumpUp = pumpUp;
                    }
                },
            };
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref mode, "mode");
        Scribe_Values.Look(ref pumpUp, "pumpUp");
    }

    new public PipeType mode;

    public bool pumpUp = true;

    private CompPipeConnector compPipeConnector;

    private CompPipeConnectorDBH pairComp;

    private static readonly ThingDef sewagePipeStuff = DefDatabase<ThingDef>.GetNamed("sewagePipeStuff");

    private static readonly ThingDef airPipe = DefDatabase<ThingDef>.GetNamed("airPipe");

    private const float flowAmount = 3f;
}
