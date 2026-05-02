using System.Collections.Generic;
using System.Linq;
using PipeSystem;
using SmashTools;
using UnityEngine;
using VehicleMapFramework.VMF_HarmonyPatches;
using Verse;

namespace VehicleMapFramework;

public class CompPipeConnectorVEF : CompResource, IPipeConnector
{
    public PipeNetDef pipeNet = DefDatabase<PipeNetDef>.GetNamed("VMF_UnassignedNet");

    private CompPipeConnectorVEF pairComp;

    public CompPipeConnector.PipeMod Mod => CompPipeConnector.PipeMod.VanillaExpandedFramework;

    private CompPipeConnector CompPipeConnector
    {
        get
        {
            if (field != null) return field;
            if (!parent.TryGetComp(out field))
            {
                Log.Error($"[VehicleMapFramework] CompPipeConnector not found with {parent.LabelCap}.");
            }
            return field;
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

    public Texture GizmoIcon => pipeNet?.pipeDefs?.FirstOrDefault()?.uiIcon;

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
                    Patches_VEF.pipeNetsCount(PipeNetManager) = PipeNetManager.pipeNets.Count;

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
        if (PairComp is not { parent.Spawned: true } pair || PipeNet == pairComp.PipeNet) return;
        
        var net = pair.PipeNet;
        
        foreach (var t in net.connectors)
        {
            PipeNet.RegisterComp(t);
        }
        pair.PipeNet = PipeNet;
        net.Destroy();
        var component = MapComponentCache<PipeNetManager>.GetComponent(pair.parent.Map);
        Patches_VEF.pipeNetsCount(component) = component.pipeNets.Count;
        parent.DirtyMapMesh(parent.Map);
    }

    public void DisconnectedAction()
    {
        var pipeNetManager = MapComponentCache<PipeNetManager>.GetComponent(parent.Map);
        var newConnectors = PipeNet.connectors.Where(c => c.parent.Map == parent.Map).ToArray();
        if (!pipeNetManager.pipeNets.Remove(PipeNet))
        {
            Patches_VEF.pipeNetsCount(pipeNetManager)++;
        }
        PipeNet = PipeNetMaker.MakePipeNet(newConnectors, parent.Map, pipeNet);
        pipeNetManager.pipeNets.Add(PipeNet);
        if (PairComp == null) return;
        foreach (var connector in newConnectors)
        {
            pairComp.PipeNet.UnregisterComp(connector);
        }
        pairComp = null;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Defs.Look(ref pipeNet, "pipeNetDef");
    }
}