using HarmonyLib;
using PipeSystem;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleInteriors;

public class CompPipeConnectorVEF : CompResource, IPipeConnector
{
    public CompPipeConnector.PipeMod Mod => CompPipeConnector.PipeMod.VanillaExpandedFramework;

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

    private CompPipeConnectorVEF PairComp
    {
        get
        {
            if (pairComp == null)
            {
                CompPipeConnector.Pair?.parent.TryGetComp(out pairComp);
            }
            return pairComp;
        }
    }

    public Texture GizmoIcon
    {
        get
        {
            return pipeNet?.pipeDefs?.FirstOrDefault()?.uiIcon;
        }
    }

    public IEnumerable<FloatMenuOption> FloatMenuOptions
    {
        get
        {
            return DefDatabase<PipeNetDef>.AllDefs.Select(d =>
            {
                return new FloatMenuOption(d.resource.name, () =>
                {
                    pipeNet = d;
                    parent.DrawColor = d.resource.color;
                    PipeNet.UnregisterComp(this);
                    PipeNetManager.RegisterConnector(this);
                    pipeNetCount(PipeNetManager) = PipeNetManager.pipeNets.Count;

                });
            });
        }
    }

    public bool ConnectCondition(CompPipeConnector another)
    {
        return another.parent.TryGetComp<CompPipeConnectorVEF>(out var CompPipeConnectorVEF) && pipeNet == CompPipeConnectorVEF.pipeNet;
    }

    public void ConnectedTickAction()
    {
        if (PairComp != null && PipeNet != pairComp.PipeNet)
        {
            var pipeNet = pairComp.PipeNet;
            for (var i = 0; i < pipeNet.connectors.Count; i++)
            {
                PipeNet.RegisterComp(pipeNet.connectors[i]);
            }
            pairComp.PipeNet = PipeNet;
            pipeNet.Destroy();
            var component = MapComponentCache<PipeNetManager>.GetComponent(pairComp.parent.Map);
            pipeNetCount(component) = component.pipeNets.Count;
            parent.DirtyMapMesh(parent.Map);
        }
    }

    public void DisconnectedAction()
    {
        var pipeNetManager = MapComponentCache<PipeNetManager>.GetComponent(parent.Map);
        var newConnectors = PipeNet.connectors.Where(c => c.parent.Map == parent.Map);
        if (!pipeNetManager.pipeNets.Remove(PipeNet))
        {
            pipeNetCount(pipeNetManager)++;
        }
        PipeNet = PipeNetMaker.MakePipeNet(newConnectors, parent.Map, pipeNet);
        pipeNetManager.pipeNets.Add(PipeNet);
        if (PairComp != null)
        {
            foreach (var connector in newConnectors.ToArray())
            {
                pairComp.PipeNet.UnregisterComp(connector);
            }
            pairComp = null;
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Defs.Look(ref pipeNet, "pipeNetDef");
    }

    public PipeNetDef pipeNet = DefDatabase<PipeNetDef>.GetNamed("VMF_UnassignedNet");

    private CompPipeConnector compPipeConnector;

    private CompPipeConnectorVEF pairComp;

    public static readonly AccessTools.FieldRef<PipeNetManager, int> pipeNetCount = AccessTools.FieldRefAccess<PipeNetManager, int>("pipeNetsCount");
}